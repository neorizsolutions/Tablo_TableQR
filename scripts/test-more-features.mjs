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

const PROD_FRONTEND = 'https://production.d25iywxxvasd5l.amplifyapp.com';

async function testMoreFeatures() {
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=1400,900', '--ignore-certificate-errors'],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });

    // 1. Sign in as Admin to check Reports
    console.log('1. Signing in as Admin to capture Reports...');
    await page.goto(PROD_FRONTEND, { waitUntil: 'networkidle2' });
    
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button, a'));
      const target = btns.find(b => b.textContent && b.textContent.includes('Staff sign in'));
      if (target) target.click();
    });
    await new Promise(r => setTimeout(r, 1000));

    const emailInput = await page.waitForSelector('input[type="email"]');
    await emailInput.type('qa-admin@test.tablo.local');
    const passInput = await page.waitForSelector('input[type="password"]');
    await passInput.type('TestPass123!');
    
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      const submit = btns.find(b => b.textContent && b.textContent.includes('Sign in'));
      if (submit) submit.click();
    });
    await new Promise(r => setTimeout(r, 4000));

    // Click Reports tab
    console.log('Clicking Reports tab...');
    await page.evaluate(() => {
      const tabs = Array.from(document.querySelectorAll('a, button'));
      const repTab = tabs.find(t => t.textContent && t.textContent.trim() === 'Reports');
      if (repTab) repTab.click();
    });
    await new Promise(r => setTimeout(r, 3000));

    const reportsShot = path.join(screenshotDir, 'PROD-015-reports-view.png');
    await page.screenshot({ path: reportsShot, fullPage: true });
    copyToArtifacts('PROD-015-reports-view.png');
    console.log('Saved PROD-015-reports-view.png');

    // Sign out
    console.log('Signing out...');
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      const signOut = btns.find(b => b.textContent && b.textContent.includes('Sign out'));
      if (signOut) signOut.click();
    });
    await new Promise(r => setTimeout(r, 2000));

    // 2. Sign in as Kitchen Staff
    console.log('2. Signing in as Kitchen Staff (qa-kitchen@test.tablo.local)...');
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button, a'));
      const target = btns.find(b => b.textContent && b.textContent.includes('Staff sign in'));
      if (target) target.click();
    });
    await new Promise(r => setTimeout(r, 1000));

    const emailInput2 = await page.waitForSelector('input[type="email"]');
    await emailInput2.type('qa-kitchen@test.tablo.local');
    const passInput2 = await page.waitForSelector('input[type="password"]');
    await passInput2.type('TestPass123!');

    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      const submit = btns.find(b => b.textContent && b.textContent.includes('Sign in'));
      if (submit) submit.click();
    });
    await new Promise(r => setTimeout(r, 4000));

    const kitchenShot = path.join(screenshotDir, 'PROD-016-kitchen-role-login.png');
    await page.screenshot({ path: kitchenShot, fullPage: true });
    copyToArtifacts('PROD-016-kitchen-role-login.png');
    console.log('Saved PROD-016-kitchen-role-login.png');

    // 3. Inspect Customer QR View
    console.log('3. Inspecting Customer Dine-in Table view on table 1...');
    // In Alakapuri, Table 1 has QR token or id
    // Let's get QR token from API
    const tokenRes = await fetch('https://13-126-116-187.nip.io/api/restaurants/b7226b97-7e7f-489c-ab2e-cc1b0164afc9/tables', {
      headers: {
        'Authorization': `Bearer ${await page.evaluate(() => localStorage.getItem('tablo_token'))}`
      }
    }).catch(() => null);

  } finally {
    await browser.close();
  }
}

testMoreFeatures().catch(console.error);
