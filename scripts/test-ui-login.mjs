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

async function testBrowserLogin() {
  console.log('Launching browser to perform UI login...');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=1400,900', '--ignore-certificate-errors'],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });

    page.on('console', msg => console.log(`[Browser Console] ${msg.type()}: ${msg.text()}`));
    page.on('pageerror', err => console.log(`[Browser PageError] ${err.toString()}`));

    // Navigate to landing
    await page.goto(PROD_FRONTEND, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 1000));

    // Click "Staff sign in" button in header
    console.log('Clicking Staff sign in button...');
    const staffSignInBtn = await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button, a'));
      const target = btns.find(b => b.textContent && b.textContent.includes('Staff sign in'));
      if (target) {
        target.click();
        return true;
      }
      return false;
    });
    console.log('Clicked Staff sign in:', staffSignInBtn);
    await new Promise(r => setTimeout(r, 1500));

    // Type credentials
    console.log('Typing credentials...');
    const emailInput = await page.waitForSelector('input[type="email"], input[name="email"]', { timeout: 5000 });
    await emailInput.type('qa-admin@test.tablo.local');

    const passInput = await page.waitForSelector('input[type="password"], input[name="password"]', { timeout: 5000 });
    await passInput.type('TestPass123!');

    // Screenshot before click
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-010-login-typed.png') });
    copyToArtifacts('PROD-010-login-typed.png');

    // Click Sign in button
    console.log('Clicking Sign in button...');
    await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      const submit = btns.find(b => b.textContent && b.textContent.includes('Sign in'));
      if (submit) submit.click();
    });

    // Wait for response/navigation
    console.log('Waiting 5 seconds post-login...');
    await new Promise(r => setTimeout(r, 5000));
    console.log('Current URL post-login:', page.url());

    const postLoginShot = path.join(screenshotDir, 'PROD-011-post-login.png');
    await page.screenshot({ path: postLoginShot, fullPage: true });
    copyToArtifacts('PROD-011-post-login.png');

    // Check localStorage / tokens
    const storage = await page.evaluate(() => {
      const res = {};
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        res[k] = localStorage.getItem(k);
      }
      return res;
    });
    console.log('LocalStorage keys post-login:', Object.keys(storage));

    // Navigate to /admin
    console.log('Navigating to /admin...');
    await page.goto(`${PROD_FRONTEND}/admin`, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 3000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-012-admin-view.png'), fullPage: true });
    copyToArtifacts('PROD-012-admin-view.png');

    // Navigate to /staff
    console.log('Navigating to /staff...');
    await page.goto(`${PROD_FRONTEND}/staff`, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 3000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-013-staff-view.png'), fullPage: true });
    copyToArtifacts('PROD-013-staff-view.png');

    // Navigate to /dashboard
    console.log('Navigating to /dashboard...');
    await page.goto(`${PROD_FRONTEND}/dashboard`, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 3000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-014-dashboard-view.png'), fullPage: true });
    copyToArtifacts('PROD-014-dashboard-view.png');

  } finally {
    await browser.close();
  }
}

testBrowserLogin().catch(err => {
  console.error('Browser login test failed:', err);
  process.exit(1);
});
