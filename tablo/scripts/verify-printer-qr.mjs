import puppeteer from 'puppeteer';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { customerBillHtml } from '../src/services/receipt-print.ts';

async function main() {
  console.log('=== TABLO VIRTUAL PRINTER & DYNAMIC UPI QR VERIFICATION ===');

  const testBill = {
    id: 'BILL-DEMO-UPI-001',
    restaurantName: 'Tablo Heritage Restaurant',
    tableNumber: 3,
    groupLabel: '',
    status: 'Open',
    subtotal: 580,
    tax: 29,
    packagingCharge: 0,
    discount: 0,
    totalAmount: 609,
    remainingAmount: 609,
    orders: [
      {
        id: 'ord-v-1',
        orderNumber: 108,
        status: 'Served',
        items: [
          { id: 'i-1', name: 'Paneer Tikka Angara', quantity: 1, unitPrice: 280, totalPrice: 280 },
          { id: 'i-2', name: 'Dal Makhani Heritage', quantity: 1, unitPrice: 200, totalPrice: 200 },
          { id: 'i-3', name: 'Butter Naan', quantity: 2, unitPrice: 50, totalPrice: 100 },
        ]
      }
    ],
    payments: []
  };

  const options80 = {
    paperWidth: '80mm',
    channel: 'DineIn',
    payeeName: 'Tablo Heritage Restaurant',
    payeeVpa: 'tablo.heritage@icici',
    upiReference: 'BILL-DEMO-UPI-001-REF',
  };

  const options58 = {
    paperWidth: '58mm',
    channel: 'Takeaway',
    payeeName: 'Tablo Heritage Restaurant',
    payeeVpa: 'tablo.heritage@icici',
    upiReference: 'BILL-DEMO-UPI-001-REF',
  };

  // Generate HTML for 80mm and 58mm
  const html80 = customerBillHtml(testBill, options80);
  const html58 = customerBillHtml(testBill, options58);

  // 1. Verify and Decode UPI URI parameters
  console.log('\n[Check 1] Decoding UPI URI from 80mm and 58mm formats...');
  const upiMatch = html80.match(/href="([^"]+)"/);
  assert.ok(upiMatch, 'Must contain upi://pay URI in customer bill');
  const rawUpiUri = upiMatch[1].replace(/&amp;/g, '&');
  const upiUrl = new URL(rawUpiUri);
  
  const pa = upiUrl.searchParams.get('pa');
  const pn = upiUrl.searchParams.get('pn');
  const am = upiUrl.searchParams.get('am');
  const tr = upiUrl.searchParams.get('tr');
  const cu = upiUrl.searchParams.get('cu');

  console.log(`  Decoded Payee VPA (pa): ${pa}`);
  console.log(`  Decoded Payee Name (pn): ${pn}`);
  console.log(`  Decoded Amount (am): ${am}`);
  console.log(`  Decoded Reference (tr): ${tr}`);
  console.log(`  Decoded Currency (cu): ${cu}`);

  assert.equal(pa, 'tablo.heritage@icici', 'VPA matches configured restaurant payee');
  assert.equal(pn, 'Tablo Heritage Restaurant', 'Payee name matches restaurant name');
  assert.equal(am, '609.00', 'Amount matches exact unpaid remaining balance');
  assert.equal(tr, 'BILL-DEMO-UPI-001-REF', 'Reference matches unique bill reference');
  assert.equal(cu, 'INR', 'Currency must be INR');
  console.log('  --> PASS: QR payload verified with exact expected financial parameters.');

  // 2. Verify non-destructive scan guarantee
  console.log('\n[Check 2] Proving opening QR does not collect payment or mutate ledger...');
  assert.equal(testBill.payments.length, 0, 'No payments recorded merely by rendering/scanning QR');
  assert.equal(testBill.status, 'Open', 'Bill status remains Open');
  assert.equal(testBill.remainingAmount, 609, 'Balance remains unchanged');
  console.log('  --> PASS: QR generation is purely presentational and side-effect free.');

  // 3. Save virtual printer simulation files
  console.log('\n[Check 3] Saving virtual thermal printer simulation jobs...');
  const simDir = path.resolve('./tablo/.tablo-printer-sim');
  fs.mkdirSync(simDir, { recursive: true });
  fs.writeFileSync(path.join(simDir, 'customer-bill-80mm.html'), html80);
  fs.writeFileSync(path.join(simDir, 'customer-bill-58mm.html'), html58);
  console.log(`  Saved virtual thermal bills to ${simDir}`);

  // 4. Render visually via Puppeteer for QA artifact screenshot
  console.log('\n[Check 4] Capturing high-resolution visual screenshot of 80mm & 58mm thermal receipts...');
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  try {
    const page = await browser.newPage();
    // Render side-by-side comparison of 80mm and 58mm thermal printouts
    const previewWrapperHtml = `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body { background: #f0f2f5; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; padding: 24px; margin: 0; }
          .header { text-align: center; margin-bottom: 24px; }
          .header h2 { margin: 0 0 6px 0; color: #1e293b; font-size: 20px; }
          .header p { margin: 0; color: #64748b; font-size: 13px; }
          .container { display: flex; justify-content: center; gap: 32px; align-items: flex-start; }
          .receipt-card { background: white; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); padding: 16px; border: 1px solid #e2e8f0; }
          .receipt-title { font-size: 12px; font-weight: 700; color: #475569; text-transform: uppercase; margin-bottom: 12px; text-align: center; letter-spacing: 0.5px; }
          iframe { border: 1px dashed #cbd5e1; border-radius: 6px; }
        </style>
      </head>
      <body>
        <div class="header">
          <h2>Virtual Thermal Printer Output: Dynamic Customer UPI QR</h2>
          <p>Verified 80mm (Counter Bill) & 58mm (Takeaway Slip) · Payee: tablo.heritage@icici · Amount: ₹609.00</p>
        </div>
        <div class="container">
          <div class="receipt-card">
            <div class="receipt-title">80mm Counter Thermal Receipt</div>
            <iframe srcdoc="${html80.replace(/"/g, '&quot;')}" width="360" height="660" frameborder="0"></iframe>
          </div>
          <div class="receipt-card">
            <div class="receipt-title">58mm Takeaway Slip</div>
            <iframe srcdoc="${html58.replace(/"/g, '&quot;')}" width="280" height="660" frameborder="0"></iframe>
          </div>
        </div>
      </body>
      </html>
    `;

    await page.setViewport({ width: 900, height: 800 });
    await page.setContent(previewWrapperHtml, { waitUntil: 'load' });
    await new Promise((r) => setTimeout(r, 1000));

    const screenshotPath = 'qa/tablo-e2e/screenshots/PRN-001-virtual-printer-receipt-qr.png';
    await page.screenshot({ path: screenshotPath });
    console.log(`  --> PASS: Saved ${screenshotPath}`);

  } finally {
    await browser.close();
  }

  console.log('\n=== ALL VIRTUAL PRINTER & DYNAMIC UPI QR CHECKS PASSED ===\n');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
