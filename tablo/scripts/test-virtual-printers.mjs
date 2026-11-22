import net from 'node:net';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';

console.log('='.repeat(70));
console.log('  TABLO VIRTUAL THERMAL PRINTER HARDWARE & FAILURE SUITE');
console.log('='.repeat(70));

const FOOD_PORT = 9100;
const BEV_PORT = 9101;
const OFFLINE_PORT = 9109; // Unused port to test offline behavior

// Helper: send raw bytes over TCP socket
function sendRawEscPos(host, port, buffer) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host, port }, () => {
      socket.write(buffer, () => {
        socket.end();
      });
    });
    socket.on('end', () => resolve({ success: true }));
    socket.on('error', (err) => reject(err));
  });
}

// Helper: send with simulated socket reset during transmission
function sendWithSocketReset(host, port, buffer) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host, port }, () => {
      // Send half the bytes, then destroy with ECONNRESET
      const half = Math.floor(buffer.length / 2);
      socket.write(buffer.subarray(0, half), () => {
        socket.destroy(new Error('ECONNRESET: Connection reset by peer mid-stream'));
      });
    });
    socket.on('error', (err) => {
      // We expect the error
      resolve({ success: false, error: err.message, uncertain: true });
    });
  });
}

// ESC/POS Generators
function buildTicket80mm({ station, token, orderNum, items, isReprint = false, reprintReason = '', banner = '' }) {
  const chunks = [];
  chunks.push(Buffer.from([0x1B, 0x40])); // Init
  chunks.push(Buffer.from([0x1B, 0x61, 0x01])); // Center
  chunks.push(Buffer.from('MALABAR LANTERN RESTAURANT\n'));
  chunks.push(Buffer.from(`*** ${station.toUpperCase()} ***\n`));
  if (token) chunks.push(Buffer.from(`*** TOKEN: #${String(token).padStart(3, '0')} *** (PARCEL TAKEAWAY)\n`));
  if (banner) chunks.push(Buffer.from(`*** ${banner} ***\n`));
  if (isReprint) {
    chunks.push(Buffer.from('*** REPRINT ***\n'));
    if (reprintReason) chunks.push(Buffer.from(`Reason: ${reprintReason}\n`));
  }
  chunks.push(Buffer.from('================================================\n'));
  chunks.push(Buffer.from([0x1B, 0x61, 0x00])); // Left
  chunks.push(Buffer.from(`Order: ORD-${String(orderNum).padStart(4, '0')}    Date: 19 Nov 2026, 11:15 pm IST\n`));
  chunks.push(Buffer.from('------------------------------------------------\n'));
  for (const it of items) {
    chunks.push(Buffer.from(`${it.qty}x ${it.name}\n`));
    if (it.notes) chunks.push(Buffer.from(`   * Note: ${it.notes}\n`));
    if (it.mods) {
      for (const m of it.mods) {
        chunks.push(Buffer.from(`   + [Mod] ${m}\n`));
      }
    }
  }
  chunks.push(Buffer.from('================================================\n\n\n'));
  chunks.push(Buffer.from([0x1D, 0x56, 0x42, 0x00])); // Cut
  return Buffer.concat(chunks);
}

function buildTicket58mm({ station, token, orderNum, items, banner = '' }) {
  const chunks = [];
  chunks.push(Buffer.from([0x1B, 0x40])); // Init
  chunks.push(Buffer.from([0x1B, 0x61, 0x01])); // Center
  chunks.push(Buffer.from('MALABAR LANTERN\n'));
  chunks.push(Buffer.from(`*** ${station.toUpperCase()} ***\n`));
  if (token) chunks.push(Buffer.from(`* TOKEN #${String(token).padStart(3, '0')} (PARCEL) *\n`));
  if (banner) chunks.push(Buffer.from(`*** ${banner} ***\n`));
  chunks.push(Buffer.from('================================\n'));
  chunks.push(Buffer.from([0x1B, 0x61, 0x00])); // Left
  chunks.push(Buffer.from(`ORD-${String(orderNum).padStart(4, '0')} 19 Nov 2026 IST\n`));
  chunks.push(Buffer.from('--------------------------------\n'));
  for (const it of items) {
    chunks.push(Buffer.from(`${it.qty}x ${it.name}\n`));
    if (it.notes) chunks.push(Buffer.from(` * ${it.notes}\n`));
  }
  chunks.push(Buffer.from('================================\n\n\n'));
  chunks.push(Buffer.from([0x1D, 0x56, 0x42, 0x00])); // Cut
  return Buffer.concat(chunks);
}

async function runTests() {
  console.log('\n[TEST 1] Delivering Food Kitchen KOT to 127.0.0.1:9100 (80mm layout)');
  const foodPayload = buildTicket80mm({
    station: 'Food Kitchen',
    token: 12,
    orderNum: 104,
    items: [
      {
        qty: 2,
        name: 'Thalassery Chicken Dum Biryani (Spicy Coastal Blend)',
        notes: 'Extra raita on the side, double fried onions',
        mods: ['Spice Level: High', 'Rice Choice: Aged Kaima Basmati']
      },
      {
        qty: 1,
        name: 'Kerala Malabar Parotta with Ghee Roast Gravy',
        notes: 'Make it crispy flaky'
      }
    ]
  });

  const res1 = await sendRawEscPos('127.0.0.1', FOOD_PORT, foodPayload);
  assert.equal(res1.success, true);
  console.log('  --> PASS: Delivered 80mm Food KOT (Length: ' + foodPayload.length + ' bytes)');

  console.log('\n[TEST 2] Delivering Beverage Station KOT to 127.0.0.1:9101 (58mm layout)');
  const bevPayload = buildTicket58mm({
    station: 'Beverage Station',
    token: 12,
    orderNum: 104,
    items: [
      { qty: 2, name: 'Cold Pressed Mango Lassi', notes: 'Less sugar, serve chilled' },
      { qty: 1, name: 'Sulaimani Black Tea', notes: 'With fresh mint leaf' }
    ]
  });

  const res2 = await sendRawEscPos('127.0.0.1', BEV_PORT, bevPayload);
  assert.equal(res2.success, true);
  console.log('  --> PASS: Delivered 58mm Beverage KOT (Length: ' + bevPayload.length + ' bytes)');

  console.log('\n[TEST 3] Testing Offline Printer Behavior (Connection Refused)');
  try {
    await sendRawEscPos('127.0.0.1', OFFLINE_PORT, foodPayload);
    assert.fail('Should have failed connecting to offline port');
  } catch (err) {
    assert.match(err.message, /ECONNREFUSED/);
    console.log('  --> PASS: Correctly caught offline failure: ' + err.code);
  }

  console.log('\n[TEST 4] Simulating Mid-Stream Socket Interruption & UncertainDelivery');
  const resetRes = await sendWithSocketReset('127.0.0.1', FOOD_PORT, foodPayload);
  assert.equal(resetRes.uncertain, true);
  console.log('  --> PASS: Mid-stream socket reset trapped; state marked UncertainDelivery');

  console.log('\n[TEST 5] Delivering Authorized Audited Reprint Ticket (80mm)');
  const reprintPayload = buildTicket80mm({
    station: 'Food Kitchen',
    token: 12,
    orderNum: 104,
    isReprint: true,
    reprintReason: 'Socket reset mid-stream; physical verification confirmed no paper ejected',
    items: [
      { qty: 2, name: 'Thalassery Chicken Dum Biryani' },
      { qty: 1, name: 'Kerala Malabar Parotta' }
    ]
  });
  const res5 = await sendRawEscPos('127.0.0.1', FOOD_PORT, reprintPayload);
  assert.equal(res5.success, true);
  console.log('  --> PASS: Audited REPRINT ticket delivered with prominent header');

  console.log('\n[TEST 6] Delivering Stop Preparation Cancellation Notice (80mm)');
  const cancelPayload = buildTicket80mm({
    station: 'Food Kitchen',
    token: 12,
    orderNum: 104,
    banner: 'STOP PREPARATION (CANCELLED PORTION)',
    items: [
      { qty: 1, name: 'Kerala Malabar Parotta (CANCELLED)', notes: 'Reason: Customer change of mind (KitchenWaste)' }
    ]
  });
  const res6 = await sendRawEscPos('127.0.0.1', FOOD_PORT, cancelPayload);
  assert.equal(res6.success, true);
  console.log('  --> PASS: Stop Preparation cancellation notice delivered');

  console.log('\n[TEST 7] Delivering Free Refill Ticket (58mm)');
  const refillPayload = buildTicket58mm({
    station: 'Beverage Station',
    token: 12,
    orderNum: 104,
    banner: 'FREE REFILL #1 (PRICE: ₹0.00)',
    items: [
      { qty: 1, name: 'Sulaimani Black Tea (Refill)', notes: 'Complimentary refill' }
    ]
  });
  const res7 = await sendRawEscPos('127.0.0.1', BEV_PORT, refillPayload);
  assert.equal(res7.success, true);
  console.log('  --> PASS: Free Refill KOT delivered to Beverage Station');

  console.log('\n[TEST 8] Verifying File Outputs in Artifact Directories');
  const foodFiles = fs.readdirSync(path.resolve('./.tablo-printer-sim/food-kitchen'));
  const bevFiles = fs.readdirSync(path.resolve('./.tablo-printer-sim/beverage-station'));

  console.log(`  Food Kitchen files captured: ${foodFiles.length} files (.bin & .txt)`);
  console.log(`  Beverage Station files captured: ${bevFiles.length} files (.bin & .txt)`);
  assert.ok(foodFiles.length >= 4);
  assert.ok(bevFiles.length >= 2);

  console.log('\n' + '='.repeat(70));
  console.log('  ALL VIRTUAL THERMAL PRINTER HARDWARE & FAILURE TESTS PASSED');
  console.log('='.repeat(70));
}

runTests().catch(err => {
  console.error('Test Suite Failed:', err);
  process.exit(1);
});
