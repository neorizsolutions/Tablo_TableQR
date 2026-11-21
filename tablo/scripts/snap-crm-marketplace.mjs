import puppeteer from 'puppeteer';
import path from 'node:path';
import fs from 'node:fs';

const SCREENSHOT_DIR = path.resolve('D:/Projects/tablo/qa/tablo-e2e/screenshots');
const BASE_URL = 'http://127.0.0.1:5173';

(async () => {
  console.log('[Snapshot] Launching Chrome...');
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: false,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu', '--window-size=1440,900']
  });

  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });

  // Mock demo admin auth credentials in localStorage
  await page.goto(BASE_URL + '/');
  await page.evaluate(() => {
    const mockUser = {
      id: '11111111-0000-0000-0000-000000000001',
      restaurantId: '22222222-0000-0000-0000-000000000001',
      restaurantName: 'Tablo Heritage Restaurant',
      email: 'admin@tablodemo.com',
      role: 'Admin',
      roles: ['Admin', 'Manager'],
      entitlements: ['standard_billing', 'staff_ordering', 'dynamic_upi_qr', 'crm_loyalty', 'marketplace_automation']
    };
    localStorage.setItem('tablo_token', 'demo-admin-jwt-token-2026');
    localStorage.setItem('tablo_user', JSON.stringify(mockUser));
  });

  // 1. CRM-001: Customer Directory & Loyalty Ledger
  console.log('[Snapshot] Capturing CRM & Loyalty Directory...');
  await page.goto(BASE_URL + '/admin?tab=crm', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1500));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'CRM-001-customer-directory-loyalty.png') });

  // 2. CRM-002: Guest Feedback Tab
  console.log('[Snapshot] Capturing Guest Feedback...');
  const buttons = await page.$$('button');
  for (const b of buttons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Guest Feedback')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'CRM-002-guest-feedback.png') });

  // 3. MKT-001: Online Marketplace Manager Tab
  console.log('[Snapshot] Capturing Marketplace Outlets...');
  await page.goto(BASE_URL + '/admin?tab=marketplace', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1500));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'MKT-001-marketplace-stores-outlets.png') });

  // 4. MKT-002: Inbound Webhook Simulator & Deduplication
  console.log('[Snapshot] Capturing Webhook Simulator...');
  const mktButtons = await page.$$('button');
  for (const b of mktButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Inbound Simulator')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1000));

  // Click Simulate Inbound Order to show live result banner
  const simButtons = await page.$$('button');
  for (const b of simButtons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Simulate Inbound Order')) {
      await b.click();
      break;
    }
  }
  await new Promise(r => setTimeout(r, 1200));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'MKT-002-webhook-deduplication.png') });

  console.log('[Snapshot] All screenshots captured successfully.');
  // Leave browser running on Marketplace Manager
})().catch(console.error);
