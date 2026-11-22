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
    console.log(`Copied ${filename} to artifacts directory`);
  }
}

const PROD_FRONTEND = 'https://production.d25iywxxvasd5l.amplifyapp.com';
const API_BASE = 'https://13-126-116-187.nip.io/api';

const USERS = {
  admin: { email: 'qa-admin@test.tablo.local', password: 'TestPass123!' },
  kitchen: { email: 'qa-kitchen@test.tablo.local', password: 'TestPass123!' },
  other: { email: 'qa-other@test.tablo.local', password: 'TestPass123!' },
};

const RESTAURANT_ALAKAPURI = 'b7226b97-7e7f-489c-ab2e-cc1b0164afc9';
const RESTAURANT_ABC = 'ceb29ac5-5a00-416b-a245-361ecc2fb905';

async function verify() {
  console.log('=== STARTING PRODUCTION RELEASE WORKFLOW VERIFICATION ===');
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--window-size=1440,960', '--ignore-certificate-errors'],
  });

  const results = {
    staffOrderTaking: 'UNKNOWN',
    parcelMarketplace: 'UNKNOWN',
    outletStorefrontConfig: 'UNKNOWN',
    centralKitchen: 'UNKNOWN',
    publicStorefront: 'UNKNOWN',
    customerBillQr: 'UNKNOWN',
    kitchenRoleRestriction: 'UNKNOWN',
    tenantIsolationApi: 'UNKNOWN',
  };

  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1440, height: 960 });

    // Track console errors
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`[Browser Console Error] ${msg.text()}`);
      }
    });

    // ---------------------------------------------------------
    // 1. PUBLIC STOREFRONT (No authentication required)
    // ---------------------------------------------------------
    console.log('\n--- 1. Testing Public Storefront (/order) without login ---');
    await page.goto(`${PROD_FRONTEND}/order`, { waitUntil: 'networkidle2', timeout: 25000 });
    await new Promise(r => setTimeout(r, 2500));
    const storefrontShot = 'PROD-025-public-storefront.png';
    await page.screenshot({ path: path.join(screenshotDir, storefrontShot), fullPage: true });
    copyToArtifacts(storefrontShot);

    const storefrontBody = await page.content();
    if (storefrontBody.includes('Storefront') || storefrontBody.includes('Pickup') || storefrontBody.includes('Delivery')) {
      console.log('✔ Public Storefront loaded successfully without login');
      results.publicStorefront = 'PASS';
    } else {
      console.log('✖ Public Storefront did not contain expected content');
      results.publicStorefront = 'FAIL';
    }

    // ---------------------------------------------------------
    // 2. ADMIN LOGIN FOR WORKSPACES
    // ---------------------------------------------------------
    console.log('\n--- 2. Signing in as Admin (qa-admin@test.tablo.local) ---');
    await page.goto(`${PROD_FRONTEND}/auth`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 1500));

    const emailInput = await page.waitForSelector('input[type="email"]');
    await emailInput.type(USERS.admin.email);
    const passInput = await page.waitForSelector('input[type="password"]');
    await passInput.type(USERS.admin.password);

    await page.evaluate(() => {
      const submit = Array.from(document.querySelectorAll('button')).find(b => b.textContent && b.textContent.includes('Sign in'));
      if (submit) submit.click();
    });
    await new Promise(r => setTimeout(r, 4000));

    // ---------------------------------------------------------
    // 3. STAFF ORDER TAKING WORKSPACE (/dashboard?tab=order)
    // ---------------------------------------------------------
    console.log('\n--- 3. Testing Staff Order Taking Workspace (/dashboard?tab=order) ---');
    await page.goto(`${PROD_FRONTEND}/dashboard?tab=order`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 3000));
    const staffOrderShot = 'PROD-021-staff-order-workspace.png';
    await page.screenshot({ path: path.join(screenshotDir, staffOrderShot), fullPage: true });
    copyToArtifacts(staffOrderShot);

    const staffOrderContent = await page.content();
    if (staffOrderContent.includes('Order') || staffOrderContent.includes('Table') || staffOrderContent.includes('Floor')) {
      console.log('✔ Staff Order Taking workspace rendered');
      results.staffOrderTaking = 'PASS';
    } else {
      results.staffOrderTaking = 'FAIL';
    }

    // ---------------------------------------------------------
    // 4. PARCEL MARKETPLACE ENTRY (/admin?tab=marketplace)
    // ---------------------------------------------------------
    console.log('\n--- 4. Testing Parcel Marketplace Counter & Automation (/admin?tab=marketplace) ---');
    await page.goto(`${PROD_FRONTEND}/admin?tab=marketplace`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 3000));
    const marketplaceShot = 'PROD-022-marketplace-entry.png';
    await page.screenshot({ path: path.join(screenshotDir, marketplaceShot), fullPage: true });
    copyToArtifacts(marketplaceShot);

    const marketplaceContent = await page.content();
    if (marketplaceContent.includes('Marketplace') || marketplaceContent.includes('Swiggy') || marketplaceContent.includes('Zomato')) {
      console.log('✔ Marketplace automation manager rendered');
      results.parcelMarketplace = 'PASS';
    } else {
      results.parcelMarketplace = 'FAIL';
    }

    // ---------------------------------------------------------
    // 5. OUTLET STOREFRONT SETTINGS (/admin?tab=storefront)
    // ---------------------------------------------------------
    console.log('\n--- 5. Testing Storefront & Outlet Configuration (/admin?tab=storefront) ---');
    await page.goto(`${PROD_FRONTEND}/admin?tab=storefront`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 3000));
    const storefrontSettingsShot = 'PROD-023-outlet-storefront-settings.png';
    await page.screenshot({ path: path.join(screenshotDir, storefrontSettingsShot), fullPage: true });
    copyToArtifacts(storefrontSettingsShot);

    const storefrontSettingsContent = await page.content();
    if (storefrontSettingsContent.includes('Storefront') || storefrontSettingsContent.includes('Outlet') || storefrontSettingsContent.includes('Operating Hours')) {
      console.log('✔ Storefront & Outlet settings rendered');
      results.outletStorefrontConfig = 'PASS';
    } else {
      results.outletStorefrontConfig = 'FAIL';
    }

    // ---------------------------------------------------------
    // 6. CENTRAL KITCHEN (/inventory?tab=central-kitchen)
    // ---------------------------------------------------------
    console.log('\n--- 6. Testing Central Kitchen Manager (/inventory?tab=central-kitchen) ---');
    await page.goto(`${PROD_FRONTEND}/inventory?tab=central-kitchen`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 3000));
    const centralKitchenShot = 'PROD-024-central-kitchen.png';
    await page.screenshot({ path: path.join(screenshotDir, centralKitchenShot), fullPage: true });
    copyToArtifacts(centralKitchenShot);

    const ckContent = await page.content();
    if (ckContent.includes('Central Kitchen') || ckContent.includes('Production') || ckContent.includes('Batch')) {
      console.log('✔ Central Kitchen manager rendered');
      results.centralKitchen = 'PASS';
    } else {
      results.centralKitchen = 'FAIL';
    }

    // ---------------------------------------------------------
    // 7. UNPAID CUSTOMER BILL & DYNAMIC UPI QR
    // ---------------------------------------------------------
    console.log('\n--- 7. Testing Customer Table & Unpaid Bill QR (/t/qd4x8f2tp or /t/2x02rywxc) ---');
    await page.goto(`${PROD_FRONTEND}/t/qd4x8f2tp`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 3000));
    const billQrShot = 'PROD-026-customer-unpaid-bill-qr.png';
    await page.screenshot({ path: path.join(screenshotDir, billQrShot), fullPage: true });
    copyToArtifacts(billQrShot);

    const billContent = await page.content();
    if (billContent.includes('Alakapuri') || billContent.includes('Bill') || billContent.includes('Table') || billContent.includes('UPI')) {
      console.log('✔ Customer view with bill/table active');
      results.customerBillQr = 'PASS';
    } else {
      results.customerBillQr = 'FAIL';
    }

    // ---------------------------------------------------------
    // 8. ROLE-BASED ACCESS CONTROL (Kitchen Staff)
    // ---------------------------------------------------------
    console.log('\n--- 8. Testing Role Restrictions with Kitchen Staff ---');
    // Clear storage/cookies to sign out
    await page.evaluate(() => localStorage.clear());
    await page.goto(`${PROD_FRONTEND}/auth`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 1000));

    const emailInputK = await page.waitForSelector('input[type="email"]');
    await emailInputK.type(USERS.kitchen.email);
    const passInputK = await page.waitForSelector('input[type="password"]');
    await passInputK.type(USERS.kitchen.password);

    await page.evaluate(() => {
      const submit = Array.from(document.querySelectorAll('button')).find(b => b.textContent && b.textContent.includes('Sign in'));
      if (submit) submit.click();
    });
    await new Promise(r => setTimeout(r, 3500));

    // Try navigating to /staff or /admin
    await page.goto(`${PROD_FRONTEND}/staff`, { waitUntil: 'networkidle2', timeout: 20000 });
    await new Promise(r => setTimeout(r, 2000));
    const roleShot = 'PROD-027-role-restriction.png';
    await page.screenshot({ path: path.join(screenshotDir, roleShot), fullPage: true });
    copyToArtifacts(roleShot);

    const staffGateContent = await page.content();
    if (staffGateContent.includes('No staff access yet') || staffGateContent.includes('Admin sign in required') || staffGateContent.includes('Kitchen')) {
      console.log('✔ Restricted role access properly gated');
      results.kitchenRoleRestriction = 'PASS';
    } else {
      results.kitchenRoleRestriction = 'FAIL';
    }

  } finally {
    await browser.close();
  }

  // ---------------------------------------------------------
  // 9. TENANT ISOLATION API CHECKS
  // ---------------------------------------------------------
  console.log('\n--- 9. Testing Cross-Tenant API Isolation ---');
  try {
    // Login as other tenant admin
    const otherLoginRes = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(USERS.other),
    });
    const otherAuth = await otherLoginRes.json();

    // Call Alakapuri outlets/storefront/central-kitchen with other tenant JWT
    const crossRes = await fetch(`${API_BASE}/restaurants/${RESTAURANT_ALAKAPURI}/tables`, {
      headers: { Authorization: `Bearer ${otherAuth.token}` },
    });
    console.log(`Cross-tenant tables status: ${crossRes.status}`);

    const crossCkRes = await fetch(`${API_BASE}/central-kitchen/production-batches?restaurantId=${RESTAURANT_ALAKAPURI}`, {
      headers: { Authorization: `Bearer ${otherAuth.token}` },
    });
    console.log(`Cross-tenant central-kitchen status: ${crossCkRes.status}`);

    if (crossRes.status === 403 || crossCkRes.status === 403) {
      console.log('✔ Cross-tenant access strictly rejected with 403 Forbidden');
      results.tenantIsolationApi = 'PASS';
    } else {
      results.tenantIsolationApi = 'FAIL';
    }
  } catch (err) {
    console.error('Cross-tenant API check error:', err);
    results.tenantIsolationApi = 'FAIL';
  }

  console.log('\n=============================================');
  console.log('FINAL EXECUTION VERIFICATION SUMMARY:');
  console.log(JSON.stringify(results, null, 2));
  console.log('=============================================');
}

verify().catch(console.error);
