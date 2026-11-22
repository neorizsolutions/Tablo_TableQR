import puppeteer from 'puppeteer';
import path from 'node:path';

const SCREENSHOT_DIR = path.resolve('D:/Projects/tablo/qa/tablo-e2e/screenshots');
const BASE_URL = 'http://127.0.0.1:5173';
const API_URL = 'http://127.0.0.1:5166/api';

(async () => {
  const adminRes = await fetch(API_URL + '/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'admin@tablodemo.com', password: 'password123' })
  });
  const adminAuth = await adminRes.json();

  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });

  await page.goto(BASE_URL + '/auth');
  await page.evaluate((tok, usr) => {
    localStorage.setItem('tablo_token', tok);
    localStorage.setItem('tablo_user', JSON.stringify(usr));
  }, adminAuth.token, adminAuth.user);

  // 1. Interactive Floor with Settled Review
  await page.goto(BASE_URL + '/dashboard?tab=floor', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1000));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'FLR-001-table-1-settled-review.png') });
  console.log('FLR-001 captured');

  // 2. Interactive Staff Order Taking with Preselected Table 1
  await page.goto(BASE_URL + '/dashboard?tab=order&table=77777777-0000-0000-0000-000000000001', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1500));
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'WTR-004-order-taking-selected-table.png') });
  console.log('WTR-004 captured');

  // 3. Printer & Stations Modal from Kitchen
  await page.goto(BASE_URL + '/dashboard?tab=kitchen', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 1200));
  const buttons = await page.$$('button');
  for (const b of buttons) {
    const text = await page.evaluate(el => el.textContent, b);
    if (text && text.includes('Stations')) {
      await b.click();
      await new Promise(r => setTimeout(r, 800));
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'PRN-001-kitchen-station-printers.png') });
      console.log('PRN-001 captured');
      break;
    }
  }

  await browser.close();
  console.log('Interaction states captured successfully');
})().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});
