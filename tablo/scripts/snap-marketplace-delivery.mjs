import puppeteer from 'puppeteer';
import path from 'node:path';

const SCREENSHOT_DIR = path.resolve('D:/Projects/tablo/qa/tablo-e2e/screenshots');
const BASE_URL = 'http://127.0.0.1:5173';

(async () => {
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: false, // Visible browser per prompt instructions: "Open TABLO in a visible browser... Leave the verified local services running and the browser open on Parcel Counter."
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu', '--window-size=1440,900']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });

  // Mock demo auth credentials in localStorage
  await page.goto(BASE_URL + '/');
  await page.evaluate(() => {
    const mockUser = {
      id: '11111111-0000-0000-0000-000000000001',
      restaurantId: '22222222-0000-0000-0000-000000000001',
      restaurantName: 'Tablo Heritage Restaurant',
      email: 'admin@tablodemo.com',
      role: 'Admin',
      roles: ['Admin', 'Manager', 'Cashier'],
      entitlements: ['thermal_printing', 'multiple_stations', 'central_kitchen_transfers', 'parcel_counter']
    };
    localStorage.setItem('tablo_token', 'demo-admin-jwt-token-2026');
    localStorage.setItem('tablo_user', JSON.stringify(mockUser));
  });

  // 1. DLV-001: Open Parcel Counter and select Swiggy Delivery
  await page.goto(BASE_URL + '/admin?tab=parcel', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1200));

  // Click Swiggy / Zomato channel button
  const channelButtons = await page.$$('button');
  for (const b of channelButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Swiggy / Zomato')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 600));

  // Type external reference
  const inputs = await page.$$('input');
  for (const input of inputs) {
    const placeholder = await page.evaluate(el => el.getAttribute('placeholder'), input);
    if (placeholder && placeholder.includes('SWIGGY-8921')) {
      await input.type('SWIGGY-8921');
      break;
    }
  }

  // Add 1 Biryani to cart if available
  const addButtons = await page.$$('button');
  for (const b of addButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Add')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 800));

  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'DLV-001-swiggy-order-entry.png') });
  console.log('DLV-001 captured');

  // 2. DLV-002: Select Zomato delivery order
  const selects = await page.$$('select');
  for (const sel of selects) {
    const val = await page.evaluate(el => el.value, sel);
    if (val === 'Swiggy') {
      await sel.select('Zomato');
      break;
    }
  }
  // Change external ref to ZOM-4412
  for (const input of inputs) {
    const val = await page.evaluate(el => el.value, input);
    if (val === 'SWIGGY-8921') {
      await input.click({ clickCount: 3 });
      await input.type('ZOM-4412');
      break;
    }
  }
  await new Promise(r => setTimeout(r, 600));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'DLV-002-zomato-order-entry.png') });
  console.log('DLV-002 captured');

  // 3. DLV-003: Orders Queue showing Delivery orders with badges
  const navButtons = await page.$$('button');
  for (const b of navButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes("Today's Tokens")) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'DLV-003-marketplace-queue.png') });
  console.log('DLV-003 captured');

  // 4. DLV-004: Marketplace Payouts & Commission Reconciliation Tab
  for (const b of navButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Marketplace Payouts')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'DLV-004-payout-reconciliation.png') });
  console.log('DLV-004 captured');

  // 5. INV-009: Inventory Transfers Tab with Shortage Audit Dialog
  await page.goto(BASE_URL + '/inventory?tab=transfers', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1200));

  const invButtons = await page.$$('button');
  for (const b of invButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Shortage Audit')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'INV-009-transfer-shortage-correction.png') });
  console.log('INV-009 captured');

  // Final step: Navigate back to Parcel Counter and leave the browser open per instructions:
  // "Leave the verified local services running and the browser open on Parcel Counter."
  await page.goto(BASE_URL + '/admin?tab=parcel', { waitUntil: 'networkidle2' });
  console.log('Screenshots captured and browser left open on Parcel Counter.');
})().catch(err => {
  console.error('Screenshot capture script error:', err);
});
