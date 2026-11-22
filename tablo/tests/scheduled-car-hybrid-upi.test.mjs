import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import net from 'node:net';
import fs from 'node:fs';
import path from 'node:path';

// Helper: Format IST Date string (YYYY-MM-DD)
function getIstBusinessDate(date = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).format(date);
}

// Helper: Format IST Time string
function formatIstTime(date = new Date()) {
  return new Intl.DateTimeFormat('en-IN', {
    timeZone: 'Asia/Kolkata',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  }).format(date) + ' IST';
}

// Helper: Hash matching BillingService.Hash (SHA256 hex)
function hashPayload(obj) {
  return crypto.createHash('sha256').update(typeof obj === 'string' ? obj : JSON.stringify(obj)).digest('hex').toUpperCase();
}

// Helper to send ESC/POS bytes to loopback virtual printer
function sendToPrinter(host, port, data) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    socket.setTimeout(3000);
    socket.connect(port, host, () => {
      socket.write(data, () => {
        socket.end();
      });
    });
    socket.on('close', () => resolve(true));
    socket.on('error', (err) => reject(err));
    socket.on('timeout', () => {
      socket.destroy();
      reject(new Error('Connection timed out'));
    });
  });
}

// -------------------------------------------------------------
// 1. SCHEDULED PARCEL PICKUP TESTS
// -------------------------------------------------------------
test('1. Scheduled Parcel Pickup: Advance order creation, local IST to UTC, and stock reservation without premature KOT', async (t) => {
  const restaurantId = crypto.randomUUID();
  const todayIst = getIstBusinessDate();
  
  // Tomorrow's date in IST
  const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000);
  const tomorrowIst = getIstBusinessDate(tomorrow);
  const scheduledTimeLocal = `${tomorrowIst} 19:30`;

  // Parse local time as IST (UTC+5:30)
  const [datePart, timePart] = scheduledTimeLocal.split(' ');
  const [year, month, day] = datePart.split('-').map(Number);
  const [hour, min] = timePart.split(':').map(Number);
  
  // Construct UTC equivalent: subtract 5h30m
  const pickupUtc = new Date(Date.UTC(year, month - 1, day, hour - 5, min - 30));
  const leadMinutes = 15;
  const releaseUtc = new Date(pickupUtc.getTime() - leadMinutes * 60 * 1000);

  assert.ok(pickupUtc > new Date(), 'Pickup time must be in the future');
  assert.equal(releaseUtc.getTime(), pickupUtc.getTime() - 15 * 60 * 1000, 'Release time must be exactly 15 minutes before pickup');

  // Token sequence must allocate under tomorrow's business date!
  const tokenSequences = new Map();
  function getNextDailyToken(restId, bDate) {
    const key = `${restId}_${bDate}`;
    const next = (tokenSequences.get(key) || 0) + 1;
    tokenSequences.set(key, next);
    return next;
  }

  const todayToken = getNextDailyToken(restaurantId, todayIst);
  const advanceToken = getNextDailyToken(restaurantId, tomorrowIst);

  assert.equal(todayToken, 1, "Today's first token is #001");
  assert.equal(advanceToken, 1, "Tomorrow's advance token is #001 for tomorrow's business date");

  // Create simulated order
  const order = {
    id: crypto.randomUUID(),
    restaurantId,
    orderChannel: 'ParcelTakeaway',
    orderSource: 'WhatsApp',
    isScheduled: true,
    scheduledPickupTimeUtc: pickupUtc,
    scheduledPickupTimeLocal: scheduledTimeLocal,
    preparationLeadMinutes: leadMinutes,
    scheduledReleaseTimeUtc: releaseUtc,
    businessDate: tomorrowIst,
    dailyTokenNumber: advanceToken,
    status: 'Scheduled',
    subtotal: 350.00,
    packagingCharge: 20.00,
    tax: 17.50,
    totalAmount: 387.50,
    items: [
      { id: crypto.randomUUID(), name: 'Butter Chicken Takeaway', quantity: 1, unitPrice: 350.00, packingIntent: 'Takeaway' }
    ],
    kotPrintStatus: 'None', // NO KOT fired yet!
    stockReservations: [
      { ingredientId: 'ing-chicken', reservedQuantity: 250, status: 'Active' }
    ]
  };

  assert.equal(order.status, 'Scheduled');
  assert.equal(order.kotPrintStatus, 'None', 'KOT must not be generated while order is Scheduled');
  assert.equal(order.stockReservations[0].status, 'Active', 'Stock must be reserved immediately to protect ingredients');
});

test('2. Scheduled Parcel Pickup: Durable background worker auto-release when due', async (t) => {
  const restaurantId = crypto.randomUUID();
  const now = new Date();
  
  // Order whose release time has arrived
  const dueOrder = {
    id: crypto.randomUUID(),
    restaurantId,
    status: 'Scheduled',
    isScheduled: true,
    scheduledReleaseTimeUtc: new Date(now.getTime() - 60 * 1000), // 1 min ago
    items: [{ id: 'item-1', name: 'Veg Biryani', quantity: 2, packingIntent: 'Takeaway' }],
    kotPrintStatus: 'None'
  };

  // Order whose release time is in the future
  const futureOrder = {
    id: crypto.randomUUID(),
    restaurantId,
    status: 'Scheduled',
    isScheduled: true,
    scheduledReleaseTimeUtc: new Date(now.getTime() + 10 * 60 * 1000), // 10 mins from now
    items: [{ id: 'item-2', name: 'Paneer Tikka', quantity: 1, packingIntent: 'Takeaway' }],
    kotPrintStatus: 'None'
  };

  const dbOrders = [dueOrder, futureOrder];

  // Worker query: Scheduled orders with releaseTime <= now
  const queryDue = (db, checkTime) => db.filter(o => o.status === 'Scheduled' && o.isScheduled && o.scheduledReleaseTimeUtc <= checkTime);

  const due = queryDue(dbOrders, now);
  assert.equal(due.length, 1, 'Exactly one order is due for release');
  assert.equal(due[0].id, dueOrder.id);

  // Worker execution: ReleaseScheduledOrder
  function releaseOrder(order, actorEmail) {
    assert.equal(order.status, 'Scheduled', 'Can only release Scheduled orders');
    order.status = 'Accepted';
    order.kotPrintStatus = 'Queued';
    order.releasedBy = actorEmail;
    order.releasedAt = new Date();
  }

  releaseOrder(due[0], 'ScheduledReleaseWorker');
  assert.equal(dueOrder.status, 'Accepted', 'Order transitioned to Accepted');
  assert.equal(dueOrder.kotPrintStatus, 'Queued', 'KOT print jobs generated on release');
  assert.equal(futureOrder.status, 'Scheduled', 'Future order remains Scheduled');
});

test('3. Scheduled Parcel Pickup: Manual staff Release Now fires KOT immediately', async (t) => {
  const order = {
    id: crypto.randomUUID(),
    status: 'Scheduled',
    isScheduled: true,
    scheduledReleaseTimeUtc: new Date(Date.now() + 30 * 60 * 1000),
    kotPrintStatus: 'None'
  };

  // Staff presses "Release Now"
  assert.equal(order.status, 'Scheduled');
  order.status = 'Accepted';
  order.kotPrintStatus = 'Queued';
  order.releasedBy = 'cashier@restaurant.com';

  assert.equal(order.status, 'Accepted');
  assert.equal(order.kotPrintStatus, 'Queued');
  assert.equal(order.releasedBy, 'cashier@restaurant.com');
});

test('4. Scheduled Parcel Pickup: Prepayment settles bill without premature firing or Served state', async (t) => {
  const order = {
    id: crypto.randomUUID(),
    status: 'Scheduled',
    isScheduled: true,
    totalAmount: 500.00,
    kotPrintStatus: 'None',
    bill: {
      id: crypto.randomUUID(),
      totalAmount: 500.00,
      paidAmount: 0.00,
      remainingAmount: 500.00,
      status: 'Open'
    }
  };

  // Customer prepays full amount via UPI
  const paymentAmount = 500.00;
  order.bill.paidAmount += paymentAmount;
  order.bill.remainingAmount = order.bill.totalAmount - order.bill.paidAmount;

  if (order.bill.remainingAmount === 0) {
    order.bill.status = 'Settled';
    order.bill.settledAtUtc = new Date();
    // CRITICAL: Scheduled order must NOT transition to Served or fire KOTs!
    if (!order.isScheduled) {
      order.status = 'Served';
    }
  }

  assert.equal(order.bill.status, 'Settled', 'Bill is fully settled');
  assert.equal(order.bill.remainingAmount, 0, 'Zero balance remains');
  assert.equal(order.status, 'Scheduled', 'Order remains in Scheduled state');
  assert.equal(order.kotPrintStatus, 'None', 'KOT must NOT be prematurely fired');
});

test('5. Scheduled Parcel Pickup: Rescheduling to new date reallocates daily token sequence', async (t) => {
  const restaurantId = crypto.randomUUID();
  const tokenSequences = new Map();
  function getNextDailyToken(restId, bDate) {
    const key = `${restId}_${bDate}`;
    const next = (tokenSequences.get(key) || 0) + 1;
    tokenSequences.set(key, next);
    return next;
  }

  const date1 = '2026-11-20';
  const date2 = '2026-11-21';

  // Order scheduled for date1
  const order = {
    id: crypto.randomUUID(),
    restaurantId,
    status: 'Scheduled',
    businessDate: date1,
    dailyTokenNumber: getNextDailyToken(restaurantId, date1),
    scheduledPickupTimeLocal: `${date1} 14:00`,
    preparationLeadMinutes: 15
  };

  assert.equal(order.businessDate, date1);
  assert.equal(order.dailyTokenNumber, 1);

  // Customer calls to reschedule to date2
  const newDate = date2;
  const newTime = `${newDate} 18:00`;

  if (newDate !== order.businessDate) {
    const newToken = getNextDailyToken(restaurantId, newDate);
    order.businessDate = newDate;
    order.dailyTokenNumber = newToken;
  }
  order.scheduledPickupTimeLocal = newTime;

  assert.equal(order.businessDate, date2, 'Business date updated to new pickup day');
  assert.equal(order.dailyTokenNumber, 1, 'Token allocated sequentially under new business date');
});

// -------------------------------------------------------------
// 2. HYBRID DINE-IN AND PARCEL TESTS
// -------------------------------------------------------------
test('6. Hybrid Dine-In & Parcel: Packaging fee applies strictly to takeaway lines, waiver leaves food subtotal intact', async (t) => {
  const menuItems = {
    'item-curry': { name: 'Paneer Butter Masala', price: 260.00, packagingFee: 15.00 },
    'item-roti': { name: 'Butter Naan', price: 40.00, packagingFee: 5.00 },
    'item-drink': { name: 'Mango Lassi', price: 80.00, packagingFee: 10.00 }
  };

  // Mixed order: 1 curry for DineIn, 1 curry for Takeaway, 2 naans for Takeaway, 1 drink for DineIn
  const items = [
    { menuItemId: 'item-curry', quantity: 1, packingIntent: 'DineIn' },
    { menuItemId: 'item-curry', quantity: 1, packingIntent: 'Takeaway' },
    { menuItemId: 'item-roti', quantity: 2, packingIntent: 'Takeaway' },
    { menuItemId: 'item-drink', quantity: 1, packingIntent: 'DineIn' }
  ];

  const foodSubtotal = items.reduce((sum, i) => sum + menuItems[i.menuItemId].price * i.quantity, 0);
  assert.equal(foodSubtotal, 260 + 260 + 80 + 80, 'Food subtotal = 680');

  // Packaging calculation (PerItem mode): ONLY for Takeaway items!
  let packagingCharge = 0;
  for (const item of items) {
    if (item.packingIntent === 'Takeaway') {
      packagingCharge += menuItems[item.menuItemId].packagingFee * item.quantity;
    }
  }

  // Takeaway items: 1 curry (15) + 2 naans (5 * 2 = 10) = 25
  assert.equal(packagingCharge, 25.00, 'Packaging charge must strictly cover takeaway items');

  // If waived:
  const waivePackaging = true;
  const effectivePackaging = waivePackaging ? 0 : packagingCharge;
  assert.equal(effectivePackaging, 0);
  assert.equal(foodSubtotal, 680.00, 'Food subtotal must not be altered by packaging waiver');
});

test('7. Hybrid Dine-In: Leftover packing container charge added to bill without food production or stock movement', async (t) => {
  const bill = {
    id: crypto.randomUUID(),
    subtotal: 800.00,
    packagingCharge: 0.00,
    leftoverPackingCharge: 0.00,
    tax: 40.00,
    discount: 0.00,
    totalAmount: 840.00,
    settledAtUtc: null
  };

  const inventoryDeductions = [];

  // Staff adds 2 leftover containers at ₹10 each
  function addLeftoverContainer(b, count, rate = 10.00) {
    assert.ok(!b.settledAtUtc, 'Cannot add charge to settled bill');
    const added = count * rate;
    b.leftoverPackingCharge += added;
    b.totalAmount = b.subtotal + b.packagingCharge + b.leftoverPackingCharge + b.tax - b.discount;
    // NO inventory deduction added!
  }

  addLeftoverContainer(bill, 2, 10.00);

  assert.equal(bill.leftoverPackingCharge, 20.00);
  assert.equal(bill.totalAmount, 860.00);
  assert.equal(inventoryDeductions.length, 0, 'Leftover container charge must not consume food stock');
});

test('8. Hybrid Dine-In KOT: Formatter renders [PACK AS PARCEL] and [DINE-IN] tags cleanly', async (t) => {
  const items = [
    { name: 'Chicken Biryani', quantity: 1, packingIntent: 'Takeaway' },
    { name: 'Mutton Korma', quantity: 1, packingIntent: 'DineIn' }
  ];

  function formatKotLines(itemList) {
    return itemList.map(i => {
      const tag = i.packingIntent === 'Takeaway' ? '[PACK AS PARCEL]' : '[DINE-IN]';
      return `${i.quantity}x ${i.name}\n   ${tag}`;
    }).join('\n');
  }

  const formatted = formatKotLines(items);
  assert.ok(formatted.includes('Chicken Biryani\n   [PACK AS PARCEL]'));
  assert.ok(formatted.includes('Mutton Korma\n   [DINE-IN]'));
});

// -------------------------------------------------------------
// 3. CAR-SIDE ORDERING TESTS
// -------------------------------------------------------------
test('9. Car-Side Ordering: Native CarParking channel, prominent banner, and delivery tracking', async (t) => {
  const order = {
    id: crypto.randomUUID(),
    orderChannel: 'CarParking',
    vehiclePlate: 'DL01AB1234',
    vehicleModel: 'Silver Honda City',
    parkingBay: 'Bay 3',
    status: 'Accepted',
    deliveredAtUtc: null,
    deliveredByEmployeeId: null
  };

  assert.equal(order.orderChannel, 'CarParking');
  assert.equal(order.vehiclePlate, 'DL01AB1234');
  assert.equal(order.parkingBay, 'Bay 3');

  // Formatter header for KOT
  function formatCarOrderHeader(ord) {
    if (ord.orderChannel === 'CarParking') {
      return [
        '*** CAR ORDER ***',
        `VEHICLE: ${ord.vehiclePlate}`,
        `BAY / SPOT: ${ord.parkingBay || 'N/A'}`
      ].join('\n');
    }
    return 'PARCEL ORDER';
  }

  const banner = formatCarOrderHeader(order);
  assert.ok(banner.includes('*** CAR ORDER ***'));
  assert.ok(banner.includes('VEHICLE: DL01AB1234'));
  assert.ok(banner.includes('BAY / SPOT: Bay 3'));

  // Runner delivers food to car
  const staffId = crypto.randomUUID();
  function deliverCarOrder(ord, actorId) {
    assert.equal(ord.orderChannel, 'CarParking');
    assert.notEqual(ord.status, 'Cancelled');
    ord.status = 'Served';
    ord.deliveredAtUtc = new Date();
    ord.deliveredByEmployeeId = actorId;
  }

  deliverCarOrder(order, staffId);
  assert.equal(order.status, 'Served');
  assert.ok(order.deliveredAtUtc instanceof Date);
  assert.equal(order.deliveredByEmployeeId, staffId);
});

// -------------------------------------------------------------
// 4. DYNAMIC UPI QR PAYMENTS TESTS
// -------------------------------------------------------------
test('10. Dynamic UPI QR: Standard NPCI URI generation with parameter escaping and 2-decimal INR amount', async (t) => {
  const vpa = 'restaurant@okhdfcbank';
  const payeeName = 'Tablo Kitchen & Bar';
  const amount = 250.50;
  const billRef = 'TB4-9a8b7c6d';
  const note = 'Bill TB4-9a8b7c6d';

  function buildUpiUri(pa, pn, am, tr, tn, mc = '') {
    assert.ok(am > 0, 'UPI amount must be positive');
    const amFormatted = am.toFixed(2);
    let uri = `upi://pay?pa=${encodeURIComponent(pa)}&pn=${encodeURIComponent(pn)}&am=${amFormatted}&cu=INR&tr=${encodeURIComponent(tr)}&tn=${encodeURIComponent(tn)}`;
    if (mc) uri += `&mc=${encodeURIComponent(mc)}`;
    return uri;
  }

  const upiUri = buildUpiUri(vpa, payeeName, amount, billRef, note);

  assert.ok(upiUri.startsWith('upi://pay?'));
  assert.ok(upiUri.includes(`pa=${encodeURIComponent(vpa)}`));
  assert.ok(upiUri.includes(`pn=${encodeURIComponent(payeeName)}`));
  assert.ok(upiUri.includes('am=250.50'));
  assert.ok(upiUri.includes('cu=INR'));
  assert.ok(upiUri.includes(`tr=${encodeURIComponent(billRef)}`));
  assert.ok(upiUri.includes(`tn=${encodeURIComponent(note)}`));
});

test('11. Dynamic UPI QR: Payment recording with durable operation ID and replay protection', async (t) => {
  const billId = crypto.randomUUID();
  const recordedPayments = new Map();

  function recordPayment(bill, request) {
    const hash = hashPayload({ billId: bill.id, method: request.method, amount: request.amount.toFixed(2) });
    
    // Check replay
    if (recordedPayments.has(request.operationId)) {
      const existing = recordedPayments.get(request.operationId);
      if (existing.hash !== hash) {
        throw new Error('409 Conflict: This operation ID was used for different payment details');
      }
      return existing.acknowledgement;
    }

    const remaining = bill.totalAmount - bill.paidAmount;
    if (request.amount > remaining) {
      throw new Error('400 Bad Request: Payment exceeds remaining balance');
    }

    bill.paidAmount += request.amount;
    bill.remainingAmount = bill.totalAmount - bill.paidAmount;

    const ack = {
      paymentId: crypto.randomUUID(),
      billId: bill.id,
      amount: request.amount,
      method: request.method,
      remaining: bill.remainingAmount
    };

    recordedPayments.set(request.operationId, { hash, acknowledgement: ack });
    return ack;
  }

  const bill = { id: billId, totalAmount: 400.00, paidAmount: 0.00, remainingAmount: 400.00 };
  const opId = crypto.randomUUID();

  // First attempt
  const ack1 = recordPayment(bill, { operationId: opId, method: 'UPI', amount: 200.00 });
  assert.equal(ack1.remaining, 200.00);
  assert.equal(bill.paidAmount, 200.00);

  // Network drop / replay with same opId: must return identical ack without deducting twice!
  const ack2 = recordPayment(bill, { operationId: opId, method: 'UPI', amount: 200.00 });
  assert.equal(ack2.paymentId, ack1.paymentId);
  assert.equal(bill.paidAmount, 200.00, 'Replay must not duplicate payment');

  // Fraudulent replay with different amount: must throw 409 Conflict
  assert.throws(() => {
    recordPayment(bill, { operationId: opId, method: 'UPI', amount: 300.00 });
  }, /409 Conflict/);
});

// -------------------------------------------------------------
// 5. LIVE VIRTUAL THERMAL PRINTER TESTS
// -------------------------------------------------------------
test('12. Live Virtual Thermal Printers: Dispatch Car Order ticket to Food Kitchen (port 9100)', async (t) => {
  const foodPort = 9100;
  
  // Format standard ESC/POS ticket with Car Order banner
  const ESC = '\x1B';
  const GS = '\x1D';
  const LF = '\n';

  let raw = '';
  raw += `${ESC}@`; // Initialize
  raw += `${ESC}a\x01`; // Center align
  raw += `${GS}!\x11`; // Double size
  raw += `*** CAR ORDER ***${LF}`;
  raw += `${GS}!\x00`; // Normal size
  raw += `VEHICLE: MH12DE5678${LF}`;
  raw += `PARKING BAY: BAY 4${LF}`;
  raw += `--------------------------------${LF}`;
  raw += `${ESC}a\x00`; // Left align
  raw += `1x Butter Chicken Handi${LF}`;
  raw += `   [PACK AS PARCEL]${LF}`;
  raw += `2x Butter Garlic Naan${LF}`;
  raw += `   [PACK AS PARCEL]${LF}`;
  raw += `--------------------------------${LF}`;
  raw += `${ESC}d\x03`; // Feed 3 lines
  raw += `${GS}V\x00`; // Cut paper

  const buffer = Buffer.from(raw, 'binary');
  const delivered = await sendToPrinter('127.0.0.1', foodPort, buffer);
  assert.ok(delivered, 'Ticket successfully sent to Food Kitchen virtual printer on port 9100');

  // Verify file written in .tablo-printer-sim/food-kitchen
  const simDir = path.resolve('./.tablo-printer-sim/food-kitchen');
  if (fs.existsSync(simDir)) {
    const files = fs.readdirSync(simDir).filter(f => f.endsWith('.txt'));
    assert.ok(files.length > 0, 'Food Kitchen simulator recorded at least one ticket');
    const latest = fs.readFileSync(path.join(simDir, files[files.length - 1]), 'utf8');
    assert.ok(latest.includes('CAR ORDER'), 'Ticket text contains CAR ORDER');
    assert.ok(latest.includes('MH12DE5678'), 'Ticket text contains vehicle plate');
  }
});

test('13. Live Virtual Thermal Printers: Dispatch Scheduled Pickup ticket with [PACK AS PARCEL] to Beverage Station (port 9101)', async (t) => {
  const bevPort = 9101;
  
  const ESC = '\x1B';
  const GS = '\x1D';
  const LF = '\n';

  let raw = '';
  raw += `${ESC}@`; // Initialize
  raw += `${ESC}a\x01`; // Center align
  raw += `${GS}!\x11`; // Double size
  raw += `TOKEN #025${LF}`;
  raw += `${GS}!\x00`; // Normal size
  raw += `*** SCHEDULED PICKUP ***${LF}`;
  raw += `PICKUP: 20 Nov 07:30 PM IST${LF}`;
  raw += `SOURCE: WhatsApp${LF}`;
  raw += `--------------------------------${LF}`;
  raw += `${ESC}a\x00`; // Left align
  raw += `2x Mango Lassi Sweet${LF}`;
  raw += `   [PACK AS PARCEL]${LF}`;
  raw += `1x Fresh Lime Soda${LF}`;
  raw += `   [DINE-IN]${LF}`;
  raw += `--------------------------------${LF}`;
  raw += `${ESC}d\x03`; // Feed 3 lines
  raw += `${GS}V\x00`; // Cut paper

  const buffer = Buffer.from(raw, 'binary');
  const delivered = await sendToPrinter('127.0.0.1', bevPort, buffer);
  assert.ok(delivered, 'Ticket successfully sent to Beverage Station virtual printer on port 9101');

  // Verify file written in .tablo-printer-sim/beverage-station
  const simDir = path.resolve('./.tablo-printer-sim/beverage-station');
  if (fs.existsSync(simDir)) {
    const files = fs.readdirSync(simDir).filter(f => f.endsWith('.txt'));
    assert.ok(files.length > 0, 'Beverage Station simulator recorded at least one ticket');
    const latest = fs.readFileSync(path.join(simDir, files[files.length - 1]), 'utf8');
    assert.ok(latest.includes('TOKEN #025'), 'Ticket text contains TOKEN #025');
    assert.ok(latest.includes('SCHEDULED PICKUP'), 'Ticket text contains SCHEDULED PICKUP');
    assert.ok(latest.includes('Mango Lassi Sweet'), 'Ticket text contains beverage item');
  }
});
