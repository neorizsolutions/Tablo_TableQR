import fs from "node:fs";
import net from "node:net";
import path from "node:path";
import { execFileSync, spawn } from "node:child_process";

const PG_BIN = "C:\\Program Files\\PostgreSQL\\17\\bin";
const PSQL = path.join(PG_BIN, "psql.exe");

const PG_HOST = "127.0.0.1";
const PG_PORT = 5432;
const PG_USER = "postgres";
const PG_PASS = process.env.PGPASSWORD || "postgres";

const API_PORT_1 = 5191;
const API_PORT_2 = 5192;
const API_BASE_1 = `http://127.0.0.1:${API_PORT_1}/api`;
const API_BASE_2 = `http://127.0.0.1:${API_PORT_2}/api`;

const RUN_STAMP = new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14);
const DB_NAME = `tablo_esc_matrix_${RUN_STAMP}`;
const QA_DIR = path.resolve(`qa/support-messaging/matrix-${RUN_STAMP}`);
fs.mkdirSync(QA_DIR, { recursive: true });

const TEST_PASS = `MatrixQA#${RUN_STAMP.slice(-4)}!`;
const SA_USER = "superadmin-matrix";
const SA_PASS = `SaMatrix#${RUN_STAMP.slice(-4)}!`;

const results = [];
function record(name, status, details = "") {
  results.push({ name, status, details });
  console.log(`[${status}] ${name}${details ? ` — ${details}` : ""}`);
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
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

function runPsql(dbName, sql) {
  return execFileSync(
    PSQL,
    ["-h", PG_HOST, "-p", String(PG_PORT), "-U", PG_USER, "-d", dbName, "-t", "-A", "-v", "ON_ERROR_STOP=1", "-c", sql],
    { encoding: "utf8", env: { ...process.env, PGPASSWORD: PG_PASS } },
  ).trim();
}

async function waitForHttp(url, timeoutMs = 60000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch {}
    await sleep(400);
  }
  throw new Error(`Timed out waiting for ${url}`);
}

async function apiJson(base, urlPath, { method = "GET", token, body } = {}) {
  const res = await fetch(`${base}${urlPath}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  return { ok: res.ok, status: res.status, data: text ? JSON.parse(text) : null };
}

function startSmtpSinkServer() {
  const emails = [];
  let failRemaining = 0;
  let permanentFail = false;
  let attempts = 0;

  const server = net.createServer((socket) => {
    socket.on("error", () => {});
    let buffer = "";
    let inData = false;
    let currentTo = [];
    let currentFrom = "";
    let dataBuffer = "";

    socket.write("220 tablo-test-smtp ESMTP\r\n");

    socket.on("data", (chunk) => {
      buffer += chunk.toString("utf8");
      while (true) {
        if (inData) {
          const endIdx = buffer.indexOf("\r\n.\r\n");
          if (endIdx === -1) break;
          dataBuffer += buffer.slice(0, endIdx);
          buffer = buffer.slice(endIdx + 5);
          inData = false;
          attempts++;

          if (permanentFail) {
            socket.write("550 5.7.1 Simulated permanent SMTP failure\r\n");
          } else if (failRemaining > 0) {
            failRemaining--;
            socket.write("451 4.3.0 Simulated transient SMTP failure\r\n");
          } else {
            emails.push({
              from: currentFrom,
              to: currentTo,
              raw: dataBuffer,
              receivedAtUtc: new Date().toISOString(),
            });
            socket.write("250 2.0.0 OK queued\r\n");
          }
          dataBuffer = "";
          continue;
        }

        const lineEnd = buffer.indexOf("\r\n");
        if (lineEnd === -1) break;
        const line = buffer.slice(0, lineEnd);
        buffer = buffer.slice(lineEnd + 2);
        const upper = line.toUpperCase();

        if (upper.startsWith("EHLO") || upper.startsWith("HELO")) {
          socket.write("250-tablo-test-smtp Hello\r\n250 OK\r\n");
        } else if (upper.startsWith("MAIL FROM:")) {
          currentFrom = line.slice(10).trim();
          socket.write("250 OK\r\n");
        } else if (upper.startsWith("RCPT TO:")) {
          currentTo.push(line.slice(8).trim());
          socket.write("250 OK\r\n");
        } else if (upper === "DATA") {
          inData = true;
          socket.write("354 Start mail input; end with <CRLF>.<CRLF>\r\n");
        } else if (upper === "QUIT") {
          socket.write("221 Bye\r\n");
          socket.end();
          break;
        } else if (upper === "RSET") {
          currentTo = [];
          currentFrom = "";
          socket.write("250 OK\r\n");
        } else {
          socket.write("250 OK\r\n");
        }
      }
    });
  });

  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => {
      const addr = server.address();
      resolve({
        port: addr.port,
        emails,
        getAttempts: () => attempts,
        setFailRemaining: (n) => { failRemaining = n; },
        setPermanentFail: (b) => { permanentFail = b; },
        close: () => server.close(),
      });
    });
  });
}

async function waitUntil(fn, timeoutMs = 15000, msg = "Condition timed out") {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await fn()) return;
    await sleep(400);
  }
  throw new Error(msg);
}

async function main() {
  let api1 = null;
  let api2 = null;
  let smtp = null;
  let dbCreated = false;

  try {
    console.log("=== 1. Starting local TCP SMTP server & disposable PostgreSQL 17 DB ===");
    smtp = await startSmtpSinkServer();
    runPsql("postgres", `CREATE DATABASE "${DB_NAME}";`);
    dbCreated = true;

    const commonEnv = {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      ConnectionStrings__DefaultConnection: `Host=${PG_HOST};Port=${PG_PORT};Database=${DB_NAME};Username=${PG_USER};Password=${PG_PASS}`,
      Jwt__Key: "matrix-verification-signing-key-for-support-module-2026-abc",
      Jwt__Issuer: "TabloApi",
      Jwt__Audience: "TabloApp",
      AllowedOrigins__0: "http://127.0.0.1:4188",
      SuperAdmin__Username: SA_USER,
      SuperAdmin__Password: SA_PASS,
      Email__SmtpHost: "127.0.0.1",
      Email__SmtpPort: String(smtp.port),
      Email__SmtpEnableSsl: "false",
      Email__FromEmail: "no-reply@notify.neorizsolutions.com",
      Email__FromName: "Tablo Escalations",
      SeedDummyData: "false",
    };

    // Instance 1 migrates + starts worker 1
    api1 = spawn("dotnet", ["exec", path.resolve("Tablo.Api/bin/Debug/net8.0/Tablo.Api.dll")], {
      cwd: path.resolve("Tablo.Api"),
      env: { ...commonEnv, ASPNETCORE_URLS: `http://127.0.0.1:${API_PORT_1}`, AutoMigrate: "true" },
      stdio: "pipe",
    });
    api1.stdout.on("data", (d) => process.stdout.write(`[API1] ${d}`));
    api1.stderr.on("data", (d) => process.stderr.write(`[API1 ERR] ${d}`));
    api1.on("exit", (code) => console.log(`[API1 EXIT] code=${code}`));
    await waitForHttp(`http://127.0.0.1:${API_PORT_1}/health`, 60000);

    // Instance 2 connects to SAME DB + starts concurrent worker 2
    api2 = spawn("dotnet", ["exec", path.resolve("Tablo.Api/bin/Debug/net8.0/Tablo.Api.dll")], {
      cwd: path.resolve("Tablo.Api"),
      env: { ...commonEnv, ASPNETCORE_URLS: `http://127.0.0.1:${API_PORT_2}`, AutoMigrate: "false" },
      stdio: "pipe",
    });
    api2.stdout.on("data", (d) => process.stdout.write(`[API2] ${d}`));
    api2.stderr.on("data", (d) => process.stderr.write(`[API2 ERR] ${d}`));
    api2.on("exit", (code) => console.log(`[API2 EXIT] code=${code}`));
    await waitForHttp(`http://127.0.0.1:${API_PORT_2}/health`, 60000);
    record("Dual concurrent Tablo.Api workers started against PostgreSQL", "PASS", `smtpPort=${smtp.port}`);

    // Login SA & onboard restaurant
    const saLogin = await apiJson(API_BASE_1, "/sysadmin/login", {
      method: "POST",
      body: { username: SA_USER, password: SA_PASS },
    });
    const saToken = saLogin.data.token;

    await apiJson(API_BASE_1, "/sysadmin/onboard", {
      method: "POST",
      token: saToken,
      body: {
        restaurantName: "Escalation Matrix Bistro",
        adminFirstName: "Matrix",
        adminLastName: "Admin",
        adminEmail: "matrix@admin.com",
        adminPassword: TEST_PASS,
        planCode: "basic",
      },
    });
    const adminLogin = await apiJson(API_BASE_1, "/auth/login", {
      method: "POST",
      body: { email: "matrix@admin.com", password: TEST_PASS },
    });
    const adminToken = adminLogin.data.token;

    // 1. First unanswered message starts 10-minute waiting period
    const c1 = await apiJson(API_BASE_1, "/support/conversations", {
      method: "POST",
      token: adminToken,
      body: {
        subject: "Rule 1+2+4 Escalation Check",
        message: "Initial unanswered ticket",
        clientOperationId: crypto.randomUUID(),
      },
    });
    const conv1Id = c1.data.conversation.id;
    const deadlineDeltaSec = Math.round(
      (new Date(c1.data.conversation.escalationDeadlineUtc).getTime() -
        new Date(c1.data.conversation.awaitingSupportSinceUtc).getTime()) / 1000,
    );
    if (deadlineDeltaSec !== 600 || c1.data.conversation.status !== "AwaitingSupport") {
      throw new Error(`Expected exact 600s deadline, got ${deadlineDeltaSec}s`);
    }
    const period1 = c1.data.conversation.currentWaitingPeriodId;
    record("First restaurant message starts 10-minute waiting period (600s)", "PASS");

    // 2. Follow-up restaurant message preserves original deadline
    await sleep(1200);
    const c1Follow = await apiJson(API_BASE_1, `/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: adminToken,
      body: { message: "Second message while still awaiting support", clientOperationId: crypto.randomUUID() },
    });
    if (
      c1Follow.data.conversation.currentWaitingPeriodId !== period1 ||
      Math.abs(
        new Date(c1Follow.data.conversation.escalationDeadlineUtc).getTime() -
          new Date(c1.data.conversation.escalationDeadlineUtc).getTime(),
      ) > 1
    ) {
      throw new Error(
        `Follow-up restaurant message altered original waiting period deadline! before=${c1.data.conversation.escalationDeadlineUtc} after=${c1Follow.data.conversation.escalationDeadlineUtc}`,
      );
    }
    const logCountBefore = Number(runPsql(DB_NAME, `SELECT COUNT(*) FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv1Id}';`));
    if (logCountBefore !== 1) throw new Error(`Expected 1 escalation log row, got ${logCountBefore}`);
    record("Additional restaurant message preserves original deadline & log", "PASS");

    // 3. Before 10m expires: does not escalate
    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "EscalationDeadlineUtc" = NOW() + interval '25 seconds' WHERE "Id"='${conv1Id}';
       UPDATE "SupportEscalationLogs" SET "ScheduledDeadlineUtc" = NOW() + interval '25 seconds' WHERE "ConversationId"='${conv1Id}';`,
    );
    await sleep(6000);
    if (smtp.emails.length !== 0) throw new Error("Escalated before 10-minute deadline!");
    record("Pre-deadline period sends zero escalation emails", "PASS");

    // 4. At deadline with TWO concurrent workers: escalates ONCE to support@neorizsolutions.com and does NOT count as support reply
    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "AwaitingSupportSinceUtc" = NOW() - interval '10 minutes', "EscalationDeadlineUtc" = NOW() - interval '2 seconds' WHERE "Id"='${conv1Id}';
       UPDATE "SupportEscalationLogs" SET "ScheduledDeadlineUtc" = NOW() - interval '2 seconds' WHERE "ConversationId"='${conv1Id}';`,
    );
    await waitUntil(() => smtp.emails.length === 1, 15000, "Concurrent workers did not dispatch email");
    await sleep(6500); // wait another full worker cycle to verify zero duplicates
    if (smtp.emails.length !== 1) throw new Error(`Expected 1 email, got ${smtp.emails.length}`);
    if (!smtp.emails[0].to.some((t) => t.includes("support@neorizsolutions.com"))) {
      throw new Error(`Unexpected RCPT TO: ${smtp.emails[0].to}`);
    }
    const detailAfterEsc = await apiJson(API_BASE_1, `/support/conversations/${conv1Id}`, { token: adminToken });
    if (detailAfterEsc.data.conversation.status !== "AwaitingSupport" || !detailAfterEsc.data.conversation.isEscalated) {
      throw new Error("Escalation altered AwaitingSupport status incorrectly");
    }
    record("Dual concurrent workers escalate ONCE at deadline without counting as reply", "PASS");

    // 5. Super Admin reply ends waiting period; subsequent restaurant message starts NEW period; early SA reply cancels it
    const saReply = await apiJson(API_BASE_2, `/sysadmin/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: saToken,
      body: { message: "Super Admin responded", clientOperationId: crypto.randomUUID() },
    });
    if (
      saReply.data.conversation.status !== "AwaitingRestaurant" ||
      saReply.data.conversation.currentWaitingPeriodId !== null ||
      saReply.data.conversation.isEscalated !== false
    ) {
      throw new Error("Super Admin reply did not cleanly end waiting period");
    }

    const period2Res = await apiJson(API_BASE_1, `/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: adminToken,
      body: { message: "New follow-up question", clientOperationId: crypto.randomUUID() },
    });
    const period2Id = period2Res.data.conversation.currentWaitingPeriodId;
    if (!period2Id || period2Id === period1) {
      throw new Error("Later restaurant message did not start a new WaitingPeriodId");
    }

    await apiJson(API_BASE_1, `/sysadmin/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: saToken,
      body: { message: "Second SA reply before deadline", clientOperationId: crypto.randomUUID() },
    });
    const cancelledState = runPsql(
      DB_NAME,
      `SELECT "Status" FROM "SupportEscalationLogs" WHERE "WaitingPeriodId"='${period2Id}';`,
    );
    if (cancelledState !== "3") throw new Error(`Expected Status=3 Cancelled, got ${cancelledState}`);
    record("Super Admin reply ends waiting period; subsequent reply starts new period; timely reply cancels", "PASS");

    // 6. Resolution cancels pending escalation; Reopen WITH message starts new waiting period
    const p3Res = await apiJson(API_BASE_1, `/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: adminToken,
      body: { message: "Third waiting period before resolve", clientOperationId: crypto.randomUUID() },
    });
    const p3Id = p3Res.data.conversation.currentWaitingPeriodId;
    await apiJson(API_BASE_1, `/support/conversations/${conv1Id}/resolve`, { token: adminToken, method: "POST", body: {} });
    const p3Status = runPsql(DB_NAME, `SELECT "Status" FROM "SupportEscalationLogs" WHERE "WaitingPeriodId"='${p3Id}';`);
    if (p3Status !== "3") throw new Error("Resolve failed to cancel pending escalation");

    const reopened = await apiJson(API_BASE_1, `/support/conversations/${conv1Id}/reopen`, {
      method: "POST",
      token: adminToken,
      body: { message: "Reopened waiting period", clientOperationId: crypto.randomUUID() },
    });
    if (reopened.data.conversation.status !== "AwaitingSupport" || !reopened.data.conversation.currentWaitingPeriodId) {
      throw new Error("Reopening with message did not start new waiting period");
    }
    record("Resolution cancels pending escalation; Reopen with message starts new period", "PASS");

    // 7. Pre-dispatch state recheck + Stale worker crash recovery
    // Make reopened period into simulated dead-worker stale Processing claim (AttemptCount=1, ClaimedAtUtc=NOW()-3m)
    const reopenPeriodId = reopened.data.conversation.currentWaitingPeriodId;
    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "EscalationDeadlineUtc" = NOW() - interval '1 minute' WHERE "Id"='${conv1Id}';
       UPDATE "SupportEscalationLogs" SET "Status" = 4, "AttemptCount" = 1, "ClaimedBy" = 'crashed-pod', "ClaimedAtUtc" = NOW() - interval '3 minutes', "ScheduledDeadlineUtc" = NOW() - interval '1 minute' WHERE "WaitingPeriodId"='${reopenPeriodId}';`,
    );
    await waitUntil(() => smtp.emails.length === 2, 15000, "Worker failed to recover stale Processing claim");
    record("Worker recovers stale Processing claim after simulated crash/restart", "PASS");

    // Pre-dispatch state recheck: seed Pending escalation row while conversation is already AwaitingRestaurant
    const badPeriodId = crypto.randomUUID();
    await apiJson(API_BASE_1, `/sysadmin/support/conversations/${conv1Id}/messages`, {
      method: "POST",
      token: saToken,
      body: { message: "Move back to AwaitingRestaurant", clientOperationId: crypto.randomUUID() },
    });
    runPsql(
      DB_NAME,
      `INSERT INTO "SupportEscalationLogs" ("Id","ConversationId","WaitingPeriodId","EscalationTargetEmail","ScheduledDeadlineUtc","AttemptCount","Status")
       VALUES ('${crypto.randomUUID()}','${conv1Id}','${badPeriodId}','support@neorizsolutions.com',NOW() - interval '1 minute',0,0);`,
    );
    await waitUntil(
      () => runPsql(DB_NAME, `SELECT "Status" FROM "SupportEscalationLogs" WHERE "WaitingPeriodId"='${badPeriodId}';`) === "3",
      15000,
      "Pre-dispatch recheck did not cancel mismatched escalation log",
    );
    if (smtp.emails.length !== 2) throw new Error("Email sent despite mismatched conversation state!");
    record("Worker pre-dispatch recheck cancels mismatched log without sending email", "PASS");

    // 8. Transient SMTP failure + exponential backoff retry + recovery
    const c2 = await apiJson(API_BASE_1, "/support/conversations", {
      method: "POST",
      token: adminToken,
      body: { subject: "Transient SMTP Failure Ticket", message: "Test retries", clientOperationId: crypto.randomUUID() },
    });
    const conv2Id = c2.data.conversation.id;
    smtp.setFailRemaining(2);
    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "EscalationDeadlineUtc" = NOW() - interval '5 seconds' WHERE "Id"='${conv2Id}';
       UPDATE "SupportEscalationLogs" SET "ScheduledDeadlineUtc" = NOW() - interval '5 seconds' WHERE "ConversationId"='${conv2Id}';`,
    );

    await waitUntil(
      () =>
        runPsql(
          DB_NAME,
          `SELECT ("AttemptCount" = 1 AND "Status" = 2)::text FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv2Id}';`,
        ) === "true",
      15000,
      "Attempt 1 failure not recorded with Status=2",
    );
    const err1 = runPsql(DB_NAME, `SELECT COALESCE("LastError",'') || '|' || "Status" FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv2Id}';`);
    if (!err1.includes("|2")) throw new Error(`Expected Status=2 Failed after Attempt 1, got ${err1}`);

    // Fast-forward Retry 1 backoff
    runPsql(DB_NAME, `UPDATE "SupportEscalationLogs" SET "NextRetryAtUtc" = NOW() - interval '1 second' WHERE "ConversationId"='${conv2Id}';`);
    await waitUntil(
      () =>
        runPsql(
          DB_NAME,
          `SELECT ("AttemptCount" = 2 AND "Status" = 2)::text FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv2Id}';`,
        ) === "true",
      15000,
      "Attempt 2 failure not recorded with Status=2",
    );

    // Fast-forward Retry 2 backoff -> succeeds on Attempt 3
    runPsql(DB_NAME, `UPDATE "SupportEscalationLogs" SET "NextRetryAtUtc" = NOW() - interval '1 second' WHERE "ConversationId"='${conv2Id}';`);
    await waitUntil(
      () => smtp.emails.length === 3 &&
            runPsql(DB_NAME, `SELECT "Status" FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv2Id}';`) === "1",
      15000,
      "Attempt 3 did not recover cleanly",
    );
    record("Transient SMTP failure records errors, retries with backoff, and succeeds on Attempt 3", "PASS");

    // 9. Bounded retries stop at MaxAttempts = 5 on permanent failure
    const c3 = await apiJson(API_BASE_1, "/support/conversations", {
      method: "POST",
      token: adminToken,
      body: { subject: "Permanent Failure Cap", message: "Cap at 5 attempts", clientOperationId: crypto.randomUUID() },
    });
    const conv3Id = c3.data.conversation.id;
    smtp.setPermanentFail(true);

    runPsql(
      DB_NAME,
      `UPDATE "SupportConversations" SET "EscalationDeadlineUtc" = NOW() - interval '5 seconds' WHERE "Id"='${conv3Id}';
       UPDATE "SupportEscalationLogs" SET "ScheduledDeadlineUtc" = NOW() - interval '5 seconds' WHERE "ConversationId"='${conv3Id}';`,
    );

    for (let target = 1; target <= 5; target++) {
      await waitUntil(
        () =>
          runPsql(
            DB_NAME,
            `SELECT ("AttemptCount" >= ${target} AND "Status" = 2)::text FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv3Id}';`,
          ) === "true",
        15000,
        `Attempt ${target} did not fail and record Status=2`,
      );
      if (target < 5) {
        runPsql(DB_NAME, `UPDATE "SupportEscalationLogs" SET "NextRetryAtUtc" = NOW() - interval '1 second' WHERE "ConversationId"='${conv3Id}';`);
      }
    }
    await sleep(6500); // Ensure worker stops trying once AttemptCount === 5
    const capState = runPsql(
      DB_NAME,
      `SELECT "AttemptCount"::text || '|' || "Status"::text || '|' || COALESCE("NextRetryAtUtc"::text,'NULL') FROM "SupportEscalationLogs" WHERE "ConversationId"='${conv3Id}';`,
    );
    if (!capState.startsWith("5|2|NULL")) {
      throw new Error(`Expected bounded failure state 5|2|NULL, got ${capState}`);
    }
    record("Permanent SMTP failure stops strictly at MaxAttempts=5 with NextRetryAtUtc=NULL", "PASS");
  } finally {
    smtp?.close();
    killProcessTree(api2);
    killProcessTree(api1);
    if (dbCreated) {
      try {
        runPsql("postgres", `DROP DATABASE IF EXISTS "${DB_NAME}" WITH (FORCE);`);
      } catch {}
    }
  }

  fs.writeFileSync(
    path.join(QA_DIR, "MATRIX_RESULTS.json"),
    JSON.stringify({ generatedAt: new Date().toISOString(), results }, null, 2),
  );
  console.log(`\nALL MATRIX CHECKS COMPLETED: ${results.filter((r) => r.status === "PASS").length}/${results.length} PASS`);
  process.exit(results.some((r) => r.status !== "PASS") ? 1 : 0);
}

main().catch((err) => {
  console.error("Matrix verification failed:", err);
  process.exit(1);
});
