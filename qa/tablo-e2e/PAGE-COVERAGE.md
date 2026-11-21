# TABLO Page & Tab Coverage Inventory

This document tracks every discovered public, customer, role-guarded, and URL-driven page/tab in TABLO.

---

## 1. Public & Customer Pages

| Route / Destination | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/auth` | Public | Implemented | **Pass** | `PUB-001` | [PUB-001-public-login.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PUB-001-public-login.png) | Public staff & admin authentication screen. |
| `/forgot-password` | Public | Implemented | **Pass** | `PUB-002` | [PUB-002-forgot-password.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PUB-002-forgot-password.png) | Password reset request form. |
| `/t/{qrToken}` (Valid) | Customer (Dine-in) | Implemented | **Pass** | `CUST-001` | [CUST-001-table-1-qr-menu.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-001-table-1-qr-menu.png) | Table 1 customer menu with categories, cart, and dish details. |
| `/t/{qrToken}` (Mobile) | Customer (Mobile) | Implemented | **Pass** | `CUST-002` | [CUST-002-table-1-qr-mobile.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-002-table-1-qr-mobile.png) | Mobile view (390px) with responsive cart and bottom action bar. |
| `/t/{qrToken}` (Invalid) | Customer (Error) | Implemented | **Pass** | `CUST-003` | [CUST-003-invalid-qr-token.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-003-invalid-qr-token.png) | Error state for expired or invalid QR tokens. |

---

## 2. Platform Super Admin Workspace (`/super-admin`)

| Tab / View | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/super-admin` (Overview) | Super Admin | Implemented | **Pass** | `SA-001` | [SA-001-superadmin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-001-superadmin-overview.png) | Multi-tenant KPI overview, total subscriptions, and active tenants. |
| `/super-admin` (Restaurants) | Super Admin | Implemented | **Pass** | `SA-002` | [SA-002-superadmin-tenants.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-002-superadmin-tenants.png) | Tenant directory with status toggle, plan name, and outlet counts. |
| `/super-admin` (Plans) | Super Admin | Implemented | **Pass** | `SA-003` | [SA-003-superadmin-plans-features.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-003-superadmin-plans-features.png) | **FIXED**: Basic, Plus, and Premium packaging with capability pills. |
| `/super-admin` (Subscriptions) | Super Admin | Implemented | **Pass** | `SA-004` | [SA-004-superadmin-subscriptions.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-004-superadmin-subscriptions.png) | Tenant subscription list with plan change controls and renewal status. |
| `/super-admin` (Audit Log) | Super Admin | Implemented | **Pass** | `SA-005` | [SA-005-superadmin-audit-log.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-005-superadmin-audit-log.png) | Audit history for plan modifications, feature toggles, and status overrides. |

---

## 3. Restaurant Admin & Operations Workspace (`/`, `/dashboard`)

| Route / Tab | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/` (Overview) | Restaurant Admin | Implemented | **Pass** | `ADM-001` | [ADM-001-admin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-001-admin-overview.png) | Central overview with live revenue, order counts, kitchen alerts, floor balance. |
| `/dashboard?tab=floor` | Restaurant Admin | Implemented | **Pass** | `ADM-002` | [ADM-002-floor-and-orders.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-002-floor-and-orders.png) | Active floor tables, dining groups, service requests, and quick settlement. |
| `/dashboard?tab=floor` (Review) | Restaurant Admin | Implemented | **Pass** | `FLR-001` | [FLR-001-table-1-settled-review.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/FLR-001-table-1-settled-review.png) | Itemized bill review, paid collections breakdown, and receipt generation. |
| `/dashboard?tab=order` | Restaurant Admin / Waiter | Implemented | **Pass** | `ADM-003` | [ADM-003-staff-order-taking.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-003-staff-order-taking.png) | Dedicated staff ordering workspace with touch-friendly table grid and menu. |
| `/dashboard?tab=order` (Selected) | Restaurant Admin / Waiter | Implemented | **Pass** | `WTR-004` | [WTR-004-order-taking-selected-table.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-004-order-taking-selected-table.png) | Table 1 selected with multi-draft persistence and modifier customization. |
| `/dashboard?tab=kitchen` | Restaurant Admin / Kitchen | Implemented | **Pass** | `ADM-004` | [ADM-004-kitchen-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-004-kitchen-queue.png) | Live KOT queue with station indicators (FOOD, BEV), delayed alerts, hold/fire. |
| `/dashboard?tab=kitchen` (Printers) | Restaurant Admin | Implemented | **Pass** | `PRN-001` | [PRN-001-kitchen-station-printers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PRN-001-kitchen-station-printers.png) | Kitchen printer modal showing ports 9100/9101 and station routing rules. |

---

## 4. Menu, Categories & Tables (`/admin`)

| Tab / View | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/admin?tab=menu` | Restaurant Admin | Implemented | **Pass** | `ADM-005` | [ADM-005-menu-and-categories.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-005-menu-and-categories.png) | Category organizer, dish pricing, station assignment, sold-out toggles. |
| `/admin?tab=tables` | Restaurant Admin | Implemented | **Pass** | `ADM-006` | [ADM-006-tables-and-qr-codes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-006-tables-and-qr-codes.png) | Tables 1-10 overview, download QR canvas, active status toggle. |

---

## 5. Inventory & Supply Chain Workspace (`/inventory`)

| Sub-tab / View | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/inventory?tab=stock` | Restaurant Admin | Implemented | **Pass** | `INV-001` | [INV-001-inventory-stock-recipes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-001-inventory-stock-recipes.png) | 21 ingredients, recipe yields, average costing, low stock alert (Fresh Mint). |
| `/inventory?tab=purchasing` | Restaurant Admin | Implemented | **Pass** | `INV-002` | [INV-002-inventory-purchasing-pos.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-002-inventory-purchasing-pos.png) | Purchase Orders list, approval, partial goods receipt `KADAL-DEMO-001`. |
| `/inventory?tab=lots` | Restaurant Admin | Implemented | **Pass** | `INV-003` | [INV-003-inventory-lots-expiry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-003-inventory-lots-expiry.png) | Inventory lot batches, expiry tracking, quarantined lot `WEST-HOLD`. |
| `/inventory?tab=transfers` | Restaurant Admin | Implemented | **Pass** | `INV-004` | [INV-004-inventory-outlet-transfers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-004-inventory-outlet-transfers.png) | Central Kitchen to MG Road Counter transfer with 10ml spill discrepancy. |
| `/inventory?tab=movements` | Restaurant Admin | Implemented | **Pass** | `INV-005` | [INV-005-inventory-movements.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-005-inventory-movements.png) | Full inventory audit trail: opening stock, settlements, restocks, transfers. |
| `/inventory?tab=suppliers` | Restaurant Admin | Implemented | **Pass** | `INV-006` | [INV-006-inventory-suppliers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-006-inventory-suppliers.png) | Vendor directory: Kadal Produce Cooperative and West Coast Growers. |
| `/inventory?tab=preparations` | Restaurant Admin | Implemented | **Pass** | `INV-007` | [INV-007-inventory-preparations.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-007-inventory-preparations.png) | Fresh lime cordial prep recipe, production batch (450ml yield, 50ml loss). |
| `/inventory?tab=stocktakes` | Restaurant Admin | Implemented | **Pass** | `INV-008` | [INV-008-inventory-stocktakes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-008-inventory-stocktakes.png) | Physical count cycle: "Closing count in progress" draft session. |

---

## 6. Financial Reports & Staff Management

| Route | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/reports` | Restaurant Admin | Implemented | **Pass** | `REP-001` | [REP-001-payment-reports.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/REP-001-payment-reports.png) | IST date filtering, gross sales, ₹722 payment ledger across Cash/UPI/Card. |
| `/staff` | Restaurant Admin | Implemented | **Pass** | `STF-001` | [STF-001-staff-management.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STF-001-staff-management.png) | Employee directory, active status toggles, role-permission assignments. |

---

## 7. Role-Specific & Isolation Personas

| View / Persona | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/dashboard?tab=order` | Waiter Staff | Implemented | **Pass** | `WTR-001` | [WTR-001-waiter-order-taking.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-001-waiter-order-taking.png) | **FIXED**: Waiter order taking with all 10 tables loaded, no 404 error. |
| `/dashboard?tab=order` (Mobile) | Waiter Staff | Implemented | **Pass** | `WTR-002` | [WTR-002-waiter-order-mobile.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-002-waiter-order-mobile.png) | Waiter mobile interface with quick-tap table list and sticky cart summary. |
| `/dashboard?tab=floor` | Waiter Staff | Implemented | **Pass** | `WTR-003` | [WTR-003-waiter-floor-view.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-003-waiter-floor-view.png) | Waiter floor view showing Table 1 settled, Table 2 active, Table 9 held. |
| `/dashboard?tab=kitchen` | Kitchen Staff | Implemented | **Pass** | `KIT-001` | [KIT-001-kitchen-station-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/KIT-001-kitchen-station-queue.png) | Kitchen staff interface with 1-click order advance to Preparing and Ready. |
| `/` (Coastal Cafe) | Secondary Tenant Admin | Implemented | **Pass** | `ISO-001` | [ISO-001-second-tenant-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ISO-001-second-tenant-overview.png) | Second tenant on Basic plan. Verified zero leakage of Tablo Heritage data. |

---

## 8. Marketplace Delivery & Payout Reconciliation (`/admin?tab=parcel`)

| View / Sub-tab | Persona / Role | Implementation Status | Visit Status | Test Case ID | Screenshot Evidence | Description / Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `/admin?tab=parcel` (Swiggy Delivery) | Cashier / Counter Staff | Implemented | **Pass** | `DLV-001` | [DLV-001-swiggy-order-entry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-001-swiggy-order-entry.png) | Manual Swiggy order entry with external ref `SWIGGY-8921`, prepaid toggle, entered total matching. |
| `/admin?tab=parcel` (Zomato Delivery) | Cashier / Counter Staff | Implemented | **Pass** | `DLV-002` | [DLV-002-zomato-order-entry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-002-zomato-order-entry.png) | Zomato order entry with external ref `ZOM-4412`, tableless routing, station tickets. |
| `/admin?tab=parcel` (Delivery Queue) | Cashier / Counter Staff | Implemented | **Pass** | `DLV-003` | [DLV-003-marketplace-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-003-marketplace-queue.png) | Today's tokens queue with Swiggy/Zomato provider badges, external references, and token numbers. |
| `/admin?tab=parcel` (Payouts Tab) | Manager / Admin | Implemented | **Pass** | `DLV-004` | [DLV-004-payout-reconciliation.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-004-payout-reconciliation.png) | Marketplace payouts reconciliation: gross order GMV, commissions deducted, TCS/GST, and net bank deposits. |
| `/inventory?tab=transfers` (Shortage Audit) | Admin / Inventory Manager | Implemented | **Pass** | `INV-009` | [INV-009-transfer-shortage-correction.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-009-transfer-shortage-correction.png) | Outlet transfer shortage dry-run audit report and traceable compensating correction movements. |
