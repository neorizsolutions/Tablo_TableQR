import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const puppeteer = require('D:/Projects/tablo/tablo/node_modules/puppeteer');
import path from 'node:path';
import fs from 'node:fs';

const screenshotDir = 'D:/Projects/tablo/qa/tablo-e2e/screenshots';
const artifactDir = 'C:/Users/neonk/.gemini/antigravity/brain/03dd655e-9ce5-4ee1-b6ec-1fa58e96ee0a';

if (!fs.existsSync(screenshotDir)) {
  fs.mkdirSync(screenshotDir, { recursive: true });
}

async function copyToArtifacts(filename) {
  const src = path.join(screenshotDir, filename);
  const dest = path.join(artifactDir, filename);
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, dest);
  }
}

async function run() {
  console.log('Launching browser...');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=1400,900'],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });

    // 1. Capture Admin Storefront & Outlet Settings
    console.log('Navigating to login...');
    await page.goto('http://127.0.0.1:5173/auth', { waitUntil: 'networkidle0' });

    // Login as Admin
    await page.type('input[type="email"]', 'admin@tablodemo.com');
    await page.type('input[type="password"]', 'password123');
    await page.click('button[type="submit"]');

    console.log('Waiting for admin navigation...');
    await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 10000 }).catch(() => {});

    // Navigate to admin storefront tab
    console.log('Navigating to admin storefront settings...');
    await page.goto('http://127.0.0.1:5173/admin?tab=storefront', { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 2000));

    const admPath = path.join(screenshotDir, 'ADM-007-outlet-storefront-settings.png');
    await page.screenshot({ path: admPath, fullPage: true });
    console.log('Captured ADM-007-outlet-storefront-settings.png');
    await copyToArtifacts('ADM-007-outlet-storefront-settings.png');

    // 2. Capture Public Storefront Guest View
    console.log('Opening public storefront at /order...');
    const guestPage = await browser.newPage();
    await guestPage.setViewport({ width: 1400, height: 900 });
    await guestPage.goto('http://127.0.0.1:5173/order', { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 3000));

    const pubPath = path.join(screenshotDir, 'STR-003-storefront-outlet-hours-badge.png');
    await guestPage.screenshot({ path: pubPath, fullPage: true });
    console.log('Captured STR-003-storefront-outlet-hours-badge.png');
    await copyToArtifacts('STR-003-storefront-outlet-hours-badge.png');

    // 3. Place order and capture order confirmation / tracking link
    console.log('Interacting with storefront to place order...');
    // Add item to cart
    const addBtns = await guestPage.$$('button');
    for (const btn of addBtns) {
      const text = await guestPage.evaluate(el => el.textContent, btn);
      if (text && text.includes('Add')) {
        await btn.click();
        break;
      }
    }
    await new Promise(r => setTimeout(r, 1500));

    // Fill customer form
    const inputs = await guestPage.$$('input');
    for (const input of inputs) {
      const ph = await guestPage.evaluate(el => el.getAttribute('placeholder') || '', input);
      if (ph.toLowerCase().includes('name')) {
        await input.type('Kavita Sharma');
      } else if (ph.toLowerCase().includes('phone') || ph.toLowerCase().includes('mobile')) {
        await input.type('9811223344');
      }
    }

    // Click Place Order
    const placeBtns = await guestPage.$$('button');
    for (const btn of placeBtns) {
      const text = await guestPage.evaluate(el => el.textContent, btn);
      if (text && text.includes('Place Order')) {
        console.log('Clicking Place Order button...');
        await btn.click();
        await new Promise(r => setTimeout(r, 4000));
        break;
      }
    }

    const trkPath = path.join(screenshotDir, 'STR-004-storefront-order-tracking.png');
    await guestPage.screenshot({ path: trkPath, fullPage: true });
    console.log('Captured STR-004-storefront-order-tracking.png');
    await copyToArtifacts('STR-004-storefront-order-tracking.png');

  } catch (err) {
    console.error('Screenshot error:', err);
  } finally {
    await browser.close();
  }
}

run();
