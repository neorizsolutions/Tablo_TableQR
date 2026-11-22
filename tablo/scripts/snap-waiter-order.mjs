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

    // Login via API and set token directly for reliability
    const loginRes = await fetch('http://127.0.0.1:5166/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'waiter@tablodemo.com', password: 'password123' })
    });
    const authData = await loginRes.json();
    console.log('Login successful for:', authData.user.email);

    await page.goto('http://127.0.0.1:5173/auth', { waitUntil: 'networkidle0' });
    await page.evaluate((auth) => {
      localStorage.setItem('tablo_token', auth.token);
      localStorage.setItem('tablo_user', JSON.stringify(auth.user));
    }, authData);

    // Navigate to Staff Order Taking
    await page.goto('http://127.0.0.1:5173/dashboard?tab=order', { waitUntil: 'networkidle0' });
    await new Promise((r) => setTimeout(r, 1500));
    await page.screenshot({ path: 'qa/tablo-e2e/screenshots/WTR-001-waiter-order-taking.png' });
    console.log('Captured WTR-001-waiter-order-taking.png');

    // Click Table 1
    const buttons = await page.$$('button');
    let clicked = false;
    for (const btn of buttons) {
      const text = await page.evaluate((el) => el.innerText, btn);
      if (text && text.includes('Table 1') && !text.includes('Table 10')) {
        await btn.click();
        clicked = true;
        break;
      }
    }

    if (clicked) {
      await new Promise((r) => setTimeout(r, 1500));
      await page.screenshot({ path: 'qa/tablo-e2e/screenshots/WTR-004-order-taking-selected-table.png' });
      console.log('Captured WTR-004-order-taking-selected-table.png');
    }
  } finally {
    await browser.close();
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
