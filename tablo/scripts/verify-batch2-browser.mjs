import puppeteer from 'puppeteer';
import fs from 'node:fs';
import path from 'node:path';

async function main() {
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 950 });

    console.log('--- Step 1: Public Storefront Guest View ---');
    // Ensure clean storage for unauthenticated guest
    await page.goto('http://127.0.0.1:5173/order', { waitUntil: 'networkidle0' });
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    await page.goto('http://127.0.0.1:5173/order', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1200));

    // Save Guest Storefront initial screenshot
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/PUB-003-public-storefront-guest.png' });
    console.log('Captured qa/tablo-e2e/screenshots/PUB-003-public-storefront-guest.png');

    // Select Takeaway Pickup channel
    const buttons = await page.$$('button');
    for (const btn of buttons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && text.includes('Takeaway Pickup')) {
        await btn.click();
        await new Promise((r) => setTimeout(r, 500));
        break;
      }
    }

    // Click Place Order
    const placeButtons = await page.$$('button');
    for (const btn of placeButtons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && text.includes('Place Order')) {
        await btn.click();
        await new Promise((r) => setTimeout(r, 1500));
        break;
      }
    }

    // Save Guest Order Confirmed Screenshot with UPI QR
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/STR-002-storefront-order-confirmation.png' });
    console.log('Captured qa/tablo-e2e/screenshots/STR-002-storefront-order-confirmation.png');

    console.log('--- Step 2: Central Kitchen & Transfers Screen ---');
    // Login as Admin
    const loginRes = await fetch('http://127.0.0.1:5166/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@tablodemo.com', password: 'password123' })
    });
    const authData = await loginRes.json();

    await page.goto('http://127.0.0.1:5173/auth', { waitUntil: 'networkidle0' });
    await page.evaluate((auth) => {
      localStorage.setItem('tablo_token', auth.token);
      localStorage.setItem('tablo_user', JSON.stringify(auth.user));
    }, authData);

    // Navigate to Central Kitchen
    await page.goto('http://127.0.0.1:5173/inventory?tab=central-kitchen', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1200));

    // Switch to Transfers Tab to show reconciled shortage receipts
    const ckButtons = await page.$$('button');
    for (const btn of ckButtons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && text.includes('Stock Transfers')) {
        await btn.click();
        await new Promise((r) => setTimeout(r, 1000));
        break;
      }
    }
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/CKM-002-reconciled-transfers-postgres.png' });
    console.log('Captured qa/tablo-e2e/screenshots/CKM-002-reconciled-transfers-postgres.png');

    console.log('--- Step 3: Floor Bills & Unpaid Bill Dynamic UPI QR ---');
    await page.goto('http://127.0.0.1:5173/dashboard?tab=floor', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1500));

    const billButtons = await page.$$('button');
    for (const btn of billButtons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && (text.includes('View customer bill') || text.includes('Customer bill'))) {
        await btn.click();
        await new Promise((r) => setTimeout(r, 1500));
        break;
      }
    }
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/PYM-002-unpaid-bill-qr-dialog.png' });
    console.log('Captured qa/tablo-e2e/screenshots/PYM-002-unpaid-bill-qr-dialog.png');

  } finally {
    await browser.close();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
