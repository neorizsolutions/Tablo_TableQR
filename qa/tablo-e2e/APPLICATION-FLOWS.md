# TABLO Role-by-Role Application Flow Guide

This guide provides step-by-step walkthroughs for every primary operational workflow in TABLO, using the exact current browser routes, button labels, and verified roles.

---

## 1. Master End-to-End Restaurant Lifecycle Walkthrough
**Journey**: *Customer QR Scan → Staff Acceptance → Kitchen KOT Routing → Food Preparation → Table Service → Split Billing & Payment → Immutable Receipt → Recipe Inventory Deduction → Financial Gross Profit Reporting*.

### Step 1: Customer Scans QR Code & Places Order
- **Starting Route**: `http://127.0.0.1:5173/t/table_1_demo_token`
- **Role**: Public Customer (Dine-in Table 1)
- **Workflow**:
  1. Customer opens their camera and loads their unique table URL.
  2. The digital menu opens displaying categories (**Starters & Appetizers**, **Main Course Curries**, **Tandoor & Breads**, **Artisan Beverages**, **Desserts**).
  3. Customer clicks **Add** on *Thalassery Chicken Dum Biryani* (₹320.00).
  4. A modifier dialog opens: Customer selects **Spice Level: Medium Spicy** (₹0.00) and **Portion Size: Regular Portion** (₹0.00).
  5. Customer clicks **Add to Order**, then clicks the bottom cart drawer and selects **Send Order to Kitchen**.
- **State Change**:
  - An `Order` record is created in status `Placed` with `OrderChannel = DineIn` and `TableNumber = 1`.
  - An inventory stock reservation is registered for the 21 ingredients. **No physical KOT is printed yet** (preventing ghost orders).
- **Screenshots & Cases**: [CUST-001-table-1-qr-menu.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CUST-001-table-1-qr-menu.png), `TC-CUST-001`.

### Step 2: Waiter Accepts Order & Reviews Dining Floor
- **Starting Route**: `http://127.0.0.1:5173/dashboard?tab=floor`
- **Role**: Waiter (`waiter@tablodemo.com`) or Restaurant Admin
- **Workflow**:
  1. The waiter opens the **Floor & Orders** view.
  2. Table 1 displays an amber alert showing a new pending QR order.
  3. Waiter reviews the requested items and clicks **Accept Order**.
- **State Change**:
  - Order status advances to `Accepted`.
  - SignalR hub broadcasts `NewOrder` to kitchen displays.
  - The KOT routing engine evaluates item categories: Food items are queued to the **Food Kitchen** station (`127.0.0.1:9100`), and drink items are queued to the **Beverage Station** (`127.0.0.1:9101`).
- **Screenshots & Cases**: [ADM-002-floor-and-orders.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-002-floor-and-orders.png), `TC-OPS-002`.

### Step 3: Kitchen Prepares Food & Advances Ticket Status
- **Starting Route**: `http://127.0.0.1:5173/dashboard?tab=kitchen`
- **Role**: Kitchen Chef (`kitchen@tablodemo.com`)
- **Workflow**:
  1. Chef views the incoming ticket card on the **Kitchen Queue**.
  2. Chef clicks **Start Preparing** when the station begins cooking.
  3. When dishes are plated, Chef clicks **Mark Ready**.
- **State Change**:
  - Order status transitions from `Accepted` → `Preparing` → `Ready`.
  - If any course was marked **Held**, it displays an amber `HELD ⏸` banner and does not generate production tickets until staff triggers **Fire Course**.
- **Screenshots & Cases**: [ADM-004-kitchen-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-004-kitchen-queue.png), `TC-KIT-001`.

### Step 4: Waiter Serves & Manages Split Bills
- **Starting Route**: `http://127.0.0.1:5173/dashboard?tab=floor`
- **Role**: Waiter / Cashier
- **Workflow**:
  1. Waiter delivers the food to Table 1 and marks the order **Served**.
  2. Guests request separate checks: Waiter opens Table 1, clicks **Split Bill**, enters equal division (50% / 50%) or itemized allocation.
  3. The system validates that exact paise allocation matches total (`child.Sum() == parent.TotalAmount`) before issuing child bills.
- **State Change**:
  - Parent bill splits into two independent child bills, each with a unique revision identifier.
- **Screenshots & Cases**: [WTR-003-waiter-floor-view.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-003-waiter-floor-view.png), `TC-OPS-002`.

### Step 5: Multi-Method Collection & Receipt Generation
- **Starting Route**: `http://127.0.0.1:5173/dashboard?tab=floor`
- **Role**: Cashier (`cashier@tablodemo.com`) / Waiter
- **Workflow**:
  1. Guest pays ₹200 in Cash: Cashier enters `₹200.00`, selects **Cash**, and clicks **Record Payment**.
  2. Remaining balance updates immediately from ₹672.00 to ₹472.00.
  3. Guest pays remaining ₹472 via UPI: Cashier enters `₹472.00`, selects **UPI**, and clicks **Record Payment**.
  4. Balance reaches ₹0.00: Bill status transitions to `Settled`.
  5. Cashier clicks **View Receipt** to review or print the final tax receipt.
- **State Change**:
  - Exactly one stock deduction is executed in the inventory ledger for all 21 biryani ingredients.
  - Payment records are written to `BillPayments` with actor ID and UTC timestamps.
- **Screenshots & Cases**: [FLR-001-table-1-settled-review.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/FLR-001-table-1-settled-review.png), `TC-OPS-003`.

### Step 6: Reconciliation in Payment & Gross Profit Reports
- **Starting Route**: `http://127.0.0.1:5173/reports`
- **Role**: Restaurant Admin
- **Workflow**:
  1. Admin opens **Payment Reports**.
  2. Selects today's date range in IST.
  3. Verifies that the ₹672.00 Table 1 collection appears under Cash (₹200) and UPI (₹472).
  4. In Inventory, verifies the gross profit report: Net Sales = ₹640.00, COGS = ₹227.55, Profit = ₹412.45, Margin = 64.45%.
- **Screenshots & Cases**: [REP-001-payment-reports.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/REP-001-payment-reports.png), `TC-REP-001`.

---

## 2. Dedicated Staff Order Taking Workspace
- **Starting Route**: `http://127.0.0.1:5173/dashboard?tab=order`
- **Role**: Waiter / Admin
- **Step-by-step Flow**:
  1. Waiter navigates to **Staff Order Taking** from the sidebar or tab bar.
  2. Click any table card (e.g., **Table 2**) from the table grid.
  3. If table has active dining groups, select **Group A** or click **New Group** to create an independent tab.
  4. Search dishes in the search bar (e.g., "Biryani" or "Cooler") or browse category pills.
  5. Click items to add to cart; customize modifiers if prompted.
  6. To hold a dish for later, click the course badge and select **Hold Course**.
  7. If called to another table, click **Table 4**: Table 2's draft is preserved in local storage.
  8. Return to Table 2 and click **Send Order** to submit.
- **Screenshots**: [WTR-001-waiter-order-taking.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-001-waiter-order-taking.png), [WTR-004-order-taking-selected-table.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/WTR-004-order-taking-selected-table.png).

---

## 3. Parcel Counter, Scheduled Pickup & Car-Side Delivery
- **Starting Route**: `http://127.0.0.1:5173/admin` or `http://127.0.0.1:5173/dashboard`
- **Role**: Cashier / Counter Staff
- **Step-by-step Flow**:
  1. **Walk-in Parcel**: Enter customer name, phone, select dishes with packaging intent "Takeaway", and assign Daily Token Number (e.g., #201).
  2. **Advance Scheduled Pickup**: Customer calls ahead for 2 hours later. Staff checks **Scheduled Order**, selects pickup time in IST. The backend marks order as `Scheduled` without firing food early; the background release worker releases the ticket to the kitchen when lead time is reached.
  3. **Car-Side Ordering**: Enter Vehicle Plate (`KA-01-MJ-5522`), Vehicle Model (`White Creta`), and Parking Bay (`Bay 3`). Kitchen ticket prints bay details for runner delivery.
- **Screenshots**: [ADM-001-admin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-001-admin-overview.png), `TC-OPS-001`.

---

## 4. Purchasing, Goods Receiving & Quarantined Lots
- **Starting Route**: `http://127.0.0.1:5173/inventory?tab=purchasing`
- **Role**: Restaurant Admin / Inventory Manager
- **Step-by-step Flow**:
  1. Navigate to **Purchasing & POs**.
  2. Click **Create Purchase Order**: Select supplier *Kadal Produce Cooperative*, add 4 crates of Fresh Limes, and submit.
  3. Admin approves PO status to **Approved**.
  4. When delivery arrives with 2 crates: Click **Receive Goods**, enter supplier invoice `KADAL-DEMO-001`, received quantity (2 of 4 crates), freight/tax, and lot code `KADAL-FRESH`.
  5. If damaged items are detected: Click **Supplier Return**, enter returned quantity and reason ("Damaged produce").
  6. For a separate delivery with questionable quality: Receive with lot code `WEST-HOLD`, navigate to **Lots & Expiry**, and click **Quarantine Lot** ("Quality inspection").
- **Screenshots**: [INV-002-inventory-purchasing-pos.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-002-inventory-purchasing-pos.png), [INV-003-inventory-lots-expiry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-003-inventory-lots-expiry.png).

---

## 5. Physical Stocktakes & Outlet Transfers
- **Starting Route**: `http://127.0.0.1:5173/inventory?tab=transfers`
- **Role**: Restaurant Admin
- **Step-by-step Flow**:
  1. **Stock Transfer**: Navigate to **Outlet Transfers** → click **New Transfer**.
  2. Select Source: *Heritage Central Kitchen*, Destination: *MG Road Counter*, Item: *Lime cordial (200 ml)*.
  3. Dispatcher marks **Dispatch (200 ml)**.
  4. Destination receiver counts physical stock upon arrival (190 ml received, 10 ml spilled in transit). Receiver enters received quantity and discrepancy reason. Both inventory ledgers update automatically.
  5. **Stocktake Cycle**: Navigate to **Stocktakes** → click **Start Stocktake Cycle**.
  6. Staff enters physical counts against system on-hand quantities; variances are computed and reviewed before posting adjustments.
- **Screenshots**: [INV-004-inventory-outlet-transfers.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-004-inventory-outlet-transfers.png), [INV-008-inventory-stocktakes.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-008-inventory-stocktakes.png).

---

## 6. Platform Super Admin Plan Packaging & Feature Management
- **Starting Route**: `http://127.0.0.1:5173/super-admin`
- **Role**: Platform Super Admin (`superadmin@example.test`)
- **Step-by-step Flow**:
  1. Open `/super-admin` and sign in.
  2. Click **Plans & Features** tab to inspect all 3 tiers (**Basic**, **Plus**, **Premium**).
  3. Click **Configure Features** on any plan: Toggle capabilities (e.g. `inventory_core`, `thermal_printing`, `carside_ordering`). Feature dependencies (such as purchasing requiring inventory) are strictly checked.
  4. Navigate to **Subscriptions**: Switch any restaurant's subscription tier with an audited reason.
  5. Navigate to **Audit Log** to review immutable logs of all administrative actions.
- **Screenshots**: [SA-001-superadmin-overview.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-001-superadmin-overview.png), [SA-003-superadmin-plans-features.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/SA-003-superadmin-plans-features.png).

---

## 7. Manual Marketplace Delivery Order Entry & Settlement (Swiggy / Zomato)
- **Starting Route**: `http://127.0.0.1:5173/admin?tab=parcel`
- **Role**: Counter Staff / Cashier / Manager
- **Step-by-step Flow**:
  1. In **Parcel Counter & Takeaway**, click the channel switcher: **🛵 Swiggy / Zomato**.
  2. Select Provider: **Swiggy** or **Zomato** (or **Other Delivery** with custom name).
  3. Enter the marketplace app's **External Order Reference** (e.g. `SWIGGY-8921` or `ZOM-4412`).
  4. Enter the partner statement amounts (Entered Total, Tax, Packaging, Discount). The UI displays a live comparison against TABLO's calculated total and flags any discrepancy.
  5. Select dishes and beverages from the menu catalog.
  6. Click **Print & Place Order**:
     - Daily token sequence `#001` is allocated uniquely per IST business date.
     - Unique PostgreSQL index on `(RestaurantId, OutletId, DeliveryProvider, NormalizedExternalReference)` guarantees duplicate references cannot be placed.
     - Dual-station KOT routing fires immediately to Food Kitchen (9100) and Beverage Station (9101) with `*** SWIGGY DELIVERY ***` banner.
  7. **Settlement**: Click **Collect Payment** on the delivery order card:
     - Select **🛵 Marketplace Collected (Prepaid)**.
     - Confirms payment without inflating physical cash drawers, UPI, or card machine totals.
     - Generates 1 immutable receipt and consumes physical recipe ingredients once.
- **Screenshots**: [DLV-001-swiggy-order-entry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-001-swiggy-order-entry.png), [DLV-002-zomato-order-entry.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-002-zomato-order-entry.png), [DLV-003-marketplace-queue.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-003-marketplace-queue.png).

---

## 8. Marketplace Payout Statement Reconciliation & Shortage Auditing
- **Starting Route**: `http://127.0.0.1:5173/admin?tab=parcel` -> **Marketplace Payouts** tab
- **Role**: Restaurant Admin / Accountant
- **Step-by-step Flow**:
  1. Click **Marketplace Payouts** in the Parcel Counter navigation header.
  2. Click **Record Marketplace Payout**.
  3. Enter partner statement reference (e.g. `SWIGGY-PAY-20261121`), gross order GMV, commissions deducted, TCS/GST, and net bank deposit.
  4. System validates net deposit against computed formula: `Gross - Commission - TCS/GST + Adjustments`.
  5. Dashboard updates summary KPI cards: Total Payouts Recorded, Gross GMV Reconciled, Total Commission & Tax Deducted, and Net Payout Received.
  6. **Outlet Transfer Shortage Audit**:
     - Navigate to `http://127.0.0.1:5173/inventory?tab=transfers`.
     - Click **Shortage Audit & Correction**: Dry-run inspects past transfers where dispatch quantity exceeded receipt quantity (e.g. 10,000 g dispatched, 9,500 g received).
     - Clicking **Apply Compensating Movements** appends traceable `TransferDiscrepancyCorrection` (+500 g) movements without rewriting or deleting historical ledger rows.
- **Screenshots**: [DLV-004-payout-reconciliation.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/DLV-004-payout-reconciliation.png), [INV-009-transfer-shortage-correction.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/INV-009-transfer-shortage-correction.png).
