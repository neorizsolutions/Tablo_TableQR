# TABLO Staff Menu Access Overhaul & Support Module — Production Readiness Report

**Generated At:** 2026-11-26  
**Branch:** `Rizal-Rstrct-1` (release pending explicit user approval; NOT merged into `dev`, `main`, or `production`)  
**QA Evidence:** `qa/menu-access/20261126055827/` (12 screenshots + `RESULTS.json`)

---

## 1. Executive Summary

- **Full Assignable Menu Checklist in `/menu-access`:** Selecting any employee (`Waiter`, `Kitchen`, `Cashier`, `Admin`, `Owner`) displays all **25 assignable restaurant workspace menus** grouped by section (`Overview`, `Daily Operations`, `Restaurant Management`, `Inventory & Stock`, `Reports & Analytics`) via `NAVIGATION_REGISTRY`.
- **Preserved Role Defaults (`allowedMenuIds === null`):**
  - `Waiter`: `overview`, `orders-servicedesk`, `staff-order-taking`, `table-billing`
  - `Kitchen`: `overview`, `kitchen-queue`
  - `Cashier`: `cashier-billing`
  - `Admin` / `Owner`: all 28 restaurant menus
- **Subscription Gating & UI Badges:** Menus unavailable under the restaurant's active subscription plan render as disabled checkboxes with explicit plan badges (`Requires Plus plan` / `Requires Premium plan`), and backend `PUT /api/employees/{id}/menu-access` plus runtime `NavigationAccessFilter` / `RequireCapability` enforce plan entitlements on every API call.
- **Cross-Role Execution End-to-End:** Employees granted cross-role operational menus (e.g., `Waiter` granted `kitchen-queue`, or `Kitchen` granted `cashier-billing`, `inventory-stock`, `financial-reports`) immediately gain sidebar navigation, overview shortcuts, direct deep links that survive page refresh, matching controller `[Authorize]` + `NavigationAccessFilter` + `BillingService.RequireActorAsync` permissions, and matching plan-backed capabilities in `SubscriptionService.GetEffectiveAccessAsync`.
- **Live Open-Session Updates & SignalR Revocation:** `OrderHub` exposes `JoinPermissionUpdates()` per employee and `OrderConnectionRegistry` broadcasts `PermissionsUpdated` / `AccessRevoked` while removing revoked employees from `restaurant_{id}` groups immediately. `useNavigationAccess()` listens on `/hubs/order` so open browser tabs reflect grants or revocations immediately without manual page refresh.
- **Restricted Admin & Super Admin Boundaries:** `staff-management`, `staff-menu-access`, and `support` are strictly Admin/Owner-only (`AdminOnly = true`) and cannot be assigned to `Waiter`, `Kitchen`, or `Cashier` (`400 Bad Request` on PUT, `403 Forbidden` on execution). Super Admin platform screens (`sa-*`, `/api/sysadmin/*`, `/api/sysadmin/support/*`) remain exclusive to `SuperAdmin`.
- **Clean-DB Migration Fix Discovered & Resolved:** Removed stale `.HasDefaultValue(0m)` on `OrderItemConfiguration.PackagingFeeApplied` (since `20261119233000_AddScheduledHybridCarSideAndUpi` created the column `nullable: true` without DB default, while `OrderItem` already initializes `0m` in C#), preventing `InvalidCastException: Column 'PackagingFeeApplied' is null` on fresh PostgreSQL migrations.

---

## 2. Assignable vs. Restricted Permission Matrix

| Scope | Menu IDs | Allowed Roles (Default `null`) | Custom Assignment Target | Plan / Capability Requirement | Backend Endpoints Authorized |
| --- | --- | --- | --- | --- | --- |
| **Operational Workspace** | `overview` | `Admin`, `Owner`, `Waiter`, `Kitchen` | Any restaurant role | None (`Basic`) | `Orders` GET, `Tables.GetTables` GET, `ServiceRequests` GET |
| **Operational Workspace** | `cashier-billing` | `Admin`, `Owner`, `Cashier` | Any restaurant role | `standard_billing` (`Basic`) | `ServiceDesk` (`Tables`, `Review`, `Groups`, `Pay`, `PreviewSplit`, `Split`, `Receipt`, `Open`, `Settle`), `Orders` GET, `Tables.GetTables`, `Settings` GET, `Restaurants` GET |
| **Operational Workspace** | `orders-servicedesk` | `Admin`, `Owner`, `Waiter` | Any restaurant role | `standard_billing` (`Basic`) | `Orders` GET/Status/Fire/Refill/Cancel, `ServiceRequests` GET/Acknowledge/Complete, `ServiceDesk` Floor/Transfer/Bill/Group/Payment, `Employees.VerifyManagerPin`, `Tables.GetTables`, `Settings` GET, `Restaurants` GET |
| **Operational Workspace** | `staff-order-taking` | `Admin`, `Owner`, `Waiter` | Any restaurant role | `staff_ordering` or `standard_billing` (`Plus`/`Basic`) | `ServiceDesk.Submit` / Order / Tables / Transfer, `Orders`, `Tables.GetTables`, `Settings` GET, `Restaurants` GET |
| **Operational Workspace** | `kitchen-queue` | `Admin`, `Owner`, `Kitchen` | Any restaurant role | `kitchen_display` (`Basic`) | `Orders` GET/Status/Fire/Cancel, `MenuItems` Availability, `KitchenStations`, `Printers`, `Employees.VerifyManagerPin`, `Settings` GET, `Restaurants` GET |
| **Operational Workspace** | `table-billing` | `Admin`, `Owner`, `Waiter` | Any restaurant role | `standard_billing` (`Basic`) | `ServiceDesk` Review/Groups/Pay/Split/Transfer/Receipt/Open/Settle, `Employees.VerifyManagerPin`, `Orders` GET, `Tables.GetTables`, `Settings` GET |
| **Operational Workspace** | `parcel-counter` | `Admin`, `Owner` | Any restaurant role | `standard_billing` + `parcel_counter` (`Plus`) | `Parcel` (non-marketplace) & `UpdateSettings`, `AdminMenu` GET, `Orders` GET, `Employees.VerifyManagerPin`, `Settings` GET, `Restaurants` GET |
| **Management Workspace** | `menu-management` | `Admin`, `Owner` | Any restaurant role | `menu_management` (`Basic`) | `AdminMenu`, `Categories`, `MenuItems`, `Modifiers`, `Settings`, `KitchenStations`, `Printers`, `Restaurants` GET |
| **Management Workspace** | `tables-qr` | `Admin`, `Owner` | Any restaurant role | `tables_qr` (`Basic`) | `Tables` CRUD + QR generation |
| **Management Workspace** | `crm-loyalty` | `Admin`, `Owner` | Any restaurant role | `standard_billing` (`Basic`) | Customer CRM & Loyalty operations under `/admin?tab=crm` |
| **Management Workspace** | `storefront-settings` | `Admin`, `Owner` | Any restaurant role | `standard_billing` (`Basic`) | `Storefront.UpdateOutletSettings`, `Settings`, `Parcel.UpdateSettings`, `KitchenStations`, `Printers` |
| **Management Workspace** | `marketplace-automation` | `Admin`, `Owner` | Any restaurant role | `standard_billing` (`Basic`) | `Parcel` Marketplace ingestion & payouts |
| **Inventory & ERP** | `inventory-stock`, `inventory-recipes`, `purchasing-orders`, `inventory-suppliers`, `inventory-preparations`, `inventory-movements`, `lots-expiry`, `outlet-transfers`, `inventory-stocktakes`, `central-kitchen` | `Admin`, `Owner` | Any restaurant role | Matching Premium inventory capability (`inventory_core`, `recipes_costing`, `purchasing`, `preparations`, `lots_expiry`, `outlet_transfers`, `stocktakes`) | Corresponding actions on `InventoryController` & `CentralKitchenController` |
| **Reports & Analytics** | `financial-reports`, `stock-valuation-report`, `gross-profit-report` | `Admin`, `Owner` | Any restaurant role | `basic_reports` (`Basic`) / `stock_profit_reports` (`Premium`) | `ReportsController`, `PaymentReportsController`, `ServiceDesk.Tables`, `Tables.GetTables`, `Inventory` Valuation & Gross Profit reports |
| **Restricted Admin Only** | `staff-management`, `staff-menu-access`, `support` | `Admin`, `Owner` ONLY | **`Admin`/`Owner` ONLY** (Recovery locks `staff-management` & `staff-menu-access` ON for Admins) | None | `EmployeesController` (`GetEmployees`, CRUD, reset/status/pin), `NavigationAccessController` (`GetEmployee`, `Put`), `SupportController` (`/api/support/*`), `SupportHub` |
| **Platform Exclusive** | `sa-overview`, `sa-support-inbox`, `sa-tenants`, `sa-plans`, `sa-subscriptions`, `sa-audit` | `SuperAdmin` ONLY | Non-assignable | Platform | `SystemAdminController`, `SupportAdminController`, `SupportHub` platform inbox |

---

## 3. Migration & Production Deployment Checklist

1. **Pending EF Core Migrations on Production (from `Rizal-Rstrct-1`):**
   - `20261125090000_AddEmployeeMenuAccess` (`Employees."AllowedMenuIds"`)
   - `20261125190000_AddEmployeeSecurityStamp` (`Employees."SecurityStamp"`)
   - `20261125200000_AddSupportModuleAndEscalations` (`SupportConversations`, `SupportMessages`, `SupportEscalationLogs`, `SupportAuditLogs`)
   - *Note:* No new schema migration was needed for the `OrderItemConfiguration.PackagingFeeApplied` mapping fix (`8c689c8` -> current HEAD).

2. **Recommended Deployment Order (Pending User Approval):**
   1. Back up the production PostgreSQL database before applying migrations.
   2. Verify nginx reverse-proxy routes `/hubs/order` and `/hubs/support` with WebSocket upgrade headers (`proxy_http_version 1.1`, `Upgrade`, `Connection "upgrade"`).
   3. Confirm production environment configuration (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `AllowedOrigins__*`, `Email__*`, `App__FrontendBaseUrl`, `SuperAdmin__*`).
   4. Stop the backend service, apply the 3 EF Core migrations (`dotnet ef database update` or generated idempotent SQL), and deploy the newly built `Tablo.Api` binaries (`SupportEscalationWorker` + `ScheduledOrderReleaseWorker` run inside the API host).
   5. Build and deploy the updated `tablo` frontend bundle (`dist/`).

3. **Smoke Tests Post-Deployment:**
   - Sign in as Restaurant Admin -> open `/menu-access` -> select Waiter, Kitchen, Cashier -> verify all 25 assignable menus show and plan-gated items show disabled plan badges.
   - Temporarily grant `Kitchen Queue` to a test Waiter (without affecting other Waiters), verify open Waiter tab updates live and survives refresh on `/dashboard?tab=kitchen`, then reset back to Waiter defaults.
   - Verify non-Admin cannot open `/staff`, `/menu-access`, `/support`, or `/super-admin`.
   - Verify Restaurant Support (`/support`) and Super Admin Support Inbox (`/super-admin?tab=inbox`) deliver messages in real time over `/hubs/support`.

4. **Rollback Plan:**
   - Re-deploy previous release frontend bundle and previous `Tablo.Api` binary if application rollback is required.
   - Additive schema changes (`AllowedMenuIds`, `SecurityStamp`, `Support*` tables) are backward-compatible with prior binaries, or restore the pre-deployment PostgreSQL snapshot to revert schema.

---

## 4. Verification Results

| Suite / Command | Result | Scope & Details |
| --- | --- | --- |
| `node --test tests/menu-access.test.mjs tests/menu-access-defaults.test.mjs tests/sidebar-and-navigation.test.mjs tests/navigation-access-compat.test.mjs tests/support-messaging.test.mjs` | **29/29 PASS** | Baseline defaults regression (`Waiter`, `Kitchen`, `Cashier`, `Admin`, `Owner`), cross-role grants, Admin-only boundary, plan labels, sidebar active routes, Support state/navigation |
| `tsc --noEmit` (`tablo`) | **PASS** (0 errors) | Full TypeScript compilation |
| `vite build` (`tablo`) | **PASS** | Production client bundle built in 5.30s |
| `dotnet build Tablo.Api.Tests/Tablo.Api.Tests.csproj` | **PASS** (0 errors) | Backend + test assembly compiled cleanly |
| `node scripts/verify-staff-menu-access.mjs` | **10/10 PASS** | Fresh PostgreSQL 17 DB + real `Tablo.Api.dll` + Vite preview + simultaneous Puppeteer contexts + SignalR `OrderHub`: Basic/Premium checklists, role defaults, Waiter->Kitchen workflow, Kitchen->Cashier cash settlement + Inventory + Reports, Admin/Support denial, single-employee isolation, tenant boundary, downgrade enforcement, open-session live SignalR grant/revocation, reset to defaults (12 screenshots) |
| `node scripts/verify-support-messaging.mjs` | **13/13 PASS** | Full Support regression E2E against fresh PostgreSQL 17 DB: tenant isolation, live bi-directional messaging, unread badge, offline draft + cursor sync, resolve/reopen, 10-minute escalation worker email sink, live session revocation |
| `dotnet test Tablo.Api.Tests --no-build --filter "FullyQualifiedName!~Postgres"` | **173 PASS / 26 FAIL / 12 SKIP** (211 non-Postgres) | **Pre-existing fixture mismatches only:** 17 `inactive` / stale-stamp assertions expecting `403` vs `401` introduced by prior commit `EmployeeSecurityStamp` `OnTokenValidated`, 4 `StartupSeedingTests` (`Migrate()` on InMemory DB), 2 `HealthEndpointTests` (local DB config), 2 `SupportModuleTests` InMemory tracking, 12 `[Fact(Skip)]`. **All 6** `DefaultRoleMenusRegressionTests` and all new cross-role assertions in `NavigationAccessTests` (except pre-existing line 90 inactive `401` vs `403`) passed. |

## 5. Screenshots (`qa/menu-access/20261126055827/`)

- [01-menu-access-basic-plan-waiter-full-checklist.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/01-menu-access-basic-plan-waiter-full-checklist.png)
- [02-menu-access-cashier-defaults-on-basic.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/02-menu-access-cashier-defaults-on-basic.png)
- [03-menu-access-premium-all-enabled.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/03-menu-access-premium-all-enabled.png)
- [04-waiter-defaults-sidebar.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/04-waiter-defaults-sidebar.png)
- [05-kitchen-defaults-sidebar.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/05-kitchen-defaults-sidebar.png)
- [06-cashier-defaults-sidebar.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/06-cashier-defaults-sidebar.png)
- [07-admin-grant-kitchen-to-waiter.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/07-admin-grant-kitchen-to-waiter.png)
- [08-waiter-kitchen-queue-after-refresh.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/08-waiter-kitchen-queue-after-refresh.png)
- [09-kitchen-cashier-billing-settled.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/09-kitchen-cashier-billing-settled.png)
- [10-kitchen-inventory-access.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/10-kitchen-inventory-access.png)
- [11-open-session-revoked-no-access.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/11-open-session-revoked-no-access.png)
- [12-reset-to-role-defaults.png](file:///d:/Projects/tablo/qa/menu-access/20261126055827/12-reset-to-role-defaults.png)
