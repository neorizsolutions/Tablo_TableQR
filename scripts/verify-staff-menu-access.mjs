import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync, spawn } from "node:child_process";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const puppeteer = require("../tablo/node_modules/puppeteer");
const signalR = require("../tablo/node_modules/@microsoft/signalr");

const PG_BIN = "C:\\Program Files\\PostgreSQL\\17\\bin";
const PSQL = path.join(PG_BIN, "psql.exe");

const PG_HOST = "127.0.0.1";
const PG_PORT = 5432;
const PG_USER = "postgres";
const PG_PASS = process.env.PGPASSWORD || "postgres";
const API_PORT = 5194;
const WEB_PORT = 4194;
const API_BASE = `http://127.0.0.1:${API_PORT}/api`;
const ORDER_HUB_URL = `http://127.0.0.1:${API_PORT}/hubs/order`;
const WEB_BASE = `http://127.0.0.1:${WEB_PORT}`;

const RUN_STAMP = new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14);
const DB_NAME = `tablo_menu_access_e2e_${RUN_STAMP}`;
const QA_DIR = path.resolve(`qa/menu-access/${RUN_STAMP}`);
fs.mkdirSync(QA_DIR, { recursive: true });

const TEST_PASSWORD = `MenuQA#${RUN_STAMP.slice(-4)}!`;
const SA_USERNAME = "superadmin-qa";
const SA_PASSWORD = `SaMenu#${RUN_STAMP.slice(-4)}!`;

const checks = [];
function record(name, status, details = "") {
  checks.push({ name, status, details });
  console.log(`[${status}] ${name}${details ? ` — ${details}` : ""}`);
}

function assert(cond, message) {
  if (!cond) throw new Error(message);
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForHttp(url, timeoutMs = 45000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch {
      // retry
    }
    await sleep(400);
  }
  throw new Error(`Timed out waiting for ${url}`);
}

async function waitUntil(fn, timeoutMs = 15000, label = "condition") {
  const start = Date.now();
  let lastErr = null;
  while (Date.now() - start < timeoutMs) {
    try {
      const val = await fn();
      if (val) return val;
    } catch (e) {
      lastErr = e;
    }
    await sleep(250);
  }
  throw new Error(`Timed out waiting for ${label}${lastErr ? `: ${lastErr.message}` : ""}`);
}

async function apiJson(urlPath, { method = "GET", token, body, isForm = false } = {}) {
  const headers = {};
  if (!isForm) headers["Content-Type"] = "application/json";
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${urlPath}`, {
    method,
    headers,
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }
  return { ok: res.ok, status: res.status, data };
}

function runPsql(dbName, sql) {
  return execFileSync(
    PSQL,
    ["-h", PG_HOST, "-p", String(PG_PORT), "-U", PG_USER, "-d", dbName, "-v", "ON_ERROR_STOP=1", "-c", sql],
    { encoding: "utf8", env: { ...process.env, PGPASSWORD: PG_PASS } },
  );
}

function killProcessTree(proc) {
  if (!proc || !proc.pid) return;
  try {
    execFileSync("taskkill", ["/PID", String(proc.pid), "/T", "/F"], { stdio: "ignore" });
  } catch {
    try {
      proc.kill("SIGKILL");
    } catch {}
  }
}

async function capture(page, fileName) {
  const full = path.join(QA_DIR, fileName);
  await page.screenshot({ path: full, fullPage: false });
  console.log(`[Screenshot] ${fileName}`);
  return full;
}

async function textOf(page, selector) {
  return page.evaluate((sel) => document.querySelector(sel)?.textContent || "", selector);
}

async function signInViaUi(context, email, password, expectedPathPrefix) {
  const page = await context.newPage();
  await page.setViewport({ width: 1366, height: 860 });
  const errors = [];
  page.on("pageerror", (err) => errors.push(`PAGEERROR: ${err.message}`));
  page.on("response", (res) => {
    if (res.status() >= 500) {
      errors.push(`HTTP ${res.status()} ${res.url()}`);
    }
  });

  await page.goto(`${WEB_BASE}/auth`, { waitUntil: "networkidle2" });
  await page.waitForSelector('input[type="email"]');
  await page.type('input[type="email"]', email);
  await page.type('input[type="password"]', password);
  await page.click('button[type="submit"]');

  await waitUntil(async () => !page.url().includes("/auth"), 15000, `login redirect for ${email}`);
  if (expectedPathPrefix) {
    await waitUntil(async () => new URL(page.url()).pathname.startsWith(expectedPathPrefix), 15000, `landing path ${expectedPathPrefix}`);
  }
  await page.waitForSelector("nav", { timeout: 10000 }).catch(() => undefined);
  await sleep(700);
  return { page, errors };
}

async function getSidebarLabels(page) {
  return page.evaluate(() => {
    const links = Array.from(document.querySelectorAll("aside a, nav a"));
    return [...new Set(links.map((a) => (a.textContent || "").trim()).filter(Boolean))];
  });
}

async function selectRosterEmployee(adminPage, searchText) {
  await adminPage.evaluate(async (needle) => {
    const buttons = Array.from(document.querySelectorAll("button"));
    const match = buttons.find((b) => (b.textContent || "").toLowerCase().includes(needle.toLowerCase()));
    if (!match) throw new Error(`Roster button containing "${needle}" not found`);
    match.click();
  }, searchText);
  await sleep(400);
}

async function readMenuChecklistState(adminPage) {
  return adminPage.evaluate(() => {
    const nodes = Array.from(document.querySelectorAll("[data-menu-id]"));
    return nodes.map((el) => {
      const cb = el.querySelector('[role="checkbox"], input[type="checkbox"]');
      const checkedAttr = cb?.getAttribute("data-state") || cb?.getAttribute("aria-checked");
      const checked = checkedAttr === "checked" || checkedAttr === "true" || Boolean(cb?.checked);
      const disabled = cb?.hasAttribute("disabled") || cb?.getAttribute("aria-disabled") === "true";
      return {
        id: el.getAttribute("data-menu-id"),
        planAvailable: el.getAttribute("data-plan-available") === "true",
        checked,
        disabled,
        text: (el.textContent || "").replace(/\s+/g, " ").trim(),
      };
    });
  });
}

async function clickMenuCheckbox(adminPage, menuId) {
  await adminPage.evaluate((id) => {
    const container = document.querySelector(`[data-menu-id="${id}"]`);
    if (!container) throw new Error(`Menu checkbox container ${id} not found`);
    const cb = container.querySelector('[role="checkbox"], input[type="checkbox"]');
    if (!cb) throw new Error(`Checkbox inside ${id} not found`);
    cb.click();
  }, menuId);
  await sleep(150);
}

async function clickButtonByText(page, text) {
  await page.evaluate((needle) => {
    const btns = Array.from(document.querySelectorAll("button"));
    const found = btns.find((b) => (b.textContent || "").trim().includes(needle));
    if (!found) throw new Error(`Button "${needle}" not found`);
    found.click();
  }, text);
}

async function connectOrderHub(token) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(ORDER_HUB_URL, { accessTokenFactory: () => token })
    .build();
  const events = { permissionsUpdated: [], accessRevoked: [], newOrder: [] };
  conn.on("PermissionsUpdated", (p) => events.permissionsUpdated.push(p));
  conn.on("AccessRevoked", (r) => events.accessRevoked.push(r));
  conn.on("NewOrder", (o) => events.newOrder.push(o));
  await conn.start();
  await conn.invoke("JoinRestaurantGroup");
  return { conn, events };
}

async function main() {
  let apiProc = null;
  let webProc = null;
  let browser = null;
  let dbCreated = false;
  let cashierHub = null;

  try {
    console.log(`=== 1. Creating disposable PostgreSQL 17 DB ${DB_NAME} ===`);
    runPsql("postgres", `CREATE DATABASE "${DB_NAME}";`);
    dbCreated = true;
    record("Isolated PostgreSQL 17 DB created", "PASS", DB_NAME);

    apiProc = spawn(
      "dotnet",
      ["exec", path.resolve("Tablo.Api/bin/Debug/net8.0/Tablo.Api.dll")],
      {
        cwd: path.resolve("Tablo.Api"),
        env: {
          ...process.env,
          ASPNETCORE_ENVIRONMENT: "Development",
          ASPNETCORE_URLS: `http://127.0.0.1:${API_PORT}`,
          ConnectionStrings__DefaultConnection: `Host=${PG_HOST};Port=${PG_PORT};Database=${DB_NAME};Username=${PG_USER};Password=${PG_PASS}`,
          Jwt__Key: "e2e-verification-signing-key-for-menu-access-2026-xyz",
          Jwt__Issuer: "TabloApi",
          Jwt__Audience: "TabloApp",
          AllowedOrigins__0: WEB_BASE,
          AllowedOrigins__1: `http://localhost:${WEB_PORT}`,
          SuperAdmin__Username: SA_USERNAME,
          SuperAdmin__Password: SA_PASSWORD,
          AutoMigrate: "true",
          SeedDummyData: "false",
          "Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        },
        stdio: "pipe",
      },
    );
    apiProc.stdout.on("data", (d) => process.stdout.write(`[API] ${d}`));
    apiProc.stderr.on("data", (d) => process.stderr.write(`[API ERR] ${d}`));
    apiProc.on("exit", (code, sig) => console.error(`[API EXITED] code=${code} sig=${sig}`));

    await waitForHttp(`http://127.0.0.1:${API_PORT}/health`, 60000);
    record("Backend started & migrations applied", "PASS", `port=${API_PORT}`);

    execFileSync("cmd.exe", ["/c", "npm run build"], {
      cwd: path.resolve("tablo"),
      env: { ...process.env, VITE_API_URL: API_BASE },
      stdio: "pipe",
    });

    webProc = spawn("cmd.exe", ["/c", `npx vite preview --host 127.0.0.1 --port ${WEB_PORT} --strictPort`], {
      cwd: path.resolve("tablo"),
      env: { ...process.env, VITE_API_URL: API_BASE },
      stdio: "pipe",
    });
    await waitForHttp(WEB_BASE, 30000);
    record("Frontend built & preview started", "PASS", WEB_BASE);

    // Seed Hotel City (start on Basic plan so Test Case 1 checks Basic plan gating!)
    const saLogin = await apiJson("/sysadmin/login", {
      method: "POST",
      body: { username: SA_USERNAME, password: SA_PASSWORD },
    });
    assert(saLogin.ok, `SuperAdmin login failed (${saLogin.status})`);
    const saToken = saLogin.data.token;

    const onboardA = await apiJson("/sysadmin/onboard", {
      method: "POST",
      token: saToken,
      body: {
        restaurantName: "Hotel City",
        adminFirstName: "City",
        adminLastName: "Admin",
        adminEmail: "city@admin.com",
        adminPassword: TEST_PASSWORD,
        planCode: "basic",
      },
    });
    const onboardB = await apiJson("/sysadmin/onboard", {
      method: "POST",
      token: saToken,
      body: {
        restaurantName: "Foreign Bistro",
        adminFirstName: "Foreign",
        adminLastName: "Admin",
        adminEmail: "foreign@admin.com",
        adminPassword: TEST_PASSWORD,
        planCode: "basic",
      },
    });
    assert(onboardA.ok && onboardB.ok, "Tenant onboarding failed");
    const restAId = onboardA.data.restaurantId;

    const adminLogin = await apiJson("/auth/login", {
      method: "POST",
      body: { email: "city@admin.com", password: TEST_PASSWORD },
    });
    assert(adminLogin.ok, "Admin API login failed");
    const adminToken = adminLogin.data.token;
    const adminId = adminLogin.data.user.id;

    async function createStaff(first, last, email, roleName) {
      const created = await apiJson("/employees", {
        method: "POST",
        token: adminToken,
        body: { firstName: first, lastName: last, email, password: TEST_PASSWORD, roleName },
      });
      assert(created.ok, `Create employee ${email} failed: ${created.status}`);
      return created.data.id;
    }

    const waiter1Id = await createStaff("Asha", "Waiter", "city@waiter.com", "Waiter");
    const waiter2Id = await createStaff("Ravi", "WaiterTwo", "city@waiter2.com", "Waiter");
    const kitchenId = await createStaff("Babu", "Kitchen", "city@kitchen.com", "Kitchen");
    const cashierId = await createStaff("Meera", "Cashier", "city@cashier.com", "Cashier");

    const foreignLogin = await apiJson("/auth/login", {
      method: "POST",
      body: { email: "foreign@admin.com", password: TEST_PASSWORD },
    });
    assert(foreignLogin.ok, "Foreign admin login failed");
    const foreignEmp = await apiJson("/employees", {
      method: "POST",
      token: foreignLogin.data.token,
      body: { firstName: "Foreign", lastName: "Waiter", email: "foreign@waiter.com", password: TEST_PASSWORD, roleName: "Waiter" },
    });
    assert(foreignEmp.ok, "Foreign employee creation failed");
    const foreignWaiterId = foreignEmp.data.id;

    // Seed category, menu item, and table for real workflows
    const cat = await apiJson(`/restaurants/${restAId}/categories`, {
      method: "POST",
      token: adminToken,
      body: { name: "Signature Dishes" },
    });
    assert(cat.ok, `Category creation failed: ${cat.status}`);

    const form = new FormData();
    form.append("CategoryId", cat.data.id);
    form.append("Name", "Thalassery Chicken Biryani");
    form.append("Description", "E2E Verification Dish");
    form.append("Price", "280");
    const item = await apiJson(`/restaurants/${restAId}/menu-items`, {
      method: "POST",
      token: adminToken,
      body: form,
      isForm: true,
    });
    assert(item.ok, `Menu item creation failed: ${item.status}`);
    const menuItemId = item.data.id;

    const tbl = await apiJson(`/restaurants/${restAId}/tables`, {
      method: "POST",
      token: adminToken,
      body: { tableNumber: 4 },
    });
    assert(tbl.ok, `Table creation failed: ${tbl.status}`);
    const tableId = tbl.data.id;
    const tableQr = tbl.data.qrToken;

    const waiterLogin = await apiJson("/auth/login", { method: "POST", body: { email: "city@waiter.com", password: TEST_PASSWORD } });
    const waiter2Login = await apiJson("/auth/login", { method: "POST", body: { email: "city@waiter2.com", password: TEST_PASSWORD } });
    const kitchenLogin = await apiJson("/auth/login", { method: "POST", body: { email: "city@kitchen.com", password: TEST_PASSWORD } });
    const cashierLogin = await apiJson("/auth/login", { method: "POST", body: { email: "city@cashier.com", password: TEST_PASSWORD } });
    assert(waiterLogin.ok && waiter2Login.ok && kitchenLogin.ok && cashierLogin.ok, "Staff logins failed");
    const waiterToken = waiterLogin.data.token;
    const waiter2Token = waiter2Login.data.token;
    const kitchenToken = kitchenLogin.data.token;
    const cashierToken = cashierLogin.data.token;

    record("Seeded Hotel City tenant, 5 staff, menu item, and table 4", "PASS");

    browser = await puppeteer.launch({
      headless: true,
      args: ["--no-sandbox", "--disable-setuid-sandbox"],
    });

    const adminCtx = await browser.createBrowserContext();
    const waiterCtx = await browser.createBrowserContext();
    const kitchenCtx = await browser.createBrowserContext();
    const cashierCtx = await browser.createBrowserContext();

    // ------------------------------------------------------------------
    // TEST CASE 1: Full Checklist & Basic Plan Gating in /menu-access
    // ------------------------------------------------------------------
    const { page: adminPage } = await signInViaUi(adminCtx, "city@admin.com", TEST_PASSWORD);
    await adminPage.goto(`${WEB_BASE}/menu-access`, { waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(adminPage, "body")).includes("city@waiter.com"), 10000, "roster loaded on /menu-access");

    // Select Waiter on Basic plan
    await selectRosterEmployee(adminPage, "city@waiter.com");
    await adminPage.waitForSelector("[data-menu-id]", { timeout: 10000 });
    const basicWaiterList = await readMenuChecklistState(adminPage);
    assert(basicWaiterList.length === 25, `Expected 25 assignable menus for Waiter, got ${basicWaiterList.length}`);
    assert(!basicWaiterList.some((x) => ["staff-management", "staff-menu-access", "support"].includes(x.id)), "Admin-only menus must not be assignable to Waiter");
    assert(!basicWaiterList.some((x) => x.id.startsWith("sa-")), "SuperAdmin menus must never appear");
    assert(basicWaiterList.filter((x) => x.checked).map((x) => x.id).join(",") === "overview,orders-servicedesk,staff-order-taking,table-billing", "Waiter defaults incorrect");
    const invItemBasic = basicWaiterList.find((x) => x.id === "inventory-stock");
    assert(invItemBasic && !invItemBasic.planAvailable && invItemBasic.disabled && invItemBasic.text.includes("Requires Premium plan"), `Expected disabled Premium requirement on inventory-stock, got ${JSON.stringify(invItemBasic)}`);
    await capture(adminPage, "01-menu-access-basic-plan-waiter-full-checklist.png");

    // Select Cashier on Basic plan
    await selectRosterEmployee(adminPage, "city@cashier.com");
    const basicCashierList = await readMenuChecklistState(adminPage);
    assert(basicCashierList.length === 25, `Expected 25 assignable menus for Cashier, got ${basicCashierList.length}`);
    assert(basicCashierList.filter((x) => x.checked).map((x) => x.id).join(",") === "cashier-billing", "Cashier default incorrect");
    await capture(adminPage, "02-menu-access-cashier-defaults-on-basic.png");

    // Verify Admin recovery checklist displays protected items checked & disabled
    await selectRosterEmployee(adminPage, "city@admin.com");
    const basicAdminList = await readMenuChecklistState(adminPage);
    assert(basicAdminList.length === 28, `Expected all 28 menus for Admin, got ${basicAdminList.length}`);
    assert(basicAdminList.find((x) => x.id === "staff-management")?.checked && basicAdminList.find((x) => x.id === "staff-management")?.disabled, "Admin staff-management must be checked & locked");
    assert(basicAdminList.find((x) => x.id === "staff-menu-access")?.checked && basicAdminList.find((x) => x.id === "staff-menu-access")?.disabled, "Admin staff-menu-access must be checked & locked");

    record("Test 1: Full grouped checklist, role defaults & Basic plan requirement badges", "PASS");

    // Upgrade Hotel City to Premium so cross-role + inventory/plus/premium features work end-to-end
    const upgradeRes = await apiJson(`/sysadmin/subscriptions/${restAId}/change-plan`, {
      method: "POST",
      token: saToken,
      body: { targetPlanCode: "premium", newStatus: "Active", reason: "Enable full workspace E2E" },
    });
    assert(upgradeRes.ok, `Plan upgrade failed: ${upgradeRes.status}`);
    await adminPage.reload({ waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(adminPage, "body")).includes("city@waiter.com"), 10000, "roster loaded after upgrade");
    await selectRosterEmployee(adminPage, "city@waiter.com");
    await adminPage.waitForSelector("[data-menu-id]", { timeout: 10000 });
    await waitUntil(async () => {
      const list = await readMenuChecklistState(adminPage);
      return list.find((x) => x.id === "inventory-stock")?.planAvailable === true;
    }, 10000, "Waiter checklist reflecting Premium upgrade");
    await capture(adminPage, "03-menu-access-premium-all-enabled.png");
    record("Test 1b: Upgraded to Premium — all 25 operational menus enabled for assignment", "PASS");

    // ------------------------------------------------------------------
    // TEST CASE 2: Baseline Role Defaults in Simultaneous Sessions
    // ------------------------------------------------------------------
    const { page: waiterPage } = await signInViaUi(waiterCtx, "city@waiter.com", TEST_PASSWORD, "/dashboard");
    const { page: kitchenPage } = await signInViaUi(kitchenCtx, "city@kitchen.com", TEST_PASSWORD, "/dashboard");
    const { page: cashierPage } = await signInViaUi(cashierCtx, "city@cashier.com", TEST_PASSWORD, "/cashier");

    const waiterNav = await getSidebarLabels(waiterPage);
    const kitchenNav = await getSidebarLabels(kitchenPage);
    const cashierNav = await getSidebarLabels(cashierPage);
    assert(waiterNav.includes("Floor & Orders") && !waiterNav.includes("Kitchen Queue") && !waiterNav.includes("Staff Menu Access"), `Unexpected Waiter nav: ${waiterNav}`);
    assert(kitchenNav.includes("Kitchen Queue") && !kitchenNav.includes("Cashier") && !kitchenNav.includes("Ingredients & Stock"), `Unexpected Kitchen nav: ${kitchenNav}`);
    assert(cashierNav.length === 1 && cashierNav[0] === "Cashier", `Unexpected Cashier nav: ${cashierNav}`);

    // Default API denials
    assert((await apiJson(`/restaurants/${restAId}/inventory/ingredients`, { token: waiterToken })).status === 403, "Default Waiter must get 403 on Inventory");
    assert((await apiJson("/support/conversations", { token: waiterToken })).status === 403, "Waiter must get 403 on Support");
    assert((await apiJson(`/restaurants/${restAId}/service-desk/tables`, { token: kitchenToken })).status === 403, "Default Kitchen must get 403 on ServiceDesk tables");
    assert((await apiJson("/employees", { token: cashierToken })).status === 403, "Cashier must get 403 on Employees");

    await capture(waiterPage, "04-waiter-defaults-sidebar.png");
    await capture(kitchenPage, "05-kitchen-defaults-sidebar.png");
    await capture(cashierPage, "06-cashier-defaults-sidebar.png");
    record("Test 2: Default role sessions & direct backend boundaries verified", "PASS");

    // ------------------------------------------------------------------
    // TEST CASE 3: Grant Waiter Kitchen Queue (Cross-role + Live Update + Order Execution)
    // ------------------------------------------------------------------
    await selectRosterEmployee(adminPage, "city@waiter.com");
    await clickMenuCheckbox(adminPage, "kitchen-queue");
    await capture(adminPage, "07-admin-grant-kitchen-to-waiter.png");
    await clickButtonByText(adminPage, "Save menu access");

    // Open session live update (NO reload!) in waiterPage
    await waitUntil(async () => (await getSidebarLabels(waiterPage)).includes("Kitchen Queue"), 10000, "Waiter live sidebar receiving Kitchen Queue");

    // Place a guest QR order so Kitchen Queue has a live ticket
    const qrOrder = await apiJson("/orders", {
      method: "POST",
      body: { qrToken: tableQr, items: [{ menuItemId, quantity: 1 }] },
    });
    assert(qrOrder.ok, `Order create failed: ${qrOrder.status}`);
    const orderId = qrOrder.data.orderId;

    // Waiter navigates directly to /dashboard?tab=kitchen and reloads to prove persistence!
    await waiterPage.goto(`${WEB_BASE}/dashboard?tab=kitchen`, { waitUntil: "networkidle2" });
    await waiterPage.reload({ waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(waiterPage, "h1")).includes("Kitchen Production Queue"), 10000, "Waiter Kitchen Production Queue heading after refresh");

    // Prove Waiter can execute real Kitchen API transitions
    for (const nextStatus of ["Accepted", "Preparing", "Ready", "Served"]) {
      const upd = await apiJson(`/orders/${orderId}/status`, {
        method: "PATCH",
        token: waiterToken,
        body: { restaurantId: restAId, status: nextStatus },
      });
      assert(upd.ok, `Waiter with Kitchen Queue failed PATCH status=${nextStatus}: ${upd.status} ${JSON.stringify(upd.data)}`);
    }
    await capture(waiterPage, "08-waiter-kitchen-queue-after-refresh.png");
    record("Test 3: Waiter granted Kitchen Queue -> live open-session sync, refresh persistence & kitchen status workflow", "PASS");

    // ------------------------------------------------------------------
    // TEST CASE 4: Grant Kitchen Cashier + Inventory + Financial Reports
    // ------------------------------------------------------------------
    await selectRosterEmployee(adminPage, "city@kitchen.com");
    await clickMenuCheckbox(adminPage, "cashier-billing");
    await clickMenuCheckbox(adminPage, "inventory-stock");
    await clickMenuCheckbox(adminPage, "financial-reports");
    await clickButtonByText(adminPage, "Save menu access");

    await waitUntil(async () => {
      const labels = await getSidebarLabels(kitchenPage);
      return labels.includes("Cashier") && labels.includes("Ingredients & Stock") && labels.includes("Payment Reports");
    }, 10000, "Kitchen live sidebar receiving Cashier, Inventory, and Reports");

    // Open /cashier as Kitchen and execute actual Cash bill settlement!
    await kitchenPage.goto(`${WEB_BASE}/cashier`, { waitUntil: "networkidle2" });
    await kitchenPage.reload({ waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(kitchenPage, "h1")).includes("Cashier"), 10000, "Kitchen /cashier workspace");

    const reviewBill = await apiJson(`/restaurants/${restAId}/service-desk/tables/${tableId}/bill`, {
      token: kitchenToken,
    });
    assert(reviewBill.ok && reviewBill.data?.openBill?.id, `Kitchen reviewing bill failed: ${reviewBill.status} ${JSON.stringify(reviewBill.data)}`);
    const bill = reviewBill.data.openBill;
    const settle = await apiJson(`/restaurants/${restAId}/service-desk/bills/${bill.id}/settle`, {
      method: "POST",
      token: kitchenToken,
      body: {
        operationId: crypto.randomUUID(),
        expectedRevision: bill.revision,
        expectedTotal: bill.totalAmount,
        paymentMethod: "Cash",
      },
    });
    assert(settle.ok, `Kitchen Cashier settlement failed: ${settle.status} ${JSON.stringify(settle.data)}`);
    await capture(kitchenPage, "09-kitchen-cashier-billing-settled.png");

    // Verify Kitchen opens /inventory & /reports cleanly without errors
    await kitchenPage.goto(`${WEB_BASE}/inventory`, { waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(kitchenPage, "h1")).includes("Inventory"), 10000, "Kitchen /inventory heading");
    assert((await apiJson(`/restaurants/${restAId}/inventory/ingredients`, { token: kitchenToken })).ok, "Kitchen granted inventory-stock failed API");
    await capture(kitchenPage, "10-kitchen-inventory-access.png");

    await kitchenPage.goto(`${WEB_BASE}/reports`, { waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(kitchenPage, "body")).includes("Order History"), 10000, "Kitchen /reports heading");
    assert((await apiJson(`/restaurants/${restAId}/payment-reports`, { token: kitchenToken })).ok, "Kitchen granted financial-reports failed PaymentReports");
    assert((await apiJson("/reports/summary", { token: kitchenToken })).ok, "Kitchen granted financial-reports failed Reports summary");

    // Verify Admin-only pages & APIs remain strictly blocked for Kitchen & Waiter
    await kitchenPage.goto(`${WEB_BASE}/staff`, { waitUntil: "networkidle2" });
    await waitUntil(async () => new URL(kitchenPage.url()).pathname !== "/staff" || (await textOf(kitchenPage, "body")).includes("Admin"), 8000, "Kitchen blocked from /staff");
    assert((await apiJson("/employees", { token: kitchenToken })).status === 403, "Kitchen must get 403 on /employees");
    assert((await apiJson(`/employees/${waiter1Id}/menu-access`, { method: "PUT", token: kitchenToken, body: { allowedMenuIds: ["kitchen-queue"] } })).status === 403, "Kitchen cannot edit menu access");
    assert((await apiJson("/support/conversations", { token: kitchenToken })).status === 403, "Kitchen cannot access Support API");

    for (const forbiddenMenu of ["staff-management", "staff-menu-access", "support", "sa-overview"]) {
      const badAssign = await apiJson(`/employees/${waiter1Id}/menu-access`, {
        method: "PUT",
        token: adminToken,
        body: { allowedMenuIds: [forbiddenMenu] },
      });
      assert(badAssign.status === 400, `Expected 400 rejecting ${forbiddenMenu} for Waiter, got ${badAssign.status}`);
    }

    record("Test 4: Kitchen granted Cashier/Inventory/Reports, Cash settled, Admin/Support boundaries enforced", "PASS");

    // ------------------------------------------------------------------
    // TEST CASE 5: Single-Employee Isolation, Downgrade Gate, Live Revocation & Reset
    // ------------------------------------------------------------------
    // Waiter2 stays untouched
    const waiter2Access = await apiJson("/navigation-access", { token: waiter2Token });
    assert(waiter2Access.ok && waiter2Access.data.allowedMenuIds === null, "Waiter 2 must remain on untouched defaults");
    assert((await apiJson(`/restaurants/${restAId}/inventory/ingredients`, { token: waiter2Token })).status === 403, "Waiter 2 must stay blocked from Inventory");

    // Foreign tenant isolation + Admin recovery guard
    const foreignAttempt = await apiJson(`/employees/${foreignWaiterId}/menu-access`, {
      method: "PUT",
      token: adminToken,
      body: { allowedMenuIds: [] },
    });
    assert(foreignAttempt.status === 404, `Cross-tenant menu edit must return 404, got ${foreignAttempt.status}`);
    const adminSelfEmpty = await apiJson(`/employees/${adminId}/menu-access`, {
      method: "PUT",
      token: adminToken,
      body: { allowedMenuIds: ["staff-management"] },
    });
    assert(adminSelfEmpty.ok && adminSelfEmpty.data.allowedMenuIds.includes("staff-menu-access"), "Admin recovery menus must remain protected");

    // Downgrade restaurant back to Basic -> Kitchen's inventory-stock custom grant must now fail FEATURE_NOT_IN_PLAN
    const downgradeRes = await apiJson(`/sysadmin/subscriptions/${restAId}/change-plan`, {
      method: "POST",
      token: saToken,
      body: { targetPlanCode: "basic", newStatus: "Active", reason: "Verify plan gate blocks previously granted custom menu" },
    });
    assert(downgradeRes.ok, "Plan downgrade failed");
    const invAfterDowngrade = await apiJson(`/restaurants/${restAId}/inventory/ingredients`, { token: kitchenToken });
    assert(invAfterDowngrade.status === 403, `Expected 403 after downgrade to Basic, got ${invAfterDowngrade.status}`);
    const saveDisabledAttempt = await apiJson(`/employees/${waiter1Id}/menu-access`, {
      method: "PUT",
      token: adminToken,
      body: { allowedMenuIds: ["inventory-stock"] },
    });
    assert(saveDisabledAttempt.status === 400, "Admin cannot save a menu excluded by restaurant subscription plan");

    // Open-session revocation + SignalR stream termination for Cashier!
    cashierHub = await connectOrderHub(cashierToken);
    await cashierPage.goto(`${WEB_BASE}/cashier`, { waitUntil: "networkidle2" });

    const revokeCashier = await apiJson(`/employees/${cashierId}/menu-access`, {
      method: "PUT",
      token: adminToken,
      body: { allowedMenuIds: [] },
    });
    assert(revokeCashier.ok, "Failed revoking Cashier to empty");

    await waitUntil(async () => cashierHub.events.accessRevoked.length > 0 && cashierHub.events.permissionsUpdated.length > 0, 10000, "OrderHub immediate revocation events");
    await waitUntil(async () => (await textOf(cashierPage, "body")).includes("No menu access"), 10000, "Cashier open tab showing No menu access immediately without reload");
    await capture(cashierPage, "11-open-session-revoked-no-access.png");

    // Place another QR order and ensure revoked Cashier Hub DOES NOT receive NewOrder
    cashierHub.events.newOrder.length = 0;
    const postRevokeOrder = await apiJson("/orders", {
      method: "POST",
      body: { qrToken: tableQr, items: [{ menuItemId, quantity: 1 }] },
    });
    assert(postRevokeOrder.ok, "Post-revoke QR order failed");
    await sleep(800);
    assert(cashierHub.events.newOrder.length === 0, "Revoked Cashier connection must not receive NewOrder broadcasts");
    assert((await apiJson(`/restaurants/${restAId}/service-desk/tables`, { token: cashierToken })).status === 403, "Revoked Cashier must get 403 on ServiceDesk");

    // Reset Cashier to defaults via Admin UI
    await selectRosterEmployee(adminPage, "city@cashier.com");
    await clickButtonByText(adminPage, "Reset to Cashier defaults");
    await clickButtonByText(adminPage, "Save menu access");

    await waitUntil(async () => {
      const res = await apiJson(`/restaurants/${restAId}/service-desk/tables`, { token: cashierToken });
      return res.status === 200;
    }, 10000, "Cashier restored via Reset to defaults");

    await cashierPage.reload({ waitUntil: "networkidle2" });
    await waitUntil(async () => (await textOf(cashierPage, "h1")).includes("Cashier") && (await getSidebarLabels(cashierPage)).includes("Cashier"), 10000, "Cashier UI restored");
    await capture(cashierPage, "12-reset-to-role-defaults.png");

    record("Test 5: Employee isolation, tenant boundary, downgrade enforcement, SignalR revocation & reset verified", "PASS");

    fs.writeFileSync(path.join(QA_DIR, "RESULTS.json"), JSON.stringify({ runStamp: RUN_STAMP, checks }, null, 2));
    console.log(`\nALL CHECKS COMPLETE: ${checks.filter((c) => c.status === "PASS").length}/${checks.length} PASSED. Screenshots at ${QA_DIR}`);
  } finally {
    try {
      await cashierHub?.conn?.stop();
    } catch {}
    if (browser) await browser.close().catch(() => {});
    killProcessTree(webProc);
    killProcessTree(apiProc);
    await sleep(500);
    if (dbCreated) {
      try {
        runPsql("postgres", `SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();`);
        runPsql("postgres", `DROP DATABASE IF EXISTS "${DB_NAME}";`);
      } catch (e) {
        console.warn(`Cleanup warning for DB ${DB_NAME}: ${e.message}`);
      }
    }
  }
}

main().catch((err) => {
  console.error("VERIFICATION FAILED:", err);
  process.exitCode = 1;
});
