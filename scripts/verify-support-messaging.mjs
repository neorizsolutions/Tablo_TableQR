import fs from "node:fs";
import path from "node:path";
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
const API_PORT = 5188;
const WEB_PORT = 4188;
const API_BASE = `http://127.0.0.1:${API_PORT}/api`;
const HUB_URL = `http://127.0.0.1:${API_PORT}/hubs/support`;
const WEB_BASE = `http://127.0.0.1:${WEB_PORT}`;

const RUN_STAMP = new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14);
const DB_NAME = `tablo_support_e2e_${RUN_STAMP}`;
const QA_DIR = path.resolve(`qa/support-messaging/${RUN_STAMP}`);
const MAIL_SINK_DIR = path.resolve(`tmp/mail_sink_${RUN_STAMP}`);

fs.mkdirSync(QA_DIR, { recursive: true });
fs.mkdirSync(MAIL_SINK_DIR, { recursive: true });

const TEST_PASSWORD = `SupportQA#${RUN_STAMP.slice(-4)}!`;
const SA_USERNAME = "superadmin-qa";
const SA_PASSWORD = `SaQA#${RUN_STAMP.slice(-4)}!`;

const checks = [];
function record(name, status, details = "") {
  checks.push({ name, status, details });
  console.log(`[${status}] ${name}${details ? ` — ${details}` : ""}`);
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
      // Wait for server startup
    }
    await sleep(500);
  }
  throw new Error(`Timed out waiting for ${url}`);
}

async function apiJson(urlPath, { method = "GET", token, body } = {}) {
  const res = await fetch(`${API_BASE}${urlPath}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
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

async function main() {
  let apiProc = null;
  let webProc = null;
  let browser = null;
  let dbCreated = false;

  try {
    console.log(`=== 1. Creating disposable PostgreSQL 17 database ${DB_NAME} ===`);
    runPsql("postgres", `CREATE DATABASE "${DB_NAME}";`);
    dbCreated = true;
    record("Isolated PostgreSQL 17 E2E DB initialized", "PASS", `db=${DB_NAME} port=${PG_PORT}`);

    console.log("=== 2. Starting Tablo.Api & Frontend Preview ===");
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
          Jwt__Key: "e2e-verification-signing-key-for-support-module-2026-xyz",
          Jwt__Issuer: "TabloApi",
          Jwt__Audience: "TabloApp",
          AllowedOrigins__0: WEB_BASE,
          AllowedOrigins__1: `http://localhost:${WEB_PORT}`,
          SuperAdmin__Username: SA_USERNAME,
          SuperAdmin__Password: SA_PASSWORD,
          Email__LocalSinkDirectory: MAIL_SINK_DIR,
          Email__FromEmail: "no-reply@notify.neorizsolutions.com",
          Email__FromName: "Tablo Support Escalation",
          AutoMigrate: "true",
          SeedDummyData: "false",
          "App__FrontendBaseUrl": WEB_BASE,
        },
        stdio: "pipe",
      },
    );
    apiProc.stdout.on("data", (d) => process.stdout.write(`[API] ${d}`));
    apiProc.stderr.on("data", (d) => process.stderr.write(`[API ERR] ${d}`));

    await waitForHttp(`http://127.0.0.1:${API_PORT}/health`, 60000);
    record("Backend started and applied EF migrations on PostgreSQL", "PASS");

    // Build frontend with VITE_API_URL pointing to our local E2E backend port
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
    record("Frontend built and preview server running", "PASS", WEB_BASE);

    console.log("=== 3. Seeding isolated test tenants and accounts ===");
    const saLogin = await apiJson("/sysadmin/login", {
      method: "POST",
      body: { username: SA_USERNAME, password: SA_PASSWORD },
    });
    if (!saLogin.ok) throw new Error(`SuperAdmin login failed: ${saLogin.status}`);
    const saToken = saLogin.data.token;

    const onboardA = await apiJson("/sysadmin/onboard", {
      method: "POST",
      token: saToken,
      body: {
        restaurantName: "Hotel City Support QA",
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
        restaurantName: "Spice Garden Foreign QA",
        adminFirstName: "Spice",
        adminLastName: "Admin",
        adminEmail: "spice@admin.com",
        adminPassword: TEST_PASSWORD,
        planCode: "basic",
      },
    });
    if (!onboardA.ok || !onboardB.ok) throw new Error("Failed onboarding QA tenants");
    const restAId = onboardA.data.restaurantId;
    const restBId = onboardB.data.restaurantId;

    const adminALogin = await apiJson("/auth/login", {
      method: "POST",
      body: { email: "city@admin.com", password: TEST_PASSWORD },
    });
    const adminAToken = adminALogin.data.token;

    const adminBLogin = await apiJson("/auth/login", {
      method: "POST",
      body: { email: "spice@admin.com", password: TEST_PASSWORD },
    });
    const adminBToken = adminBLogin.data.token;

    // Create secondary Admin (for live browser session revocation test) & Waiter (for RBAC denial test)
    const revocableRes = await apiJson("/employees", {
      method: "POST",
      token: adminAToken,
      body: {
        firstName: "Revoke",
        lastName: "Tester",
        email: "city.revoke@admin.com",
        password: TEST_PASSWORD,
        roleName: "Admin",
      },
    });
    const waiterRes = await apiJson("/employees", {
      method: "POST",
      token: adminAToken,
      body: {
        firstName: "City",
        lastName: "Waiter",
        email: "city@waiter.com",
        password: TEST_PASSWORD,
        roleName: "Waiter",
      },
    });
    if (!revocableRes.ok || !waiterRes.ok) throw new Error("Failed creating secondary QA users");
    const revocableId = revocableRes.data.id;

    // Seed a foreign conversation under Spice Garden
    const foreignConvRes = await apiJson("/support/conversations", {
      method: "POST",
      token: adminBToken,
      body: {
        subject: "Spice Garden confidential ticket",
        message: "Foreign tenant confidential data",
        clientOperationId: crypto.randomUUID(),
      },
    });
    const foreignConvId = foreignConvRes.data.conversation.id;

    console.log("=== 4. Wire-Level SignalR Security & Revocation Tests ===");
    const connA = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => adminAToken })
      .build();
    await connA.start();

    let deniedSuperAdmin = false;
    try {
      await connA.invoke("JoinSuperAdminInbox");
    } catch {
      deniedSuperAdmin = true;
    }
    if (!deniedSuperAdmin) throw new Error("Restaurant Admin was allowed into SuperAdmin SignalR group!");

    let deniedForeignRest = false;
    try {
      await connA.invoke("JoinRestaurantSupport", restBId);
    } catch {
      deniedForeignRest = true;
    }
    if (!deniedForeignRest) throw new Error("Restaurant Admin joined foreign restaurant SignalR group!");

    let deniedForeignConv = false;
    try {
      await connA.invoke("JoinConversation", foreignConvId);
    } catch {
      deniedForeignConv = true;
    }
    if (!deniedForeignConv) throw new Error("Restaurant Admin joined foreign conversation SignalR group!");

    record(
      "SignalR wire authorization denies platform inbox, foreign restaurant, and foreign conversation",
      "PASS",
    );
    await connA.stop();

    // Test repeated send idempotency via API
    const dupOpId = crypto.randomUUID();
    const firstCreate = await apiJson("/support/conversations", {
      method: "POST",
      token: adminAToken,
      body: {
        subject: "Idempotency API Check",
        message: "Initial message",
        clientOperationId: dupOpId,
      },
    });
    const replayCreate = await apiJson("/support/conversations", {
      method: "POST",
      token: adminAToken,
      body: {
        subject: "Idempotency API Check",
        message: "Initial message",
        clientOperationId: dupOpId,
      },
    });
    if (
      firstCreate.data.conversation.id !== replayCreate.data.conversation.id ||
      replayCreate.data.deduplicated !== true
    ) {
      throw new Error("ClientOperationId deduplication failed!");
    }
    await apiJson(`/sysadmin/support/conversations/${firstCreate.data.conversation.id}/resolve`, {
      method: "POST",
      token: saToken,
      body: {},
    });
    record("API retry with identical ClientOperationId deduplicates cleanly", "PASS");

    console.log("=== 5. Two-Session Simultaneous Browser E2E Verification ===");
    browser = await puppeteer.launch({
      headless: "new",
      defaultViewport: { width: 1440, height: 900 },
      args: ["--no-sandbox", "--disable-setuid-sandbox"],
    });

    const ctxRest = await browser.createBrowserContext();
    const ctxSA = await browser.createBrowserContext();
    const ctxWaiter = await browser.createBrowserContext();

    const pageRest = await ctxRest.newPage();
    const pageSA = await ctxSA.newPage();
    const pageWaiter = await ctxWaiter.newPage();

    // Verify Waiter has NO Support sidebar item and is denied /support
    await pageWaiter.goto(`${WEB_BASE}/auth`, { waitUntil: "networkidle0" });
    await pageWaiter.type('input[type="email"]', "city@waiter.com");
    await pageWaiter.type('input[type="password"]', TEST_PASSWORD);
    await pageWaiter.click('button[type="submit"]');
    await pageWaiter.waitForFunction(() => location.pathname !== "/auth", { timeout: 15000 });
    await pageWaiter.goto(`${WEB_BASE}/support`, { waitUntil: "networkidle0" });
    await sleep(800);
    const waiterHasSupportNav = await pageWaiter.evaluate(() =>
      Boolean(document.querySelector('a[href="/support"]')),
    );
    if (waiterHasSupportNav || pageWaiter.url().endsWith("/support")) {
      throw new Error(`Waiter should not access /support: ${pageWaiter.url()}`);
    }
    await capture(pageWaiter, "02-waiter-support-denied.png");
    record("Waiter role denied Support route and sidebar item", "PASS");
    await ctxWaiter.close();

    // Session 1: Restaurant Admin login -> Support
    await pageRest.goto(`${WEB_BASE}/auth`, { waitUntil: "networkidle0" });
    await pageRest.type('input[type="email"]', "city@admin.com");
    await pageRest.type('input[type="password"]', TEST_PASSWORD);
    await pageRest.click('button[type="submit"]');
    await pageRest.waitForFunction(() => location.pathname === "/" || location.pathname === "/dashboard", {
      timeout: 15000,
    });
    await pageRest.waitForSelector('a[href="/support"]', { timeout: 15000 });
    await pageRest.click('a[href="/support"]');
    await pageRest.waitForSelector('[data-testid="new-support-conversation-btn"]', { timeout: 15000 });
    await pageRest.waitForFunction(
      () =>
        document.querySelector('[data-testid="support-connection-status"]')?.textContent?.includes("Live"),
      { timeout: 15000 },
    );
    await capture(pageRest, "01-restaurant-support-initial.png");

    // Session 2: Super Admin login -> Support Inbox
    await pageSA.goto(`${WEB_BASE}/super-admin`, { waitUntil: "networkidle0" });
    await pageSA.type('input[placeholder="superadmin"]', SA_USERNAME);
    await pageSA.type('input[placeholder="••••••••"]', SA_PASSWORD);
    await pageSA.click('button[type="submit"]');
    await pageSA.waitForSelector('a[href="/super-admin?tab=inbox"]', { timeout: 15000 }).catch(async () => {
      // Handle TanStack link href matching
      await pageSA.waitForFunction(
        () => Array.from(document.querySelectorAll("a")).some((a) => a.textContent?.includes("Support Inbox")),
        { timeout: 15000 },
      );
    });
    await pageSA.evaluate(() => {
      const link = Array.from(document.querySelectorAll("a")).find((a) =>
        a.textContent?.includes("Support Inbox"),
      );
      link?.click();
    });
    await pageSA.waitForSelector('[data-testid="sa-inbox-search"]', { timeout: 15000 });
    await pageSA.waitForFunction(
      () =>
        document.querySelector('[data-testid="support-connection-status"]')?.textContent?.includes("Live"),
      { timeout: 15000 },
    );
    await capture(pageSA, "03-superadmin-inbox-initial.png");

    // Test 1: Restaurant creates ticket -> SuperAdmin receives WITHOUT refresh
    const ticketSubject = "Kitchen Printer Offline LIVE QA";
    const firstMsg = "Printer station 1 stopped responding mid-rush.";
    await pageRest.click('[data-testid="new-support-conversation-btn"]');
    await pageRest.waitForSelector('[data-testid="new-conv-subject-input"]');
    await pageRest.type('[data-testid="new-conv-subject-input"]', ticketSubject);
    await pageRest.type('[data-testid="new-conv-message-input"]', firstMsg);
    await pageRest.click('[data-testid="new-conv-submit-btn"]');

    await pageRest.waitForFunction(
      (subj) => document.querySelector('[data-testid="active-conv-subject"]')?.textContent?.includes(subj),
      { timeout: 15000 },
      ticketSubject,
    );

    // Without refreshing pageSA, verify ticket appears in Super Admin inbox
    await pageSA.waitForFunction(
      (subj) => document.body.innerText.includes(subj) && document.body.innerText.includes("Hotel City Support QA"),
      { timeout: 15000 },
      ticketSubject,
    );
    await capture(pageSA, "04-realtime-restaurant-to-superadmin.png");
    record("Live delivery: Restaurant message reaches Super Admin inbox without refresh", "PASS");

    // SuperAdmin opens the new conversation & verifies unread drops to 0, then replies
    const convId = await pageSA.evaluate((subj) => {
      const item = Array.from(document.querySelectorAll('[data-testid^="sa-conv-item-"]')).find((el) =>
        el.textContent?.includes(subj),
      );
      item?.click();
      return item?.getAttribute("data-testid")?.replace("sa-conv-item-", "") || null;
    }, ticketSubject);
    if (!convId) throw new Error("Could not locate new conversation ID in SuperAdmin inbox");

    await pageSA.waitForFunction(
      (subj) => document.querySelector('[data-testid="sa-active-conv-subject"]')?.textContent?.includes(subj),
      { timeout: 10000 },
      ticketSubject,
    );
    await sleep(600);

    const saReply1 = "On it — restarting print spooler remotely now.";
    await pageSA.type('[data-testid="sa-reply-input"]', saReply1);
    await pageSA.evaluate(() => {
      document.querySelector('[data-testid="sa-send-reply-btn"]')?.click();
    });

    // WITHOUT refreshing pageRest, assert reply appears and status updates to Awaiting Restaurant
    await pageRest.waitForFunction(
      (msg) =>
        document.querySelector('[data-testid="restaurant-message-thread"]')?.textContent?.includes(msg) &&
        document.body.innerText.includes("Awaiting Restaurant"),
      { timeout: 15000 },
      saReply1,
    );
    await capture(pageRest, "05-realtime-superadmin-reply-to-restaurant.png");
    record("Live delivery: Super Admin reply appears in Restaurant thread & status updates without refresh", "PASS");

    // Test unread badge sync while Restaurant navigates to Overview (/)
    await pageRest.evaluate(() => {
      const overviewLink = Array.from(document.querySelectorAll("a")).find((a) =>
        a.getAttribute("href") === "/" || a.textContent?.trim() === "Overview",
      );
      overviewLink?.click();
    });
    await sleep(600);
    const saReply2 = "Please confirm if test ticket printed.";
    await pageSA.type('[data-testid="sa-reply-input"]', saReply2);
    await pageSA.evaluate(() => {
      document.querySelector('[data-testid="sa-send-reply-btn"]')?.click();
    });

    await pageRest.waitForFunction(
      () => document.querySelector('[data-testid="sidebar-unread-support"]')?.textContent?.trim() === "1",
      { timeout: 15000 },
    );
    await capture(pageRest, "06-restaurant-sidebar-unread-badge.png");
    record("Live unread badge synchronizes on sidebar when navigating away", "PASS");

    // Navigate back to Support -> auto clears unread
    await pageRest.evaluate(() => {
      document.querySelector('a[href="/support"]')?.click();
    });
    await pageRest.waitForSelector('[data-testid="restaurant-reply-input"]', { timeout: 10000 });
    await pageRest.waitForFunction(
      () => !document.querySelector('[data-testid="sidebar-unread-support"]'),
      { timeout: 10000 },
    );

    // Test Offline Draft Preservation & Reconnect Cursor Sync (Zero Duplicates)
    await pageRest.setOfflineMode(true);
    await sleep(500);
    const offlineDraftText = "Offline draft message sent after reconnection";
    await pageRest.type('[data-testid="restaurant-reply-input"]', offlineDraftText);
    await pageRest.click('[data-testid="restaurant-send-reply-btn"]');
    await sleep(800);
    await capture(pageRest, "07-offline-draft-preserved.png");

    // While Restaurant is offline, Super Admin sends another message
    const whileOfflineReply = "Message dispatched while restaurant was disconnected.";
    await pageSA.type('[data-testid="sa-reply-input"]', whileOfflineReply);
    await pageSA.evaluate(() => {
      document.querySelector('[data-testid="sa-send-reply-btn"]')?.click();
    });
    await sleep(800);

    // Bring Restaurant back online, retry/sync cleanly
    await pageRest.setOfflineMode(false);
    await sleep(500);
    await pageRest.evaluate(() => {
      const retryBtn = Array.from(document.querySelectorAll("button")).find((b) =>
        b.textContent?.includes("Retry"),
      );
      retryBtn?.click();
      const syncBtn = Array.from(document.querySelectorAll("button")).find((b) =>
        b.textContent?.includes("Sync"),
      );
      syncBtn?.click();
    });

    await pageRest.waitForFunction(
      (m1, m2) => {
        const text = document.querySelector('[data-testid="restaurant-message-thread"]')?.textContent || "";
        return text.includes(m1) && text.includes(m2);
      },
      { timeout: 20000 },
      whileOfflineReply,
      offlineDraftText,
    );
    const dupCounts = await pageRest.evaluate(
      (m1, m2) => {
        const nodes = Array.from(document.querySelectorAll('[data-testid="restaurant-message-thread"] p'));
        return {
          c1: nodes.filter((n) => n.textContent?.includes(m1)).length,
          c2: nodes.filter((n) => n.textContent?.includes(m2)).length,
        };
      },
      whileOfflineReply,
      offlineDraftText,
    );
    if (dupCounts.c1 !== 1 || dupCounts.c2 !== 1) {
      throw new Error(`Duplicate messages detected after reconnect: ${JSON.stringify(dupCounts)}`);
    }
    await capture(pageRest, "08-reconnected-synced-no-duplicates.png");
    record("Offline draft preserved & reconnect cursor syncs missed messages with zero duplicates", "PASS");

    // Test Resolution & Reopening Sync Across Both Sessions Without Refresh
    await pageSA.click('[data-testid="sa-resolve-btn"]');
    await pageRest.waitForFunction(
      () => Boolean(document.querySelector('[data-testid="restaurant-reopen-panel"]')),
      { timeout: 15000 },
    );
    await capture(pageRest, "09-resolved-synchronized.png");

    const reopenMsg = "Reopening ticket: printer stopped again.";
    await pageRest.type('[data-testid="restaurant-reopen-input"]', reopenMsg);
    await pageRest.click('[data-testid="restaurant-reopen-submit"]');

    await pageSA.waitForFunction(
      (msg) =>
        document.querySelector('[data-testid="sa-message-thread"]')?.textContent?.includes(msg) &&
        document.body.innerText.includes("Awaiting Support"),
      { timeout: 15000 },
      reopenMsg,
    );
    await capture(pageSA, "10-reopened-synchronized.png");
    record("Resolution and Reopening synchronize in real time across both sessions", "PASS");

    // Test Real Escalation Worker + Local Mail Sink + Live UI Update (no refresh!)
    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "AwaitingSupportSinceUtc" = NOW() - interval '11 minutes', "EscalationDeadlineUtc" = NOW() - interval '1 minute' WHERE "Id" = '${convId}';
       UPDATE "SupportEscalationLogs" SET "ScheduledDeadlineUtc" = NOW() - interval '1 minute' WHERE "ConversationId" = '${convId}' AND "Status" = 0;`,
    );

    await pageSA.waitForFunction(
      () => document.body.innerText.includes("Escalated (10m+)"),
      { timeout: 15000 },
    );
    const mailFiles = fs.readdirSync(MAIL_SINK_DIR).filter((f) => f.endsWith(".json"));
    if (mailFiles.length < 1) throw new Error("No escalation email written to local mail sink!");
    const emailJson = JSON.parse(fs.readFileSync(path.join(MAIL_SINK_DIR, mailFiles[0]), "utf8"));
    if (emailJson.to !== "support@neorizsolutions.com") {
      throw new Error(`Wrong escalation recipient: ${emailJson.to}`);
    }
    fs.copyFileSync(path.join(MAIL_SINK_DIR, mailFiles[0]), path.join(QA_DIR, "captured-escalation-email.json"));
    await capture(pageSA, "11-live-escalation-ui-and-mail-sink.png");
    record("10-minute Escalation Worker dispatches email to support@neorizsolutions.com & updates live UI", "PASS", mailFiles[0]);

    // Test Live Session Revocation on Connected Browser Session
    const ctxRevoke = await browser.createBrowserContext();
    const pageRevoke = await ctxRevoke.newPage();
    await pageRevoke.goto(`${WEB_BASE}/auth`, { waitUntil: "networkidle0" });
    await pageRevoke.type('input[type="email"]', "city.revoke@admin.com");
    await pageRevoke.type('input[type="password"]', TEST_PASSWORD);
    await pageRevoke.click('button[type="submit"]');
    await pageRevoke.waitForSelector('a[href="/support"]', { timeout: 15000 });
    await pageRevoke.click('a[href="/support"]');
    await pageRevoke.waitForFunction(
      () =>
        document.querySelector('[data-testid="support-connection-status"]')?.textContent?.includes("Live"),
      { timeout: 15000 },
    );

    // Deactivate revocable employee -> active browser SignalR disconnects & UI blocks access immediately
    const deactRes = await apiJson(`/employees/${revocableId}/status`, {
      method: "PATCH",
      token: adminAToken,
      body: { isActive: false },
    });
    if (!deactRes.ok) throw new Error("Failed deactivating revocable employee");

    await pageRevoke.waitForFunction(
      () => document.body.innerText.includes("Support Access Revoked"),
      { timeout: 10000 },
    );
    await capture(pageRevoke, "12-session-revoked-browser.png");
    record("Already-connected browser session immediately loses access on employee revocation", "PASS");
    await ctxRevoke.close();
  } finally {
    if (browser) await browser.close().catch(() => {});
    killProcessTree(webProc);
    killProcessTree(apiProc);
    if (dbCreated) {
      try {
        runPsql("postgres", `DROP DATABASE IF EXISTS "${DB_NAME}" WITH (FORCE);`);
      } catch {}
    }
    try {
      fs.rmSync(MAIL_SINK_DIR, { recursive: true, force: true });
    } catch {}
  }

  const reportLines = [
    `# TABLO Support Messaging Verification Report`,
    ``,
    `**Generated At:** ${new Date().toISOString()}`,
    `**Branch:** Rizal-Rstrct-1`,
    `**QA Artifacts:** \`${QA_DIR}\``,
    `**Local Mail Sink Evidence:** \`${MAIL_SINK_DIR}\``,
    ``,
    `## Verification Matrix`,
    `| Check | Status | Details |`,
    `| --- | --- | --- |`,
    ...checks.map((c) => `| ${c.name} | **${c.status}** | ${c.details || "-"} |`),
    ``,
    `## Screenshots Captured`,
    ...fs
      .readdirSync(QA_DIR)
      .filter((f) => f.endsWith(".png"))
      .sort()
      .map((f) => `- [${f}](file:///${path.join(QA_DIR, f).replace(/\\/g, "/")})`),
  ];

  fs.writeFileSync(path.join(QA_DIR, "REPORT.md"), reportLines.join("\n"));
  console.log(`\nReport written to ${path.join(QA_DIR, "REPORT.md")}`);

  const failures = checks.filter((c) => c.status !== "PASS");
  process.exit(failures.length > 0 ? 1 : 0);
}

main().catch((err) => {
  console.error("Verification failed:", err);
  process.exit(1);
});
