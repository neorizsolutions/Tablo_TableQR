import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const puppeteer = require('D:/Projects/tablo/tablo/node_modules/puppeteer');
import path from 'node:path';
import fs from 'node:fs';

const screenshotDir = 'D:/Projects/tablo/qa/production-readiness/screenshots';
const artifactDir = 'C:/Users/neonk/.gemini/antigravity/brain/03dd655e-9ce5-4ee1-b6ec-1fa58e96ee0a';

function copyToArtifacts(filename) {
  const src = path.join(screenshotDir, filename);
  const dest = path.join(artifactDir, filename);
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, dest);
  }
}

const CUSTOMER_TABLE_URL = 'https://production.d25iywxxvasd5l.amplifyapp.com/t/2x02rywxc';

async function testCustomerTable() {
  console.log('Testing customer dine-in table 3 at', CUSTOMER_TABLE_URL);
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=430,932'], // Mobile viewport
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 430, height: 932, isMobile: true });

    page.on('console', msg => console.log(`[Customer Console] ${msg.type()}: ${msg.text()}`));

    await page.goto(CUSTOMER_TABLE_URL, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 3000));

    const shot1 = path.join(screenshotDir, 'PROD-017-customer-menu-table3.png');
    await page.screenshot({ path: shot1, fullPage: true });
    copyToArtifacts('PROD-017-customer-menu-table3.png');
    console.log('Saved PROD-017-customer-menu-table3.png');

    // Check if there is an active order or bill tab
    const billBtn = await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button, a'));
      const target = btns.find(b => b.textContent && (b.textContent.includes('Bill') || b.textContent.includes('Order') || b.textContent.includes('Track')));
      if (target) {
        target.click();
        return target.textContent;
      }
      return null;
    });
    console.log('Found order/bill button:', billBtn);
    await new Promise(r => setTimeout(r, 2000));

    const shot2 = path.join(screenshotDir, 'PROD-018-customer-bill-or-tracking.png');
    await page.screenshot({ path: shot2, fullPage: true });
    copyToArtifacts('PROD-018-customer-bill-or-tracking.png');
    console.log('Saved PROD-018-customer-bill-or-tracking.png');

  } finally {
    await browser.close();
  }
}

testCustomerTable().catch(console.error);
