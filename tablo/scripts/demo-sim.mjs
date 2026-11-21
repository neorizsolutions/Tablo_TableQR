import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const simDir = path.resolve(__dirname, '../.tablo-printer-sim');
if (!fs.existsSync(simDir)) {
  fs.mkdirSync(simDir, { recursive: true });
}

function center(text, width) {
  if (text.length >= width) return text;
  const left = Math.floor((width - text.length) / 2);
  const right = width - text.length - left;
  return ' '.repeat(left) + text + ' '.repeat(right);
}

function padSides(left, right, width) {
  const spaces = width - left.length - right.length;
  if (spaces <= 0) return `${left} ${right}`;
  return left + ' '.repeat(spaces) + right;
}

function generateSampleTicket({ restaurantName, stationName, ticketNumber, orderNumber, tableNumber, groupLabel, items, paperWidthMm, isReprint = false, isIncremental = false }) {
  const cols = paperWidthMm === 58 ? 32 : 48;
  const divider = '-'.repeat(cols);
  const doubleDivider = '='.repeat(cols);
  const timestamp = '19 Nov 2026, 04:30 pm IST';

  const lines = [];
  lines.push(center(restaurantName.toUpperCase(), cols));
  lines.push(center(`*** ${stationName.toUpperCase()} ***`, cols));

  if (isReprint) {
    lines.push(center('*** REPRINT ***', cols));
    lines.push(center('Reason: Ticket paper lost in kitchen', cols));
  } else if (isIncremental) {
    lines.push(center('*** NEW ADDITION ***', cols));
  }

  lines.push(doubleDivider);
  const tableStr = `Table ${tableNumber}${groupLabel ? ` (${groupLabel})` : ''}`;
  lines.push(padSides(`Ticket: ${ticketNumber}`, `Order: ORD-${String(orderNumber).padStart(4, '0')}`, cols));
  lines.push(padSides(tableStr, timestamp, cols));
  lines.push(divider);

  let totalQty = 0;
  for (const item of items) {
    totalQty += item.qty;
    lines.push(`${item.qty}x ${item.name}`);
    if (item.instructions) {
      lines.push(`   * Note: ${item.instructions}`);
    }
  }

  lines.push(divider);
  lines.push(padSides(`Total Items: ${totalQty}`, isReprint ? 'REPRINT' : (isIncremental ? 'INCREMENTAL' : 'INITIAL'), cols));
  lines.push(doubleDivider);

  const textContent = lines.join('\n');

  // Binary ESC/POS buffer
  const chunks = [];
  chunks.push(Buffer.from([0x1B, 0x40])); // Init
  chunks.push(Buffer.from([0x1B, 0x61, 0x01])); // Center
  chunks.push(Buffer.from([0x1B, 0x45, 0x01])); // Bold
  chunks.push(Buffer.from(`${restaurantName.toUpperCase()}\n`));
  chunks.push(Buffer.from(`*** ${stationName.toUpperCase()} ***\n`));
  if (isReprint) {
    chunks.push(Buffer.from('*** REPRINT ***\n'));
    chunks.push(Buffer.from('Reason: Ticket paper lost in kitchen\n'));
  }
  chunks.push(Buffer.from([0x1B, 0x45, 0x00])); // Bold off
  chunks.push(Buffer.from(`${doubleDivider}\n`));
  chunks.push(Buffer.from([0x1B, 0x61, 0x00])); // Left
  chunks.push(Buffer.from(padSides(`Ticket: ${ticketNumber}`, `Order: ORD-${String(orderNumber).padStart(4, '0')}`, cols) + '\n'));
  chunks.push(Buffer.from(padSides(tableStr, timestamp, cols) + '\n'));
  chunks.push(Buffer.from(`${divider}\n`));

  for (const item of items) {
    chunks.push(Buffer.from([0x1B, 0x45, 0x01, 0x1D, 0x21, 0x01])); // Bold + 2x height
    chunks.push(Buffer.from(`${item.qty}x ${item.name}\n`));
    chunks.push(Buffer.from([0x1D, 0x21, 0x00, 0x1B, 0x45, 0x00]));
    if (item.instructions) {
      chunks.push(Buffer.from(`   * Note: ${item.instructions}\n`));
    }
  }
  chunks.push(Buffer.from(`${divider}\n`));
  chunks.push(Buffer.from(padSides(`Total Items: ${totalQty}`, isReprint ? 'REPRINT' : 'INITIAL', cols) + '\n'));
  chunks.push(Buffer.from(`${doubleDivider}\n\n\n`));
  chunks.push(Buffer.from([0x1D, 0x56, 0x42, 0x00])); // Cut

  const binaryContent = Buffer.concat(chunks);
  return { textContent, binaryContent };
}

// 1. Single Kitchen 80mm sample
const single80 = generateSampleTicket({
  restaurantName: 'Tablo Bistro',
  stationName: 'Main Kitchen',
  ticketNumber: 'KOT-0101-1-MAIN',
  orderNumber: 101,
  tableNumber: 4,
  groupLabel: 'Party of 4',
  paperWidthMm: 80,
  items: [
    { qty: 2, name: 'Paneer Butter Masala', instructions: 'Medium spicy, extra gravy' },
    { qty: 4, name: 'Butter Naan', instructions: null },
    { qty: 2, name: 'Sweet Mango Lassi', instructions: 'Serve chilled' }
  ]
});

// 2. Beverage Station 58mm sample
const bev58 = generateSampleTicket({
  restaurantName: 'Tablo Bistro',
  stationName: 'Beverage Bar',
  ticketNumber: 'KOT-0101-1-BAR',
  orderNumber: 101,
  tableNumber: 4,
  groupLabel: 'Party of 4',
  paperWidthMm: 58,
  items: [
    { qty: 2, name: 'Sweet Mango Lassi', instructions: 'Serve chilled' }
  ]
});

// 3. Reprint Food Station 80mm sample
const reprint80 = generateSampleTicket({
  restaurantName: 'Tablo Bistro',
  stationName: 'Food Kitchen',
  ticketNumber: 'KOT-0101-1-FOOD-R1',
  orderNumber: 101,
  tableNumber: 4,
  groupLabel: 'Party of 4',
  paperWidthMm: 80,
  isReprint: true,
  items: [
    { qty: 2, name: 'Paneer Butter Masala', instructions: 'Medium spicy, extra gravy' },
    { qty: 4, name: 'Butter Naan', instructions: null }
  ]
});

fs.writeFileSync(path.join(simDir, 'job-demo-single-80mm.txt'), single80.textContent, 'utf8');
fs.writeFileSync(path.join(simDir, 'job-demo-single-80mm.bin'), single80.binaryContent);

fs.writeFileSync(path.join(simDir, 'job-demo-bar-58mm.txt'), bev58.textContent, 'utf8');
fs.writeFileSync(path.join(simDir, 'job-demo-bar-58mm.bin'), bev58.binaryContent);

fs.writeFileSync(path.join(simDir, 'job-demo-food-reprint-80mm.txt'), reprint80.textContent, 'utf8');
fs.writeFileSync(path.join(simDir, 'job-demo-food-reprint-80mm.bin'), reprint80.binaryContent);

console.log('Generated demonstration files in .tablo-printer-sim:');
console.log(' - job-demo-single-80mm.txt & .bin');
console.log(' - job-demo-bar-58mm.txt & .bin');
console.log(' - job-demo-food-reprint-80mm.txt & .bin');
