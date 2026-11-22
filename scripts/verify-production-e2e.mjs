import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const puppeteer = require('D:/Projects/tablo/tablo/node_modules/puppeteer');
import path from 'node:path';
import fs from 'node:fs';

const screenshotDir = 'D:/Projects/tablo/qa/production-readiness/screenshots';
const artifactDir = 'C:/Users/neonk/.gemini/antigravity/brain/03dd655e-9ce5-4ee1-b6ec-1fa58e96ee0a';

if (!fs.existsSync(screenshotDir)) {
  fs.mkdirSync(screenshotDir, { recursive: true });
}

function copyToArtifacts(filename) {
  const src = path.join(screenshotDir, filename);
  const dest = path.join(artifactDir, filename);
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, dest);
  }
}

const API_BASE = 'https://13-126-116-187.nip.io/api';
const PROD_FRONTEND = 'https://production.d25iywxxvasd5l.amplifyapp.com';

const RESTAURANT_ALAKAPURI = 'b7226b97-7e7f-489c-ab2e-cc1b0164afc9';
const RESTAURANT_ABC = 'ceb29ac5-5a00-416b-a245-361ecc2fb905';

const USERS = {
  admin: { email: 'qa-admin@test.tablo.local', password: 'TestPass123!' },
  waiter: { email: 'qa-waiter@test.tablo.local', password: 'TestPass123!' },
  kitchen: { email: 'qa-kitchen@test.tablo.local', password: 'TestPass123!' },
  other: { email: 'qa-other@test.tablo.local', password: 'TestPass123!' },
};

async function loginApi(email, password) {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Login failed for ${email} (${res.status}): ${text}`);
  }
  return await res.json();
}

async function runApiChecks() {
  console.log('\n=== RUNNING PRODUCTION API CHECKS ===');
  
  // 1. Admin Login
  console.log('1. Testing Admin Login...');
  const adminAuth = await loginApi(USERS.admin.email, USERS.admin.password);
  console.log('   Admin Login SUCCESS. Role:', adminAuth.user.role, 'RestId:', adminAuth.user.restaurantId);

  // 2. Kitchen Login
  console.log('2. Testing Kitchen Login...');
  const kitchenAuth = await loginApi(USERS.kitchen.email, USERS.kitchen.password);
  console.log('   Kitchen Login SUCCESS. Role:', kitchenAuth.user.role, 'RestId:', kitchenAuth.user.restaurantId);

  // 3. Cross-Tenant User Login
  console.log('3. Testing Cross-Tenant User Login...');
  const otherAuth = await loginApi(USERS.other.email, USERS.other.password);
  console.log('   Other Login SUCCESS. Role:', otherAuth.user.role, 'RestId:', otherAuth.user.restaurantId);

  // 4. Role Restriction: Kitchen calling Admin Reports
  console.log('4. Testing Role Restriction (Kitchen calling Admin Reports)...');
  const kitchenReportRes = await fetch(`${API_BASE}/reports/summary`, {
    headers: { Authorization: `Bearer ${kitchenAuth.token}` },
  });
  console.log(`   Kitchen calling /api/reports/summary HTTP status: ${kitchenReportRes.status}`);
  const roleRestricted = kitchenReportRes.status === 403;
  console.log('   Role Restriction check:', roleRestricted ? 'PASS (403 Forbidden)' : `FAIL (${kitchenReportRes.status})`);

  // 5. Cross-Tenant Rejection: Other Tenant Admin calling Alakapuri Tables
  console.log('5. Testing Cross-Tenant Isolation (Other Tenant calling Alakapuri tables)...');
  const crossTenantRes = await fetch(`${API_BASE}/restaurants/${RESTAURANT_ALAKAPURI}/tables`, {
    headers: { Authorization: `Bearer ${otherAuth.token}` },
  });
  console.log(`   Other tenant calling Alakapuri tables HTTP status: ${crossTenantRes.status}`);
  // If endpoint validates restaurantId claim against route or returns 403
  console.log('   Cross-tenant status code:', crossTenantRes.status);

  // 6. Admin Data Queries
  console.log('6. Testing Admin Queries for Alakapuri...');
  const tablesRes = await fetch(`${API_BASE}/restaurants/${RESTAURANT_ALAKAPURI}/tables`, {
    headers: { Authorization: `Bearer ${adminAuth.token}` },
  });
  console.log('   Admin tables query status:', tablesRes.status);
  const tablesData = await tablesRes.json().catch(() => null);
  console.log('   Tables count:', Array.isArray(tablesData) ? tablesData.length : 'N/A');

  const settingsRes = await fetch(`${API_BASE}/settings`, {
    headers: { Authorization: `Bearer ${adminAuth.token}` },
  });
  console.log('   Admin settings query status:', settingsRes.status);

  return {
    adminAuth,
    kitchenAuth,
    otherAuth,
    roleRestricted,
    crossTenantStatus: crossTenantRes.status,
    tablesCount: Array.isArray(tablesData) ? tablesData.length : 0,
  };
}

async function runBrowserChecks() {
  console.log('\n=== RUNNING PRODUCTION BROWSER CHECKS ===');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=1400,900', '--ignore-certificate-errors'],
  });

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });

    // Capture console errors
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`[Browser Console Error] ${msg.text()}`);
      }
    });

    // 1. Initial Landing Page
    console.log('1. Navigating to Production Frontend:', PROD_FRONTEND);
    await page.goto(PROD_FRONTEND, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 2000));
    const landingShot = path.join(screenshotDir, 'PROD-001-landing.png');
    await page.screenshot({ path: landingShot, fullPage: true });
    copyToArtifacts('PROD-001-landing.png');
    console.log('   Saved PROD-001-landing.png');

    // 2. Perform Login as Admin
    console.log('2. Attempting Admin Login on Amplify...');
    const emailInput = await page.$('input[type="email"], input[name="email"]');
    const passInput = await page.$('input[type="password"], input[name="password"]');
    const submitBtn = await page.$('button[type="submit"]');

    if (emailInput && passInput && submitBtn) {
      await emailInput.type(USERS.admin.email);
      await passInput.type(USERS.admin.password);
      await submitBtn.click();
      console.log('   Credentials submitted, waiting for navigation...');
      await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
      await new Promise(r => setTimeout(r, 3000));

      const adminShot = path.join(screenshotDir, 'PROD-002-admin-view.png');
      await page.screenshot({ path: adminShot, fullPage: true });
      copyToArtifacts('PROD-002-admin-view.png');
      console.log('   Saved PROD-002-admin-view.png (Current URL:', page.url(), ')');
    } else {
      console.log('   Login form inputs not detected on initial landing page. Current URL:', page.url());
      // Check if there is a login link or button
      const loginLink = await page.$('a[href*="login"], a[href*="auth"], button');
      if (loginLink) {
        console.log('   Clicking available login button/link...');
        await loginLink.click().catch(() => {});
        await new Promise(r => setTimeout(r, 2000));
        const postClickShot = path.join(screenshotDir, 'PROD-002-after-click.png');
        await page.screenshot({ path: postClickShot, fullPage: true });
        copyToArtifacts('PROD-002-after-click.png');
      }
    }

    // 3. Inspect Routes & Features on Deployed Amplify Frontend
    console.log('3. Inspecting /dashboard route...');
    await page.goto(`${PROD_FRONTEND}/dashboard`, { waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-003-dashboard.png'), fullPage: true });
    copyToArtifacts('PROD-003-dashboard.png');

    console.log('4. Inspecting /staff route...');
    await page.goto(`${PROD_FRONTEND}/staff`, { waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-004-staff.png'), fullPage: true });
    copyToArtifacts('PROD-004-staff.png');

    console.log('5. Inspecting /kitchen or kitchen view...');
    await page.goto(`${PROD_FRONTEND}/kitchen`, { waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-005-kitchen.png'), fullPage: true });
    copyToArtifacts('PROD-005-kitchen.png');

    console.log('6. Inspecting /admin route...');
    await page.goto(`${PROD_FRONTEND}/admin`, { waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-006-admin.png'), fullPage: true });
    copyToArtifacts('PROD-006-admin.png');

    console.log('7. Inspecting Customer QR Bill / table view...');
    // Table 1 for Alakapuri: let's query first table ID
    await page.goto(`${PROD_FRONTEND}/t/demo-table`, { waitUntil: 'networkidle2', timeout: 15000 }).catch(() => {});
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotDir, 'PROD-007-table-qr.png'), fullPage: true });
    copyToArtifacts('PROD-007-table-qr.png');

  } finally {
    await browser.close();
  }
}

async function main() {
  try {
    const apiResults = await runApiChecks();
    console.log('\nAPI Results Summary:', apiResults);
    await runBrowserChecks();
    console.log('\nAll Production Verification checks completed!');
  } catch (err) {
    console.error('Error during verification:', err);
    process.exit(1);
  }
}

main();
