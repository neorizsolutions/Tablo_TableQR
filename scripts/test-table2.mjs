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

const TABLE2_URL = 'https://production.d25iywxxvasd5l.amplifyapp.com/t/qd4x8f2tp';

async function testTable2() {
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=430,932'],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 430, height: 932, isMobile: true });

    await page.goto(TABLE2_URL, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 3000));

    const shot1 = path.join(screenshotDir, 'PROD-019-table2-bill-request.png');
    await page.screenshot({ path: shot1, fullPage: true });
    copyToArtifacts('PROD-019-table2-bill-request.png');

    // Check if there is a track or bill button
    const trackBtn = await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button, a'));
      const target = btns.find(b => b.textContent && (b.textContent.includes('Bill') || b.textContent.includes('Order') || b.textContent.includes('Track')));
      if (target) {
        target.click();
        return target.textContent;
      }
      return null;
    });
    console.log('Clicked:', trackBtn);
    await new Promise(r => setTimeout(r, 2000));

    const shot2 = path.join(screenshotDir, 'PROD-020-table2-bill-screen.png');
    await page.screenshot({ path: shot2, fullPage: true });
    copyToArtifacts('PROD-020-table2-bill-screen.png');

  } finally {
    await browser.close();
  }
}

testTable2().catch(console.error);
