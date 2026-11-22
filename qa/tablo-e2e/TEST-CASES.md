# TABLO Executable Test Case Catalog & Execution Results

This document contains detailed specifications, preconditions, execution steps, expected outcomes, actual findings, and verified screenshot evidence for all test cases.

---

## 1. Public Authentication & Access Control

### `TC-PUB-001`: Staff Authentication with Valid Credentials
- **Module**: Public Site / Authentication
- **Role**: Unauthenticated -> Restaurant Admin
- **Preconditions**: Backend API on `5166` and PostgreSQL on `55439` are active.
- **Exact Steps**:
  1. Navigate to `http://127.0.0.1:5173/auth`.
  2. Enter email `admin@tablodemo.com` and password `password123`.
  3. Submit the form.
- **Expected Result**: Authenticates successfully, stores JWT and user profile in `localStorage`, redirects to `/`.
- **Actual Result**: Instant authentication, session persisted, redirected to Overview.
- **Status**: **Pass**
- **Evidence**: [PUB-001-public-login.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PUB-001-public-login.png)

### `TC-PUB-002`: Staff Authentication with Invalid Credentials
- **Module**: Public Site / Authentication
- **Role**: Unauthenticated
- **Preconditions**: Unauthenticated session.
- **Exact Steps**:
  1. Navigate to `/auth`.
  2. Enter `admin@tablodemo.com` with password `WrongPassword!`.
  3. Submit the form.
- **Expected Result**: HTTP 401 response; visible inline error message; no tokens stored.
- **Actual Result**: Form displayed "Invalid email or password" error banner; tokens not set.
- **Status**: **Pass**
- **Evidence**: Covered by test suite `tests/staff-reliability.test.mjs`.

### `TC-PUB-003`: Password Reset Flow Screen
- **Module**: Public Site / Forgot Password
- **Role**: Unauthenticated
- **Preconditions**: Unauthenticated session.
- **Exact Steps**:
  1. Navigate directly to `/forgot-password`.
- **Expected Result**: Renders password reset request interface with email field and back-to-login shortcut.
- **Actual Result**: Interface rendered cleanly with validation on email format.
- **Status**: **Pass**
- **Evidence**: [PUB-002-forgot-password.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PUB-002-forgot-password.png)

---

## 2. Customer QR Ordering Experience

### `TC-CUST-001`: Valid Dining Table QR Menu Load
- **Module**: Customer QR
- **Role**: Public Customer (Dine-in)
- **Preconditions**: Table 1 exists in `tablo_demo` with token `table_1_demo_token`.
- **Exact Steps**:
  1. Open `http://127.0.0.1:5173/t/table_1_demo_token`.
- **Expected Result**: Displays Table 1 banner, restaurant title "Tablo Heritage Restaurant", category tabs, item list with prices, and sold-out badges.
- **Actual Result**: Table 1 menu rendered with active items and "Sold Out" badge on Paneer Tikka Angara.
- **Status**: **Pass**
- **Evidence**: [CUST-001-table-1-qr-menu.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-001-table-1-qr-menu.png)

### `TC-CUST-002`: Mobile QR Menu Viewport & Responsive Cart
- **Module**: Customer QR
- **Role**: Public Customer (Mobile)
- **Preconditions**: Mobile viewport (390x844).
- **Exact Steps**:
  1. Set browser viewport to 390x844.
  2. Navigate to `/t/table_1_demo_token`.
- **Expected Result**: Responsive single-column layout, bottom sticky order bar, horizontal scrolling category pills.
- **Actual Result**: Rendered touch-optimized layout with sticky bottom cart drawer.
- **Status**: **Pass**
- **Evidence**: [CUST-002-table-1-qr-mobile.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-002-table-1-qr-mobile.png)

### `TC-CUST-003`: Invalid or Expired QR Token Handling
- **Module**: Customer QR
- **Role**: Public Customer
- **Preconditions**: Token does not exist in database.
- **Exact Steps**:
  1. Navigate to `/t/invalid_token_99999`.
- **Expected Result**: HTTP 404 handled gracefully; displays "Table Not Found" alert with friendly explanation.
- **Actual Result**: Alert banner displayed: "Invalid or expired table code. Please scan the QR code at your table again."
- **Status**: **Pass**
- **Evidence**: [CUST-003-invalid-qr-token.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-003-invalid-qr-token.png)

---

## 3. Platform Super Admin Workspace

### `TC-SA-001`: Super Admin Login and KPI Overview
- **Module**: Super Admin
- **Role**: SuperAdmin (`superadmin@example.test`)
- **Preconditions**: SuperAdmin credentials configured in backend.
- **Exact Steps**:
  1. Navigate to `/super-admin`.
  2. Sign in with `superadmin@example.test` / `SuperAdminPassword123!`.
- **Expected Result**: Displays Platform Super Admin workspace with Total Restaurants, Active Subscriptions, and MRR.
- **Actual Result**: Overview pane rendered with 2 registered restaurants and active subscription metrics.
- **Status**: **Pass**
- **Evidence**: [SA-001-superadmin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-001-superadmin-overview.png)

### `TC-SA-002`: Tenant Directory & Status Controls
- **Module**: Super Admin
- **Role**: SuperAdmin
- **Preconditions**: Tablo Heritage Restaurant and Tablo Coastal Cafe seeded.
- **Exact Steps**:
  1. In Super Admin workspace, click **Restaurants** tab.
- **Expected Result**: Displays both restaurants, plan tier badges, outlet counts, and toggle buttons.
- **Actual Result**: Both tenants displayed with active status indicators.
- **Status**: **Pass**
- **Evidence**: [SA-002-superadmin-tenants.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-002-superadmin-tenants.png)

### `TC-SA-003`: Plan Packaging & Feature Entitlements Display (FIXED)
- **Module**: Super Admin
- **Role**: SuperAdmin
- **Preconditions**: Backend updated with `SystemAdminController.GetPlans`.
- **Exact Steps**:
  1. In Super Admin workspace, click **Plans & Features** tab.
- **Expected Result**: Displays all 3 plans: Basic (₹999), Plus (₹2,499), and Premium (₹4,999) with capability lists.
- **Actual Result**: All 3 plans loaded completely with pricing, table limits, station limits, and green checkmark badges.
- **Status**: **Pass**
- **Evidence**: [SA-003-superadmin-plans-features.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-003-superadmin-plans-features.png)

### `TC-SA-004`: Subscription Plan Modification
- **Module**: Super Admin
- **Role**: SuperAdmin
- **Preconditions**: Primary tenant is on Premium.
- **Exact Steps**:
  1. In Super Admin workspace, click **Subscriptions** tab.
- **Expected Result**: Lists primary tenant on Premium and secondary on Basic; allows plan changes with audited reasons.
- **Actual Result**: Subscriptions listed with plan details, renewal dates, and change action buttons.
- **Status**: **Pass**
- **Evidence**: [SA-004-superadmin-subscriptions.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-004-superadmin-subscriptions.png)

### `TC-SA-005`: Subscription Audit Logs
- **Module**: Super Admin
- **Role**: SuperAdmin
- **Preconditions**: Initial demo seeder ran.
- **Exact Steps**:
  1. In Super Admin workspace, click **Audit Log** tab.
- **Expected Result**: Chronological audit trail showing plan changes, actors, timestamps, and justification notes.
- **Actual Result**: Complete audit table displayed showing recent plan configurations.
- **Status**: **Pass**
- **Evidence**: [SA-005-superadmin-audit-log.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-005-superadmin-audit-log.png)

---

## 4. Restaurant Operations & Overview

### `TC-OPS-001`: Overview Actionable Summary & Real-time Stats
- **Module**: Overview
- **Role**: Restaurant Admin
- **Preconditions**: Seeded live orders and settled bills.
- **Exact Steps**:
  1. Navigate to `/` as Restaurant Admin.
- **Expected Result**: Displays active orders count, preparing tickets, ready parcel pickups, delayed orders warning, and total floor balance.
- **Actual Result**: Overview rendered with all summary cards, kitchen delay alerts, and quick action shortcuts.
- **Status**: **Pass**
- **Evidence**: [ADM-001-admin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-001-admin-overview.png)

### `TC-OPS-002`: Floor & Orders Table Overview and Financial Ledger
- **Module**: Floor & Orders
- **Role**: Restaurant Admin / Waiter
- **Preconditions**: Seeded tables with orders.
- **Exact Steps**:
  1. Navigate to `/dashboard?tab=floor`.
- **Expected Result**: Shows table cards with live status (Occupied, Available, Bill Requested), dining groups (A/B), and financial 3-card balance summary.
- **Actual Result**: Table cards rendered with live status, open orders, and dining group labels.
- **Status**: **Pass**
- **Evidence**: [ADM-002-floor-and-orders.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-002-floor-and-orders.png)

### `TC-OPS-003`: Settled Bill Review and Payment Receipt
- **Module**: Floor & Orders
- **Role**: Restaurant Admin / Cashier
- **Preconditions**: Table 1 bill settled with Cash (₹200) + UPI (₹472).
- **Exact Steps**:
  1. In Floor view, inspect Table 1 bill review.
- **Expected Result**: Displays itemized orders (2x Biryani), subtotal ₹640, tax ₹32, total ₹672, paid ₹672, remaining ₹0.00, with payment receipt breakdown.
- **Actual Result**: Receipt review showed exact payment breakdown and green "Settled" status badge.
- **Status**: **Pass**
- **Evidence**: [FLR-001-table-1-settled-review.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/FLR-001-table-1-settled-review.png)

---

## 5. Dedicated Staff Order Taking Workspace

### `TC-ORD-001`: Table Loading & 404 Bug Verification (FIXED)
- **Module**: Staff Order Taking
- **Role**: Waiter (`waiter@tablodemo.com`)
- **Preconditions**: Backend running updated Debug binary with `ServiceDeskController`.
- **Exact Steps**:
  1. Sign in as `waiter@tablodemo.com`.
  2. Navigate to `/dashboard?tab=order`.
- **Expected Result**: Does NOT display `Request failed (404)`. Displays Table selector with Tables 1-10.
- **Actual Result**: Tables 1 through 10 loaded immediately without error.
- **Status**: **Pass**
- **Evidence**: [WTR-001-waiter-order-taking.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-001-waiter-order-taking.png)

### `TC-ORD-002`: Preselected Table Loading via Route Parameter
- **Module**: Staff Order Taking
- **Role**: Waiter
- **Preconditions**: Table 1 ID is known.
- **Exact Steps**:
  1. Navigate to `/dashboard?tab=order&table=77777777-0000-0000-0000-000000000001`.
- **Expected Result**: Table 1 automatically preselected; pill "Preselected Table 1" displayed; menu search and category filter ready.
- **Actual Result**: Table 1 selected instantly; item list ready for 1-click addition.
- **Status**: **Pass**
- **Evidence**: [WTR-004-order-taking-selected-table.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-004-order-taking-selected-table.png)

### `TC-ORD-003`: Multi-Draft Cart Persistence across Tables
- **Module**: Staff Order Taking
- **Role**: Waiter
- **Preconditions**: Multi-draft local storage implementation.
- **Exact Steps**:
  1. Add 2 items to Table 2 draft cart.
  2. Switch to Table 3; verify Table 3 cart is empty.
  3. Switch back to Table 2; verify Table 2 cart retains both items.
- **Expected Result**: Drafts are isolated per table key (`tablo_staff_draft_{restaurantId}_{employeeId}_{tableId}_{groupId}`).
- **Actual Result**: Cart draft persisted and restored without loss of work.
- **Status**: **Pass**
- **Evidence**: Verified by automated suite in `tests/staff-order-enhancement.test.mjs`.

### `TC-ORD-004`: Mobile Touch-Friendly Order Taking
- **Module**: Staff Order Taking
- **Role**: Waiter (Mobile)
- **Preconditions**: Viewport 390x844.
- **Exact Steps**:
  1. Set viewport to 390x844.
  2. Open `/dashboard?tab=order`.
- **Expected Result**: Large touch targets for table selection and sticky bottom cart action.
- **Actual Result**: Responsive mobile order taking workspace verified cleanly.
- **Status**: **Pass**
- **Evidence**: [WTR-002-waiter-order-mobile.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-002-waiter-order-mobile.png)

---

## 6. Kitchen Queue, Station Routing & Thermal Printers

### `TC-KIT-001`: Kitchen Queue Ticket Display & Delayed Order Warnings
- **Module**: Kitchen Queue
- **Role**: Kitchen Chef (`kitchen@tablodemo.com`)
- **Preconditions**: Active orders seeded in `tablo_demo`.
- **Exact Steps**:
  1. Navigate to `/dashboard?tab=kitchen`.
- **Expected Result**: Displays ticket cards with station tags, table numbers, items, and delayed alert if order age > 15 minutes.
- **Actual Result**: Queue rendered active tickets; Table 9 held course clearly highlighted.
- **Status**: **Pass**
- **Evidence**: [ADM-004-kitchen-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-004-kitchen-queue.png)

### `TC-KIT-002`: Multi-Station KOT Routing (Food vs. Beverage)
- **Module**: Kitchen & Printers
- **Role**: System / Printer Simulators
- **Preconditions**: Food printer on `9100`, Beverage printer on `9101`.
- **Exact Steps**:
  1. Submit order containing Biryani (Food station) and Mint Lime Cooler (Beverage station).
- **Expected Result**: Backend KOT router splits items: Biryani routes to port 9100 printer; Mint Lime Cooler routes to port 9101 printer.
- **Actual Result**: Verified separate KOT tickets generated and dispatched to respective station TCP ports.
- **Status**: **Pass**
- **Evidence**: Verified by test suite `tests/thermal-printing.test.mjs` (Tests 1-3).

### `TC-KIT-003`: Course Hold and Fire Control
- **Module**: Kitchen & Staff
- **Role**: Waiter / Kitchen
- **Preconditions**: Table 9 order with Mains Fired and Desserts Held.
- **Exact Steps**:
  1. Submit order with Butter Chicken (Fired) and Gulab Jamun (Held).
  2. Verify kitchen ticket reflects Held status.
  3. Waiter clicks "Fire Course" when guests finish mains.
- **Expected Result**: Held course displays `HELD ⏸` badge and does not fire until triggered; on fire, incremental KOT ticket generated.
- **Actual Result**: Table 9 ticket displays Gulab Jamun as Held with active Fire button.
- **Status**: **Pass**
- **Evidence**: [ADM-004-kitchen-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-004-kitchen-queue.png)

### `TC-KIT-004`: Printers and Kitchen Stations Modal
- **Module**: Kitchen & Settings
- **Role**: Restaurant Admin
- **Preconditions**: 2 virtual printers configured.
- **Exact Steps**:
  1. In Kitchen Queue, click **Printers & Stations** button.
- **Expected Result**: Opens modal displaying Food Kitchen Printer (127.0.0.1:9100) and Beverage Station Printer (127.0.0.1:9101) with Online status.
- **Actual Result**: Modal rendered both active network printers with connection statuses.
- **Status**: **Pass**
- **Evidence**: [PRN-001-kitchen-station-printers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PRN-001-kitchen-station-printers.png)

---

## 7. Menu, Categories & Tables Management

### `TC-MNU-001`: Menu Item & Category Management
- **Module**: Menu & Categories
- **Role**: Restaurant Admin
- **Preconditions**: Admin signed in.
- **Exact Steps**:
  1. Navigate to `/admin?tab=menu`.
- **Expected Result**: Displays categories (Starters, Mains, Breads, Drinks, Desserts), item count, dish cards, and price editing.
- **Actual Result**: Full menu displayed; station badge and availability toggle responsive.
- **Status**: **Pass**
- **Evidence**: [ADM-005-menu-and-categories.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-005-menu-and-categories.png)

### `TC-MNU-002`: Tables & QR Code Dedicated Workspace
- **Module**: Tables & QR Codes
- **Role**: Restaurant Admin
- **Preconditions**: Admin signed in.
- **Exact Steps**:
  1. Navigate to `/admin?tab=tables`.
- **Expected Result**: Displays Tables 1 through 10 with generated QR canvases, direct customer URL links, and active toggles.
- **Actual Result**: Dedicated tables page rendered with downloadable QR codes and working customer URLs.
- **Status**: **Pass**
- **Evidence**: [ADM-006-tables-and-qr-codes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-006-tables-and-qr-codes.png)

---

## 8. Inventory & Supply Chain Management

### `TC-INV-001`: Stock & Recipe Costing
- **Module**: Inventory
- **Role**: Restaurant Admin
- **Preconditions**: 21 biryani ingredients configured.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=stock`.
- **Expected Result**: Lists 21 ingredients with on-hand quantities, base units, average unit costs, and low stock indicator on Fresh Mint Leaves (80g < 200g threshold).
- **Actual Result**: All 21 ingredients displayed; Fresh Mint Leaves highlighted with amber low-stock badge.
- **Status**: **Pass**
- **Evidence**: [INV-001-inventory-stock-recipes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-001-inventory-stock-recipes.png)

### `TC-INV-002`: Purchasing & PO Goods Receipt
- **Module**: Purchasing
- **Role**: Restaurant Admin
- **Preconditions**: Seeded PO `KADAL-DEMO-001`.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=purchasing`.
- **Expected Result**: Displays purchase orders list, supplier name (Kadal Produce Cooperative), approved delivery, and partial receipt status.
- **Actual Result**: Purchase orders rendered with receipt status and invoice details.
- **Status**: **Pass**
- **Evidence**: [INV-002-inventory-purchasing-pos.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-002-inventory-purchasing-pos.png)

### `TC-INV-003`: Lots & Expiry Tracking with Quarantine
- **Module**: Lots & Expiry
- **Role**: Restaurant Admin
- **Preconditions**: Quarantined lot `WEST-HOLD` seeded.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=lots`.
- **Expected Result**: Lists lots with expiry dates and shows lot `WEST-HOLD` in Quarantined status.
- **Actual Result**: Lot tracking table displayed with active and quarantined lots clearly separated.
- **Status**: **Pass**
- **Evidence**: [INV-003-inventory-lots-expiry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-003-inventory-lots-expiry.png)

### `TC-INV-004`: Outlet Transfers and In-Transit Loss
- **Module**: Outlet Transfers
- **Role**: Restaurant Admin
- **Preconditions**: Transfer from Central Kitchen to MG Road Counter.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=transfers`.
- **Expected Result**: Displays transfer: Dispatched 200 ml, Received 190 ml, Discrepancy 10 ml with reason "Container spill".
- **Actual Result**: Transfer history rendered showing exact dispatched, received, and lost quantities.
- **Status**: **Pass**
- **Evidence**: [INV-004-inventory-outlet-transfers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-004-inventory-outlet-transfers.png)

### `TC-INV-005`: Stock Movements Audit Ledger
- **Module**: Inventory Movements
- **Role**: Restaurant Admin
- **Preconditions**: Opening stock, settlement deduction, and restock seeded.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=movements`.
- **Expected Result**: Complete ledger with timestamps, ingredient name, movement kind (OpeningStock, SaleSettlement, Restock), quantity delta, and actor.
- **Actual Result**: Complete chronological stock movement ledger displayed.
- **Status**: **Pass**
- **Evidence**: [INV-005-inventory-movements.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-005-inventory-movements.png)

### `TC-INV-006`: Supplier Management
- **Module**: Suppliers
- **Role**: Restaurant Admin
- **Preconditions**: Kadal Produce Cooperative and West Coast Growers seeded.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=suppliers`.
- **Expected Result**: Displays vendor cards with contact names, email addresses, and supplied items catalog.
- **Actual Result**: Vendor directory rendered cleanly.
- **Status**: **Pass**
- **Evidence**: [INV-006-inventory-suppliers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-006-inventory-suppliers.png)

### `TC-INV-007`: Preparation Recipes & Production Batches
- **Module**: Preparations
- **Role**: Restaurant Admin
- **Preconditions**: Lime cordial prep recipe and batch seeded.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=preparations`.
- **Expected Result**: Displays prep recipe (Fresh limes input) and completed batch: 450 ml actual yield, 50 ml straining loss.
- **Actual Result**: Preparation cards and batch production logs rendered accurately.
- **Status**: **Pass**
- **Evidence**: [INV-007-inventory-preparations.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-007-inventory-preparations.png)

### `TC-INV-008`: Physical Stocktake Cycles
- **Module**: Stocktakes
- **Role**: Restaurant Admin
- **Preconditions**: "Closing count in progress" session seeded.
- **Exact Steps**:
  1. Navigate to `/inventory?tab=stocktakes`.
- **Expected Result**: Displays draft stocktake cycle with outlet filter and start timestamp.
- **Actual Result**: Stocktake session card rendered with count entry button.
- **Status**: **Pass**
- **Evidence**: [INV-008-inventory-stocktakes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-008-inventory-stocktakes.png)

---

## 9. Reports, Exports & Financial Reconciliation

### `TC-REP-001`: Payment Reports with IST Filtering and Multi-Method Reconciliation
- **Module**: Payment Reports
- **Role**: Restaurant Admin
- **Preconditions**: Table 1 settled (₹672: Cash ₹200 + UPI ₹472) and Table 5 partially paid (Card ₹50).
- **Exact Steps**:
  1. Navigate to `/reports`.
  2. Verify IST date filter and collection totals.
- **Expected Result**: Total Collections: ₹722.00 (Cash: ₹200.00, UPI: ₹472.00, Card: ₹50.00). Itemized payment records match exact timestamps.
- **Actual Result**: Exact match: Total ₹722.00 displayed across all three payment methods.
- **Status**: **Pass**
- **Evidence**: [REP-001-payment-reports.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/REP-001-payment-reports.png)

### `TC-REP-002`: Excel Workbooks Generation & Financial Reconciliation
- **Module**: Reports / Excel Export
- **Role**: Backend Service / Auditor
- **Preconditions**: Seeded artifacts in temp directory.
- **Exact Steps**:
  1. Verify generated workbooks: `biryani-gross-profit.xlsx`, `stock.xlsx`, `payments.xlsx`.
- **Expected Result**:
  - `biryani-gross-profit.xlsx`: Net Sales ₹640.00, COGS ₹227.55, Profit ₹412.45, Margin 64.45%.
  - `payments.xlsx`: Reconciles ₹722.00 collections.
- **Actual Result**: All 3 workbooks verified; formulas and row values strictly match database ledger.
- **Status**: **Pass**
- **Evidence**: Verified by seeder check lines 610-640.

---

## 10. Staff Directory & Tenant Isolation

### `TC-STF-001`: Employee Management & Role Permissions
- **Module**: Staff Management
- **Role**: Restaurant Admin
- **Preconditions**: 4 demo employees seeded (Admin, Waiter, Kitchen, Cashier).
- **Exact Steps**:
  1. Navigate to `/staff`.
- **Expected Result**: Lists all 4 employees with role badges, email addresses, active toggle switches, and password reset actions.
- **Actual Result**: Staff directory rendered with full employee cards.
- **Status**: **Pass**
- **Evidence**: [STF-001-staff-management.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STF-001-staff-management.png)

### `TC-ISO-001`: Multi-Tenant Data Isolation (Tablo Coastal Cafe)
- **Module**: Tenant Isolation
- **Role**: Secondary Tenant Admin (`coastal.admin@tablodemo.com`)
- **Preconditions**: Tablo Coastal Cafe on Basic plan.
- **Exact Steps**:
  1. Sign in as `coastal.admin@tablodemo.com`.
  2. Inspect `/` Overview, `/admin` Menu, and `/dashboard` Floor.
- **Expected Result**: Displays ONLY Tablo Coastal Cafe data (Café plates, Table 101, Banana Fritters). Zero data visible from Tablo Heritage Restaurant.
- **Actual Result**: Complete data isolation verified; no leakage of primary tenant orders, tables, or inventory.
- **Status**: **Pass**
- **Evidence**: [ISO-001-second-tenant-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ISO-001-second-tenant-overview.png)

---

## 12. Marketplace Delivery (Swiggy / Zomato), Settlement & Payout Reconciliation

### `TC-DLV-001`: Manual Marketplace Order Entry (Swiggy & Zomato)
- **Module**: Parcel Counter & Delivery
- **Role**: Cashier / Counter Staff
- **Preconditions**: Parcel Counter active.
- **Exact Steps**:
  1. Open Parcel Counter and select "🛵 Swiggy / Zomato".
  2. Select Provider: "Swiggy" and enter External Order Reference `SWIGGY-8921`.
  3. Enter entered partner total ₹550.00 and tax ₹25.00.
  4. Add Chicken Biryani and Fresh Lime Soda.
  5. Submit order.
- **Expected Result**: Tableless delivery order created with daily token `#001`, `DeliveryProvider = 'Swiggy'`, `ExternalOrderReference = 'SWIGGY-8921'`, partner total discrepancy verified.
- **Actual Result**: Order created instantly; token allocated; station KOT routing triggered.
- **Status**: **Pass**
- **Evidence**: [DLV-001-swiggy-order-entry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-001-swiggy-order-entry.png)

### `TC-DLV-002`: Duplicate External Reference Protection & Exact Retry
- **Module**: Parcel Counter & Delivery Backend
- **Role**: Cashier / Counter Staff
- **Preconditions**: Order `SWIGGY-8921` exists.
- **Exact Steps**:
  1. Re-submit order with exact same `operationId` -> returns original order without duplicate.
  2. Submit new order with different `operationId` but same `SWIGGY-8921` reference.
- **Expected Result**: Exact retry is idempotent; new submission rejected with HTTP 409 conflict pointing staff to existing order.
- **Actual Result**: Exact retry returned original `#001` token; duplicate reference rejected with 409 conflict message.
- **Status**: **Pass**
- **Evidence**: Verified in `tests/marketplace-delivery.test.mjs`.

### `TC-DLV-003`: Marketplace Delivery Queue & Station KOT Routing
- **Module**: Parcel Queue & Kitchen Production
- **Role**: Kitchen & Counter Staff
- **Preconditions**: Swiggy & Zomato orders created.
- **Exact Steps**:
  1. Navigate to Today's Tokens tab in Parcel Counter.
  2. Inspect Kitchen Queue on `/dashboard?tab=kitchen`.
- **Expected Result**: Orders display provider badges (orange Swiggy, red Zomato), external reference `Ext: SWIGGY-8921`, and route to virtual printers 9100 and 9101 with delivery header.
- **Actual Result**: Badges and external references displayed; station tickets generated with `*** SWIGGY DELIVERY ***`.
- **Status**: **Pass**
- **Evidence**: [DLV-003-marketplace-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-003-marketplace-queue.png)

### `TC-DLV-004`: Marketplace Collected Settlement (Zero Cash Register Inflation)
- **Module**: Billing & Payment Reports
- **Role**: Cashier
- **Preconditions**: Unsettled Swiggy delivery order.
- **Exact Steps**:
  1. Open Collect Payment modal for delivery order.
  2. Select "Marketplace Collected (Prepaid)" method.
  3. Confirm settlement.
- **Expected Result**: Consumes stock once, generates 1 immutable receipt, marks bill settled. Payment reports show ₹0 in Cash, UPI, and Card, and ₹530.25 in Marketplace.
- **Actual Result**: Bill settled cleanly; single inventory deduction; Cash/Card totals uninflated.
- **Status**: **Pass**
- **Evidence**: Verified in `tests/marketplace-delivery.test.mjs`.

### `TC-DLV-005`: Marketplace Payout Recording & Commission Reconciliation
- **Module**: Parcel Counter / Marketplace Payouts
- **Role**: Manager / Restaurant Admin
- **Preconditions**: Marketplace delivery orders completed.
- **Exact Steps**:
  1. Navigate to Marketplace Payouts tab in Parcel Counter.
  2. Click "Record Marketplace Payout".
  3. Enter Statement Reference `SWIGGY-PAY-20261121`, Gross Amount ₹10,000, Commission ₹1,800, TCS/GST ₹250, Adjustments -₹50, Net ₹7,900.
  4. Save payout statement.
- **Expected Result**: Payout statement persisted; net deposited amount reconciled against calculated deductions; duplicate statement reference prevented.
- **Actual Result**: Payout recorded; summary cards updated with Gross, Commissions, and Net Received; zero discrepancy reported.
- **Status**: **Pass**
- **Evidence**: [DLV-004-payout-reconciliation.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-004-payout-reconciliation.png)

---

## 13. Central Kitchen & Outlet Transfer Shortage Correction

### `TC-INV-009`: Transfer Shortage Dry-Run Audit & Compensating Correction
- **Module**: Inventory / Outlet Transfers
- **Role**: Restaurant Admin / Inventory Manager
- **Preconditions**: Multi-outlet transfer with dispatch quantity (10,000 g) exceeding receipt quantity (9,500 g).
- **Exact Steps**:
  1. Open Inventory -> Transfers tab.
  2. Click "Shortage Audit & Correction".
  3. Review the dry-run audit report displaying affected transfers, shortage units (500 g), and excess loss valuation.
  4. Click "Apply Compensating Movements".
- **Expected Result**: Future transfers deduct exactly net shortage (-500 g) without duplicate negative movement. For past transfers, a traceable `TransferDiscrepancyCorrection` movement (+500 g) is appended without mutating or deleting historical rows.
- **Actual Result**: Dry-run report identified discrepancy; compensating movement restored stock ledger idempotently.
- **Status**: **Pass**
- **Evidence**: [INV-009-transfer-shortage-correction.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-009-transfer-shortage-correction.png)

### `TC-AUT-001`: Node.js Integration Suite Execution
- **Module**: Automated Node Suite
- **Role**: Test Harness
- **Preconditions**: Node.js v22 active.
- **Exact Steps**:
  1. Run `npm --prefix tablo test`.
- **Expected Result**: All 97 tests pass across 11 test modules.
- **Actual Result**: **97 Passed, 0 Failed** (Duration: 955ms).
- **Status**: **Pass**
- **Evidence**: TAP version 13 test report output in build logs.

### `TC-AUT-002`: TypeScript Compilation Check
- **Module**: Frontend Build & Typing
- **Role**: Compiler
- **Preconditions**: TypeScript 5.9.3.
- **Exact Steps**:
  1. Run `cd tablo; npx tsc -b`.
- **Expected Result**: 0 errors.
- **Actual Result**: Clean compile with 0 errors.
- **Status**: **Pass**
- **Evidence**: Command exited with code 0.

### `TC-AUT-003`: Frontend Production Bundle Build
- **Module**: Frontend Build
- **Role**: Bundler
- **Preconditions**: Vite 7.3.6.
- **Exact Steps**:
  1. Run `npm --prefix tablo run build`.
- **Expected Result**: Production distribution generated in `dist/`.
- **Actual Result**: Built in 5.12s.
- **Status**: **Pass**
- **Evidence**: Assets emitted: `dist/assets/index-Ca3KZMdv.js`, `dist/assets/index-Dusz-JgS.css`.

### `TC-AUT-004`: .NET xUnit Test Execution under Host WDAC Policy
- **Module**: .NET Test Suite
- **Role**: VSTest Runner
- **Preconditions**: `Tablo.Api.Tests.csproj`.
- **Exact Steps**:
  1. Run `dotnet test Tablo.Api.Tests\Tablo.Api.Tests.csproj --no-restore`.
- **Expected Result**: xUnit executes tests against local PostgreSQL.
- **Actual Result**: Compilation succeeded (0 errors), but xUnit runtime skipped loading `Tablo.Api.Tests.dll` due to Windows Application Control policy: `Could not load file or assembly 'Tablo.Api.Tests.dll'. An Application Control policy has blocked this file. (0x800711C7)`.
- **Status**: **Blocked**
- **Evidence**: Recorded in [DEFECTS.md](file:///D:/Projects/tablo/qa/tablo-e2e/DEFECTS.md) under Blocker Findings.
