# TABLO Support Module — End-to-End Verification & Handoff Report

**Generated At:** 2026-11-25T21:08:00+05:30 (`2026-11-25T15:38:00Z`)  
**Target Branch:** `Rizal-Rstrct-1` (Pending release; NOT merged into `dev`, `main`, or `production`)  
**E2E Run Folder:** `qa/support-messaging/20261125152615/`  
**Escalation Matrix Folder:** `qa/support-messaging/matrix-20261125153503/`  
**Escalation Target Email:** `support@neorizsolutions.com`

---

## 1. Executive Verification Summary

| Suite / Area | Result | Executed Counts & Notes |
| --- | --- | --- |
| Two-Session Live Browser E2E + SignalR Wire Security (`scripts/verify-support-messaging.mjs`) | **PASS** | **13 / 13 checks PASS** against live `Tablo.Api`, PostgreSQL 17, Local Mail Sink, Vite build, and simultaneous Puppeteer sessions |
| Dual-Worker Escalation Timing, Concurrency, Crash Recovery, Backoff Retry & Cap Matrix (`scripts/verify-support-escalation-matrix.mjs`) | **PASS** | **11 / 11 checks PASS** against two concurrent `Tablo.Api` instances on PostgreSQL 17 and a live TCP SMTP fault-injection server |
| Frontend Unit Tests (`support-messaging.test.mjs`, `menu-access.test.mjs`, `sidebar-and-navigation.test.mjs`, `navigation-access-compat.test.mjs`) | **PASS** | **24 / 24 tests PASS** (`node --test`) |
| Builds & Static Typechecking (`dotnet build Tablo.Api`, `tsc --noEmit`, `vite build`) | **PASS** | `Tablo.Api`: 0 errors; `tsc`: 0 errors; `vite build`: succeeded |
| xUnit Runner (`dotnet test Tablo.Api.Tests --filter SupportModuleTests`) | **BLOCKED (Host Policy)** | `Tablo.Api.Tests.dll` compiled cleanly (0 errors), but execution inside `vstest` / `testhost` was blocked by Windows Application Control policy (`0x800711C7`) on unsigned dynamic test assembly loading. Every workflow, escalation timing rule, concurrency lock, crash recovery, retry cap, local mail sink, SignalR security rule, and EF migration check was executed live against compiled `Tablo.Api.dll` + PostgreSQL 17 instead. |

---

## 2. Detailed Verification Matrix (PASS / FAIL / BLOCKED)

| Check | Status | Evidence / Notes |
| --- | --- | --- |
| Isolated PostgreSQL 17 Schema Migration (`20261125200000_AddSupportModuleAndEscalations`) | **PASS** | Applied cleanly on fresh PostgreSQL 17 databases (`tablo_support_e2e_20261125152615`, `tablo_esc_matrix_20261125153503`) |
| Role-Based Menu & Route Protection (`Admin`, `Owner` vs `Waiter`, `Kitchen`, `Cashier`) | **PASS** | Waiter denied `/support` route, `/api/support/*`, and sidebar link; Admin/Owner retain defaults; Cashier/Waiter/Kitchen defaults preserved |
| Foreign-Tenant API Isolation & Custom `AllowedMenuIds` Override | **PASS** | Restaurant B cannot access Restaurant A tickets; SignalR hub rejects foreign restaurant & conversation subscriptions |
| SignalR Platform & Cross-Tenant Group Rejection (Wire Level) | **PASS** | Restaurant Admin invoking `JoinSuperAdminInbox`, `JoinRestaurantSupport(restB)`, or `JoinConversation(convB)` throws `HubException` |
| Live Restaurant -> Super Admin Delivery (No Refresh) | **PASS** | Creating `"Kitchen Printer Offline LIVE QA"` appeared immediately in Super Admin Inbox with `Hotel City Support QA`, `Awaiting Support`, and unread badge `1` |
| Live Super Admin -> Restaurant Reply & Status Sync (No Refresh) | **PASS** | Reply immediately rendered in Restaurant thread; status updated to `Awaiting Restaurant`; active waiting timer stopped |
| Unread Count Synchronization When Navigating Away | **PASS** | Restaurant on `/` (`Overview`) immediately showed `1` badge on `Support` sidebar link on new Super Admin reply; cleared automatically upon opening thread |
| Offline Draft Preservation & Reconnect Cursor Sync (Zero Duplicates) | **PASS** | Draft + failed optimistic bubble (`Retry`) preserved under `setOfflineMode(true)`; messages sent by Super Admin during disconnect appeared cleanly on reconnect without duplicates |
| Repeated Send / `ClientOperationId` Idempotency | **PASS** | Retrying `POST /api/support/conversations` and `POST .../messages` with identical `ClientOperationId` returned `deduplicated: true` with 1 persisted row |
| Live Resolution & Reopening Synchronization (No Refresh) | **PASS** | Super Admin resolving synced `Resolved` + Reopen UI to Restaurant; Restaurant reopening with message synced `Awaiting Support` + unread `1` back to Super Admin |
| Already-Connected Client Session Revocation | **PASS** | Deactivating an Admin with an active browser session immediately aborted their SignalR connection (`AccessRevoked`) and locked the Support view without manual refresh |
| 10-Minute Escalation Rule 1 & 2 (Start Period + Preserve Original Deadline) | **PASS** | First message starts 600s deadline; second restaurant message preserves exact `CurrentWaitingPeriodId` & `EscalationDeadlineUtc` with 1 `Pending` log row |
| 10-Minute Escalation Rule 3 & 4 (Pre-Deadline Silence + Post-Deadline Single Dispatch) | **PASS** | Pre-deadline worker ticks send 0 emails; past deadline, dual concurrent `Tablo.Api` instances atomically claim and send 1 email to `support@neorizsolutions.com`; status remains `AwaitingSupport` (escalation does not count as reply) |
| 10-Minute Escalation Rule 5, 6, 7 (SA Reply Ends Period + Later Message Starts New Period + Timely SA Reply Cancels) | **PASS** | Confirmed `CurrentWaitingPeriodId` cleared on SA reply, new ID allocated on subsequent restaurant message, and `SupportEscalationLogs.Status = Cancelled (3)` when SA replies before deadline |
| 10-Minute Escalation Rule 8 (Resolve Cancels Escalation; Reopen With Message Restarts Period) | **PASS** | Resolving cancelled `Pending` escalation log (`Status = 3`); reopening with a follow-up message transitioned back to `AwaitingSupport` with a new `WaitingPeriodId` and dispatched upon deadline |
| Dual-Worker Crash Recovery (`Processing` Stale Claim Lease) | **PASS** | Simulated dead pod (`Status = 4`, `ClaimedAtUtc = NOW() - 3m`) cleanly reclaimed and dispatched once by surviving worker |
| Pre-Dispatch Conversation State Recheck | **PASS** | Seeded stale `Pending` escalation row for an `AwaitingRestaurant` conversation -> worker rechecked conversation state, marked log `Cancelled (3)`, and sent 0 emails |
| Transient SMTP Failure, Exponential Backoff Retry & Attempt 3 Recovery | **PASS** | Configured local TCP SMTP server to reject first 2 attempts (`451`) -> worker recorded `AttemptCount`, `LastError`, `NextRetryAtUtc`, and succeeded on Attempt 3 |
| Bounded Retries Stop Strictly at `MaxAttempts = 5` | **PASS** | Permanent SMTP failure (`550`) stopped cleanly at `AttemptCount = 5`, `Status = Failed (2)`, `NextRetryAtUtc = NULL` |
| xUnit In-Process Runner (`dotnet test`) | **BLOCKED** | Blocked locally by OS Application Control (`0x800711C7`) loading `Tablo.Api.Tests.dll` inside `testhost`; verified via live compiled `Tablo.Api.dll` runners instead |

---

## 3. Real-Time Transport & Reconnection Architecture
- **Hub Endpoint:** `/hubs/support` (`Tablo.Api.Hubs.SupportHub`).
- **Event Contract:** Single structured `"SupportEvent"` envelope (`EventId`, `EventType`, `OccurredAtUtc`, `ConversationId`, `RestaurantId`, `ConversationVersion`, `Conversation`, `Message`, `Escalation`, `ReadByRole`) committed **after** DB transaction commit, paired with `"AccessRevoked"` push on credential/role/menu/tenant revocation.
- **Reliability Patterns:**
  - Client subscribes to SignalR handlers BEFORE `.start()`, joins inbox/conversation groups first, and immediately runs cursor delta reconciliation (`sinceUtc` / `afterSequence`).
  - Frontend caches deduplicate by `eventId`, reject stale `conversation.version` regressions, and reconcile optimistic `"sending"`/`"failed"` messages cleanly against confirmed server `id` / `clientOperationId`.
  - Unsent drafts and pending `clientOperationId` values persist in `localStorage`; UI exposes live `Connecting`, `Live`, `Reconnecting`, `Offline (Fallback sync)`, and `Access Revoked` states with manual `Sync` + automatic fallback polling when SignalR is unavailable.
- **Topology:** Production runs a single `tablo-api` container behind Nginx (`scripts/remote-deploy.sh`, `nginx-default.conf`). Scaling horizontally to multiple API replicas later will require adding a SignalR backplane (Redis or Azure SignalR Service) so group broadcasts reach connections across instances.

---

## 4. Escalation & Email Delivery Evidence
- **Local Mail Sink Capture:** `qa/support-messaging/20261125152615/captured-escalation-email.json`
  - Recipient: `support@neorizsolutions.com`
  - Sender: `no-reply@notify.neorizsolutions.com` (`Tablo Support Escalation`)
  - Subject: `[TABLO Urgent Escalation] Unanswered Support Ticket: Kitchen Printer Offline LIVE QA`
  - Body includes Restaurant Name (`Hotel City Support QA`), Restaurant ID, Conversation ID, Subject, Waiting Started UTC, Waiting Duration (`11m 3s`), and deep-link `.../super-admin?tab=inbox&conversationId=...` while keeping private message body text out of alert emails.
- **Matrix Evidence:** `qa/support-messaging/matrix-20261125153503/MATRIX_RESULTS.json` (11/11 checks passing across PostgreSQL 17 + dual concurrent workers + TCP SMTP sink).

---

## 5. Migration & Configuration Requirements (Pending Production Release)
1. **Database Migration:** Apply EF Core migration `20261125200000_AddSupportModuleAndEscalations` (creates `SupportConversations`, `SupportMessages`, `SupportEscalationLogs`, `SupportAuditLogs`, tenant composite FKs, and filtered unique idempotency indexes). **Do NOT apply to production yet per release hold.**
2. **Reverse Proxy (Nginx):** `nginx-default.conf` (gitignored host config) must proxy `/hubs/` (not just `/hubs/order`) with WebSocket `Upgrade` headers (`Connection "Upgrade"`, `proxy_http_version 1.1`) so `/hubs/support` upgrades to WebSockets cleanly in production.
3. **Configuration Keys:**
   - Production email: existing `Email__BrevoApiKey`, `Email__FromEmail` (`no-reply@notify.neorizsolutions.com`), `Email__FromName`.
   - Deep-link base URL (optional override, defaults to production URL): `App__FrontendBaseUrl` (`https://tablopos.neorizsolutions.com`).
   - Local development / QA mail capture (optional): `Email__LocalSinkDirectory` or `Email__SmtpEnableSsl=false` for local SMTP sinks.

---

## 6. Numbered Screenshots (`qa/support-messaging/20261125152615/`)
- [01-restaurant-support-initial.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/01-restaurant-support-initial.png) — Restaurant Admin `/support` view with Live SignalR status
- [02-waiter-support-denied.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/02-waiter-support-denied.png) — Waiter role denied `/support` access and sidebar item
- [03-superadmin-inbox-initial.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/03-superadmin-inbox-initial.png) — Super Admin Support Inbox initial state
- [04-realtime-restaurant-to-superadmin.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/04-realtime-restaurant-to-superadmin.png) — Restaurant message arrives live in Super Admin inbox without refresh
- [05-realtime-superadmin-reply-to-restaurant.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/05-realtime-superadmin-reply-to-restaurant.png) — Super Admin reply & `Awaiting Restaurant` sync live to Restaurant thread
- [06-restaurant-sidebar-unread-badge.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/06-restaurant-sidebar-unread-badge.png) — Sidebar `Support` unread badge updates live while user is on `/` Overview
- [07-offline-draft-preserved.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/07-offline-draft-preserved.png) — Draft & failed Retry state preserved while offline
- [08-reconnected-synced-no-duplicates.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/08-reconnected-synced-no-duplicates.png) — Missed Super Admin message + retried draft reconciled in order with zero duplicates
- [09-resolved-synchronized.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/09-resolved-synchronized.png) — Super Admin resolution synchronizes live `Resolved` + Reopen UI to Restaurant
- [10-reopened-synchronized.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/10-reopened-synchronized.png) — Restaurant reopen with message synchronizes live back to `Awaiting Support`
- [11-live-escalation-ui-and-mail-sink.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/11-live-escalation-ui-and-mail-sink.png) — Real-time `Escalated (10m+)` UI badge + email captured in Local Mail Sink while status preserves `Awaiting Support`
- [12-session-revoked-browser.png](file:///D:/Projects/tablo/qa/support-messaging/20261125152615/12-session-revoked-browser.png) — Already-connected browser session immediately revoked upon employee deactivation
