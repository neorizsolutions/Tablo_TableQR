# Staff ordering and billing

## Access and scope

Admin and Waiter accounts open **Orders & billing** on the existing dashboard. Kitchen controls and customer QR routes remain in place. The module records externally collected Cash, UPI or Card payment; it does not charge a payment provider, issue refunds, split payments or implement KOT/printing.

## Staff orders

Select an active table, add available menu items and send them to the kitchen. The backend reuses order validation, price/name snapshots, active-order selection and existing per-order 5% tax rounding. Additional submissions append to the active preparation order. Once that order is served, another submission creates another order on the same open table bill.

Each staff submission has a durable operation UUID. The browser saves the request before transmission and reuses it after a timeout/refresh. Authentication failures also preserve the saved request; sign in again with the same account and reselect the table to retry. Same identity and payload returns the original result; changed content conflicts. Requests and receipts are scoped to the employee's restaurant. Notifications are best effort after commit; the panel refreshes bills periodically as a fallback.

## Bills and settlement

New customer and staff submissions automatically attach to an open table bill. Existing active legacy orders can be explicitly attached with **Include current active orders in bill**. Historical served orders are not imported: their prior payment status is unknown.

Bill review includes all attached orders, including served orders. Cancelled/rejected orders are shown but excluded from payable totals. Existing tax is summed per order, not recalculated over the combined bill. A bill cannot settle until every chargeable order is served. A fully cancelled bill can close at zero.

Settlement requires the reviewed revision, exact server total and a durable operation UUID. Concurrent item/status changes invalidate a stale review. Payment confirmation stores employee, method, timestamp, amount and a receipt snapshot. Settlement never advances preparation statuses. Settled orders cannot be edited through the status endpoint. Bill requests for that table are completed on successful settlement and their updated state is broadcast after commit. Notification failures cannot roll back recorded payment. Recent receipts are available per table.

## API

All routes are under `/api/restaurants/{restaurantId}/service-desk` and require an active Admin/Waiter employee with matching JWT restaurant and user claims:

- `GET tables` — service table identifiers, numbers and activity; no QR tokens.
- `POST orders` — `{ operationId, tableId, items: [{ menuItemId, quantity }] }`.
- `GET tables/{tableId}/bill` — open bill, legacy active-order count and ten recent receipts.
- `POST tables/{tableId}/bill/open` — explicitly attach active legacy orders.
- `POST bills/{billId}/settle` — `{ operationId, expectedRevision, expectedTotal, paymentMethod }`.

Customer tracking refetches the complete order after matching staff/status events and reconnects, with a 15-second polling fallback, so items and totals stay current. Existing QR order request/response contracts are unchanged. Their submissions, staff submissions, preparation-status updates and settlements share a table-row lock, so settlement cannot race past an unserved addition.

## Database and release

The additive `AddStaffOrderingAndBilling` migration adds TableBills, StaffOrderSubmissions, nullable Orders.TableBillId, tenant-aware foreign keys and unique open-bill/operation constraints. It does not backfill historical orders or payments. Migration generation does not apply it to a database.

Apply the reviewed migration only through a separately authorized deployment process before running the new backend. Do not run old and new order-writing instances concurrently. After payment records exist, prefer forward recovery; the down migration would remove billing history. Existing public tracking authorization and KOT changes are separate scope.

## Verification

Run `dotnet test Tablo.Api.Tests/Tablo.Api.Tests.csproj` and `npm run build` in the frontend. PostgreSQL tests require a dedicated temporary server with `TABLO_BILLING_TEST_CONNECTION` pointing to host 127.0.0.1, port 55439, database postgres, username tablo_test. The fixture rejects other targets, creates uniquely named `tablo_billing_test_...` databases, applies real migrations, and drops only those test databases. No application connection string is used by these tests.

The PostgreSQL suite covers concurrent retries/additions, rollback, settlement/submission races and new QR orders after settlement. HTTP tests exercise real JWT authentication, role, tenant, missing claims and inactive employee checks. Unit tests cover totals, per-order tax, stale reviews, payment replay and historical isolation.

Implementation verification (2026-11-03): 110 backend tests passed with zero skipped, including nine real PostgreSQL migration/locking/rollback/reporting tests. Ten frontend regression tests, TypeScript checking, new component/service lint and production build passed. The six baseline TypeScript errors have been fixed. Local browser checks cover staff ordering, QR tracking refresh, Admin reporting, method filters, Excel download and receipt rendering. API smoke checks cover UPI/Card settlement replay, completed bill requests, historical receipts and new unpaid bills excluded from collections. The demo uses a separate temporary PostgreSQL database; no existing application or production migration was applied.

## Local acceptance testing

Isolated demo frontend: http://127.0.0.1:5173/auth; local API: http://localhost:5166/api. Demo accounts admin@tablodemo.com, waiter@tablodemo.com and kitchen@tablodemo.com use password123. These accounts and receipts are synthetic data in the temporary demo database.

1. Sign in as Waiter, expand Orders & billing and select a table.
2. Add items and send them. Confirm one kitchen order appears. Send more items and confirm they join the active order and bill.
3. Attempt settlement while the order is preparing: it must remain unavailable. Advance Accepted, Preparing, Ready and Served.
4. Review the bill, choose Cash, UPI or Card, and confirm the externally collected test payment. Confirm the open bill closes and receipt history shows the exact items and total.
5. Refresh and reselect the table: the historical receipt must remain. Start another order: it must use a new open bill.
6. Compare the same table's customer QR tracking while staff add items: quantities and totals must refresh. Check a bill-request notification clears when the table settles.
7. If a request times out or authentication expires, retain its saved request, sign in with the same account and retry. Confirm no duplicate order/payment is created.

Browser smoke testing completed on the isolated demo: waiter login, table selection, order creation, additional items, all preparation transitions, cash settlement and expanded receipt history. Authentication-loss and customer-update recovery are covered by automated regression tests; the remaining multi-browser acceptance scenarios above are recommended for user testing.


## Payment reports and printable receipts

Admin accounts open Reports → Payment collections. These values include only settled bills, using settlement timestamps and Asia/Kolkata (IST) date boundaries. Order sales remain separate and may include unpaid orders. Existing order-date filters use UTC; order history displays browser-local timestamps. Top-item amounts are before tax/discount, with no assumed extra 5% added.

Filter payments by date, table or Cash/UPI/Card. The list, summary and Excel export share filters. Pagination is stable for equal timestamps; each response reads one database snapshot. Export includes all matching bills (ignoring page) up to 10,000; larger exports require narrower filters. Refresh updates the screen immediately; SignalR and 15-second polling also refresh collections.

New Admin-only endpoints under `/api/restaurants/{restaurantId}/payment-reports`:
- `GET /` — summary and paginated settlement history.
- `GET /export` — matching settlements as Excel.
- `GET /{billId}/receipt` — saved receipt snapshot.

Staff can also read saved receipts through `GET /api/restaurants/{restaurantId}/service-desk/bills/{billId}/receipt`. All new endpoints check restaurant identity, active employee and role.

Use View / print receipt from staff receipt history or Admin payment reports. The preview and print document use the saved snapshot. New receipts include restaurant name and recorded employee ID; older snapshots explicitly identify missing historical identity. Cancelled/rejected lines are omitted from the payment receipt. Browser printing is provided, independently of future ESC/POS/KOT hardware support. This is a payment receipt, not a newly introduced tax-invoice numbering system or payment-gateway confirmation.

Additional acceptance tests: sign in as Admin, check the three method totals, filter UPI and select Show receipt → View / print receipt. Export and confirm row amounts match the filtered total. Try a date with no settlements, and an invalid reversed range. Confirm an unpaid order increases order sales but never collections. Actual print/save-to-PDF completion must be checked in a normal browser: the embedded automation browser rendered the preview, but its print action timed out and could not confirm the native print dialog.

No new migration was needed for reporting or receipt identity; new snapshot fields are stored in the existing receipt JSON column. No deployment or commit was made.
