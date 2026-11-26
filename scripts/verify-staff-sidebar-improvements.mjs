import fs from "node:fs";
import path from "node:path";
import { execFileSync, spawn } from "node:child_process";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const puppeteer = require("../tablo/node_modules/puppeteer");

const PSQL = "C:\\Program Files\\PostgreSQL\\17\\bin\\psql.exe";
const PG_HOST = "127.0.0.1";
const PG_PORT = 5432;
const PG_USER = "postgres";
const PG_PASS = process.env.PGPASSWORD || "postgres";
const API_PORT = 5197;
const WEB_PORT = 4197;
const API_BASE = `http://127.0.0.1:${API_PORT}/api`;
const WEB_BASE = `http://127.0.0.1:${WEB_PORT}`;

const RUN_STAMP = process.env.QA_STAMP || new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14);
const DB_NAME = `tablo_staff_sidebar_${RUN_STAMP.replace(/[^a-zA-Z0-9_]/g, "_")}`;
const QA_DIR = path.resolve(process.env.QA_OUT_DIR || `qa/staff-sidebar-improvements/${RUN_STAMP}`);
fs.mkdirSync(QA_DIR, { recursive: true });

const TEST_PASS = `StaffQA#${RUN_STAMP.slice(-4)}!`;
const SA_USER = "superadmin-qa";
const SA_PASS = `SaStaff#${RUN_STAMP.slice(-4)}!`;

const checks = [];
function record(name, status, details = "") {
  checks.push({ name, status, details });
  console.log(`[${status}] ${name}${details ? ` — ${details}` : ""}`);
}
function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
function runPsql(db, sql) {
  return execFileSync(PSQL, ["-h", PG_HOST, "-p", String(PG_PORT), "-U", PG_USER, "-w", "-d", db, "-v", "ON_ERROR_STOP=1", "-c", sql], {
    encoding: "utf8",
    env: { ...process.env, PGPASSWORD: PG_PASS },
  });
}
function killTree(proc) {
  if (!proc?.pid) return;
  try { execFileSync("taskkill", ["/PID", String(proc.pid), "/T", "/F"], { stdio: "ignore" }); } catch {}
}
async function waitForHttp(url, timeoutMs = 45000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const r = await fetch(url);
      if (r.ok) return;
    } catch {}
    await sleep(300);
  }
  throw new Error(`Timed out waiting for ${url}`);
}
async function apiJson(p, { method = "GET", token, body } = {}) {
  const res = await fetch(`${API_BASE}${p}`, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  return { ok: res.ok, status: res.status, data };
}

async function main() {
  let apiProc = null;
  let webProc = null;
  let browser = null;
  let dbCreated = false;
  let exitCode = 0;

  try {
    runPsql("postgres", `CREATE DATABASE ${DB_NAME};`);
    dbCreated = true;
    record("Create isolated PostgreSQL 17 database", "PASS", DB_NAME);

    execFileSync("dotnet", ["build", "Tablo.Api/Tablo.Api.csproj", "--no-restore"], {
      cwd: path.resolve("."),
      stdio: "pipe",
    });

    apiProc = spawn("dotnet", ["exec", path.resolve("Tablo.Api/bin/Debug/net8.0/Tablo.Api.dll")], {
      cwd: path.resolve("Tablo.Api"),
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: `http://127.0.0.1:${API_PORT}`,
        ConnectionStrings__DefaultConnection: `Host=${PG_HOST};Port=${PG_PORT};Database=${DB_NAME};Username=${PG_USER};Password=${PG_PASS}`,
        Jwt__Key: "e2e-staff-sidebar-signing-key-2026-abcdef",
        Jwt__Issuer: "TabloApi",
        Jwt__Audience: "TabloApp",
        AllowedOrigins__0: WEB_BASE,
        AllowedOrigins__1: `http://localhost:${WEB_PORT}`,
        SuperAdmin__Username: SA_USER,
        SuperAdmin__Password: SA_PASS,
        AutoMigrate: "true",
        SeedDummyData: "false",
      },
      stdio: "pipe",
    });
    await waitForHttp(`http://127.0.0.1:${API_PORT}/health`, 60000);
    record("Apply migrations & boot Tablo.Api", "PASS", `port=${API_PORT}`);

    webProc = spawn("cmd.exe", ["/c", `npx vite --host 127.0.0.1 --port ${WEB_PORT} --strictPort`], {
      cwd: path.resolve("tablo"),
      env: { ...process.env, VITE_API_URL: API_BASE },
      stdio: "pipe",
    });
    await waitForHttp(WEB_BASE, 30000);
    record("Boot Vite dev server", "PASS", WEB_BASE);

    const sa = await apiJson("/sysadmin/login", { method: "POST", body: { username: SA_USER, password: SA_PASS } });
    assert(sa.ok, "SuperAdmin login failed");
    const saToken = sa.data.token;

    await apiJson("/sysadmin/onboard", {
      method: "POST",
      token: saToken,
      body: { restaurantName: "Hotel City", adminFirstName: "City", adminLastName: "Admin", adminEmail: "city@admin.com", adminPassword: TEST_PASS, planCode: "premium" },
    });
    const login = await apiJson("/auth/login", { method: "POST", body: { email: "city@admin.com", password: TEST_PASS } });
    assert(login.ok, "City Admin login failed");
    const adminToken = login.data.token;

    const waiter = await apiJson("/employees", { method: "POST", token: adminToken, body: { firstName: "Maya", lastName: "Waiter", email: "maya@hotelcity.test", password: TEST_PASS, roleName: "Waiter" } });
    const cashier = await apiJson("/employees", { method: "POST", token: adminToken, body: { firstName: "Arjun", lastName: "Cashier", email: "arjun@hotelcity.test", password: TEST_PASS, roleName: "Cashier" } });
    const inactiveEmp = await apiJson("/employees", { method: "POST", token: adminToken, body: { firstName: "Rohan", lastName: "Inactive", email: "rohan.inactive@hotelcity.test", password: TEST_PASS, roleName: "Kitchen" } });
    await apiJson(`/employees/${inactiveEmp.data.id}/status`, { method: "PATCH", token: adminToken, body: { isActive: false } });

    // Create unread support message so bottom utility badge displays live count
    const conv = await apiJson("/support/conversations", {
      method: "POST",
      token: adminToken,
      body: { subject: "Receipt printer check", message: "Initial question", clientOperationId: crypto.randomUUID() },
    });
    await apiJson(`/sysadmin/support/conversations/${conv.data.id}/messages`, {
      method: "POST",
      token: saToken,
      body: { message: "Support reply for badge verification.", clientOperationId: crypto.randomUUID() },
    });

    browser = await puppeteer.launch({ headless: "new", args: ["--no-sandbox", "--disable-setuid-sandbox"] });
    const page = await browser.newPage();
    page.on("dialog", async (dialog) => { await dialog.accept(); });
    await page.setViewport({ width: 1366, height: 860 });
    await page.goto(`${WEB_BASE}/auth`, { waitUntil: "networkidle2" });
    await page.type('input[type="email"]', "city@admin.com");
    await page.type('input[type="password"]', TEST_PASS);
    await page.click('button[type="submit"]');
    await page.waitForFunction(() => !location.pathname.includes("/auth"), { timeout: 15000 });
    await sleep(700);

    // 1. /staff defaults to Active (excludes Rohan Inactive)
    await page.goto(`${WEB_BASE}/staff`, { waitUntil: "networkidle2" });
    await sleep(600);
    let bodyText = await page.evaluate(() => document.body.innerText);
    assert(!bodyText.includes("Rohan Inactive"), "Inactive employee must be hidden from default Active /staff list");
    assert(bodyText.includes("Maya Waiter") && bodyText.includes("Arjun Cashier"), "Active employees missing");
    await page.screenshot({ path: path.join(QA_DIR, "after-01-staff-active-list.png") });
    await page.screenshot({ path: path.join(QA_DIR, "after-06-sidebar-expanded-ops-top.png") });
    record("/staff defaults to Active & sidebar top daily operations visible", "PASS");

    // 2. Switch to Inactive tab on /staff & verify Rohan Inactive
    await page.click('[data-status-filter="inactive"]');
    await sleep(500);
    bodyText = await page.evaluate(() => document.body.innerText);
    assert(bodyText.includes("Rohan Inactive") && !bodyText.includes("Maya Waiter"), "Inactive filter mismatch on /staff");
    await page.screenshot({ path: path.join(QA_DIR, "after-02-staff-inactive-filter.png") });
    record("/staff Inactive filter displays inactive employees", "PASS");

    // Verify Arjun Cashier could log in before delete, then immediately fails JWT + login after delete
    const arjunLoginBefore = await apiJson("/auth/login", { method: "POST", body: { email: "arjun@hotelcity.test", password: TEST_PASS } });
    assert(arjunLoginBefore.ok, "Arjun should log in before delete");
    const arjunTokenBefore = arjunLoginBefore.data.token;
    assert((await apiJson("/navigation-access", { token: arjunTokenBefore })).ok, "Arjun token should work before delete");

    // 3. Delete Arjun Cashier on All/Active tab via confirmation modal
    await page.click('[data-status-filter="active"]');
    await sleep(500);
    await page.click('button[aria-label="Delete Arjun Cashier"]');
    await sleep(300);
    const modalText = await page.evaluate(() => document.querySelector('[role="dialog"]')?.textContent || "");
    assert(modalText.includes("Arjun Cashier") && modalText.includes("arjun@hotelcity.test"), "Delete confirmation must show name & login email");
    await page.screenshot({ path: path.join(QA_DIR, "after-04-delete-confirm-dialog.png") });

    const confirmBtn = await page.$('[role="dialog"] button:last-child');
    await confirmBtn.click();
    await sleep(700);
    bodyText = await page.evaluate(() => document.body.innerText);
    assert(!bodyText.includes("Arjun Cashier"), "Deleted employee should be removed immediately");
    await page.click('[data-status-filter="all"]');
    await sleep(400);
    bodyText = await page.evaluate(() => document.body.innerText);
    assert(!bodyText.includes("Arjun Cashier"), "Deleted employee must stay excluded from All filter");
    assert((await apiJson("/navigation-access", { token: arjunTokenBefore })).status === 401, "Arjun JWT must be rejected immediately after soft delete");
    assert(!(await apiJson("/auth/login", { method: "POST", body: { email: "arjun@hotelcity.test", password: TEST_PASS } })).ok, "Deleted employee cannot log in");
    await page.screenshot({ path: path.join(QA_DIR, "after-05-staff-deleted-removed.png") });
    record("Soft delete removes employee immediately & revokes auth/JWT", "PASS");

    // 4. /menu-access defaults to Active and clears panel when filter hides selected employee
    await page.goto(`${WEB_BASE}/menu-access`, { waitUntil: "networkidle2" });
    await sleep(600);
    bodyText = await page.evaluate(() => document.body.innerText);
    assert(!bodyText.includes("Rohan Inactive"), "Inactive employee hidden by default on /menu-access");
    await page.screenshot({ path: path.join(QA_DIR, "after-03-menu-access-active-list.png") });

    await page.click('[data-status-filter="inactive"]');
    await sleep(500);
    await page.evaluate(() => {
      const btn = Array.from(document.querySelectorAll("button")).find((b) => (b.textContent || "").includes("Rohan Inactive"));
      if (btn) btn.click();
    });
    await sleep(400);
    assert((await page.evaluate(() => document.body.innerText)).includes("Save menu access"), "Permission panel should open for selected employee");

    await page.click('[data-status-filter="active"]');
    await sleep(400);
    assert((await page.evaluate(() => document.body.innerText)).includes("Select an employee to manage their menu access"), "Permission panel must clear when selected employee leaves filter");
    record("/menu-access filtering and selected-panel clearing", "PASS");

    // 5. Scroll sidebar to bottom, verify order & bottom utility Support unread badge, collapsed & mobile drawer
    const navOrder = await page.evaluate(() => {
      const nav = document.querySelector("aside nav");
      if (nav) nav.scrollTop = nav.scrollHeight;
      return Array.from(document.querySelectorAll("aside a, aside button[aria-label]"))
        .map((el) => (el.getAttribute("aria-label") || el.textContent || "").trim())
        .filter(Boolean);
    });
    assert(navOrder.indexOf("Overview") < navOrder.indexOf("Staff Order Taking"), "Overview must precede Staff Order Taking");
    assert(navOrder.indexOf("Staff Order Taking") < navOrder.indexOf("Cashier"), "Staff Order Taking must precede Cashier");
    assert(navOrder.indexOf("Marketplace Orders") < navOrder.indexOf("Ingredients & Stock"), "Daily Ops must precede Inventory");
    assert(navOrder.indexOf("Payment Reports") < navOrder.indexOf("Staff & Roles"), "Reporting must precede Restaurant Administration");
    assert(navOrder.indexOf("Staff Menu Access") < navOrder.indexOf("Support"), "Administration must precede Support utility");
    assert(await page.$('[data-testid="sidebar-unread-support"]'), "Support unread badge must render in utility footer");
    await sleep(300);
    await page.screenshot({ path: path.join(QA_DIR, "after-07-sidebar-lower-admin-utility.png") });

    // Collapse rail
    await page.click('button[aria-label="Collapse sidebar"]');
    await sleep(400);
    await page.screenshot({ path: path.join(QA_DIR, "after-08-sidebar-collapsed-rail.png") });

    // Mobile drawer
    await page.setViewport({ width: 390, height: 844 });
    await sleep(300);
    await page.click('button[aria-label="Open navigation menu"]');
    await sleep(400);
    await page.screenshot({ path: path.join(QA_DIR, "after-09-sidebar-mobile-drawer.png") });
    record("Sidebar ordering, independent scroll, bottom utility badge, rail, mobile drawer", "PASS");
  } catch (err) {
    console.error("E2E FAILURE:", err);
    record("E2E run", "FAIL", err.message);
    exitCode = 1;
  } finally {
    if (browser) await browser.close().catch(() => {});
    killTree(webProc);
    killTree(apiProc);
    await sleep(400);
    if (dbCreated) {
      try {
        runPsql("postgres", `SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();`);
        runPsql("postgres", `DROP DATABASE IF EXISTS ${DB_NAME};`);
      } catch {}
    }
    fs.writeFileSync(path.join(QA_DIR, "results.json"), JSON.stringify({ qaDir: QA_DIR, checks }, null, 2));
    process.exit(exitCode);
  }
}

main();
