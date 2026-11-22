# Staff ordering and billing implementation plan

Scope authorized: staff create/add table orders, bill review and full settlement. This is separate from KOT implementation. Work in the current user workspace; no commits or deployment.

Design: add an Admin/Waiter service desk to the existing dashboard. A table bill groups all new orders for one visit, including orders already served. Settlement is a separate recorded operation and never advances kitchen status. Cash, UPI and card describe payment collected externally; no payment gateway is involved. Only served, non-cancelled orders are chargeable at settlement. Cancelled/rejected orders are excluded. Historical served orders are not automatically imported because payment history is unknown; staff can explicitly start a bill for currently active legacy orders.

Backend: additive TableBills and StaffOrderSubmissions, nullable Order.TableBillId; use one table-row lock for QR submissions, staff submissions, status changes and settlement. Staff submissions have durable operation IDs. Settlement checks the reviewed bill revision and exact server amount, records actor/method/time, and is replay-safe. Bill history and order links are retained. Preserve existing 5% whole-unit rounding per order.

- [x] Write billing/staff behavior tests: totals, cancelled exclusion, unsettled preparation, stale review, duplicate settlement, cross-tenant access, actor validation and historical isolation.
- [x] Run tests to establish missing-feature failure.
- [x] Implement domain, DTOs, scoped services/controllers and transactional order integration.
- [x] Generate and inspect additive migration without applying it to any existing database.
- [x] Add dashboard service desk, menu cart, bill review, settlement confirmation and receipt history.
- [x] Verify backend suite and frontend build/type checks; test PostgreSQL migration/concurrency in a disposable local database if available.
- [x] Review all changes for tenant isolation, retry handling and customer-flow regression; report validation limits and unapplied migration.

Review focus: served orders remain on an open bill; settlement never changes preparation; stale total/revision cannot settle; retry after lost response does not duplicate items/payments; old served orders stay out of new bills.

Verification: 40 backend tests passed, including five PostgreSQL migration/concurrency tests and seven HTTP JWT authorization cases. Production frontend build and lint of new frontend files passed. TypeScript checking found six existing errors in sign-out handlers, customer tracking types/status and mock order financial fields; no new service-desk errors. Independent review identified a stale retry notification bug, reproduced by a failing regression and fixed by loading current status for notifications. PostgreSQL tests exposed timestamp/numeric canonicalization differences, also fixed. No application migration, deployment or commit performed.

2026-11-03 reliability completion: preserve operation identity through authentication failures and offer same-account sign-in recovery; refetch complete customer order snapshots on matching events/reconnection with polling fallback; broadcast completed bill requests after settlement commits. Regression coverage expanded to 73 passing backend tests (eight real PostgreSQL tests, observed lock waiters and transaction rollback fault injection) and seven passing frontend tests. Production build passes; the same six baseline TypeScript errors remain. Isolated local demo created on port 55439 in database tablo_staff_local_20261103_21be9fc8; migrations applied only there. No production/application database changes, commit or deployment. Independent follow-up review found no critical/important regressions.
