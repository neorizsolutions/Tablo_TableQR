import puppeteer from 'puppeteer';

async function main() {
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });

    // Login via API to get auth token
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

    // 1. Central Kitchen & Transfers Screen
    await page.goto('http://127.0.0.1:5173/inventory?tab=central-kitchen', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1500));
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/CKM-001-central-kitchen-transfers.png' });
    console.log('Captured CKM-001-central-kitchen-transfers.png');

    // 2. Digital Storefront Screen
    await page.goto('http://127.0.0.1:5173/admin?tab=storefront', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1500));
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/STR-001-digital-storefront-checkout.png' });
    console.log('Captured STR-001-digital-storefront-checkout.png');

    // 3. Customer Unpaid Bill Preview with Dynamic UPI QR
    await page.goto('http://127.0.0.1:5173/dashboard?tab=floor', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1500));
    const buttons = await page.$$('button');
    for (const btn of buttons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && (text.includes('View customer bill') || text.includes('Customer bill'))) {
        await btn.click();
        await new Promise((r) => setTimeout(r, 1500));
        break;
      }
    }
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/PYM-001-customer-bill-dynamic-upi-qr.png' });
    console.log('Captured PYM-001-customer-bill-dynamic-upi-qr.png');
  } finally {
    await browser.close();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
