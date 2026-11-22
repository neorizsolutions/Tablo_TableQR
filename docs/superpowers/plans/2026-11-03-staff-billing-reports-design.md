# Staff ordering, billing and payment reports completion

## Scope selected by the user

Complete staff ordering, billing and reports first. Use https://www.dinesmenu.in/ as a workflow reference and the supplied Tablo restructuring blueprint as the architecture direction. Keep the existing .NET 8, React, PostgreSQL and SignalR stack.

## Reference findings

Dinesmenu's public home/features pages describe shared QR/waiter orders, table-based billing, sales and payment reports, top-selling items and export. Its private billing screens and transaction rules have not been inspected. This design matches the public operational workflow; it does not claim exact private-screen parity.

## Proposed design

### Staff ordering and billing

Retain the Admin/Waiter service desk and shared OrderService. Select a table, add menu items and submit to the existing preparation workflow. Additional items join the active preparation order; later orders remain grouped under the open table bill. Keep tenant checks, durable retry identities, stale-review rejection and table-row serialization.

Retain full Cash, UPI or Card settlement after all chargeable orders are served. These record payment collected externally. Receipt history uses stored settlement snapshots. Cancelled/rejected orders do not contribute to the payable total. Authentication expiry must preserve a pending operation through same-account sign-in.

Add a printable settled receipt using the stored receipt snapshot and restaurant identity, with browser print support. Clearly label it a payment receipt; do not invent GST registration details, legally compliant invoice numbering or payment-provider confirmation. Thermal hardware printing remains in the KOT roadmap.

### Reports

Keep order history, existing filters, charts, top items and Excel export. Label order-value metrics as order sales rather than payment collections. Correct top-item amount presentation so an assumed extra 5% does not disagree with stored order rounding.

Add an Admin-only payment report, reading settled TableBills rather than inferring payment from Served order status. Show collected total, settled bill count, Cash/UPI/Card totals and paginated settlement history. Include receipt/bill identity, table, settlement time, payment method, amount and recorded employee identity where available. Make receipt details accessible from the report and provide export with matching filters.

Use settlement timestamps for payment dates and creation timestamps for order dates. New payment-date filters use Asia/Kolkata calendar days, converted to an inclusive-start/exclusive-end UTC range. State the time zone on the screen/export. Preserve existing order-date semantics and label them explicitly rather than silently changing historical reports.

Allow date, table and payment-method filters on payment reports. Order-status and order-number filters remain specific to order history. Use a shared filter implementation for payment list, summary and export. Refresh reports after settlement and provide explicit refresh/loading/error/empty states. Bound export volume with a clear actionable error rather than silently truncating results.

### Compatibility and release

Read the existing additive billing schema; no additional schema migration is expected for payment reporting. Preserve separate preparation and payment state. Do not replace KDS until the independent KOT/printing foundation is implemented. Split tender, refunds, parcel/car channels, tax-engine changes, inventory and SaaS plans remain in later blueprint phases.

Fix the six existing frontend TypeScript errors as part of finishing this module. Preserve unrelated workspace changes. Do not commit, deploy or apply migrations to an existing application/production database.

## Acceptance and verification

- Staff and QR orders share a table bill without duplicated items or payment records after retries.
- An unserved order cannot settle; stale totals/revisions cannot settle.
- Cash/UPI/Card payment totals reconcile exactly to matching settled bills, including zero-value closures; open/unpaid orders and other tenants are excluded.
- Settlement-date boundaries, table/method filters, pagination and exports agree.
- Historical receipt values do not change when menu/table/staff data later changes.
- Admin reports deny Waiter, Kitchen, anonymous and cross-tenant access.
- Browser receipt printing uses the correct snapshot and excludes dashboard controls.
- Full backend suite including actual PostgreSQL concurrency/rollback tests passes with no skipped PostgreSQL coverage.
- Frontend regression tests, TypeScript check and production build pass.
- Local browser checks cover waiter ordering, QR updates, settlement, receipt history and Admin payment reporting/export.

## Status

Approved by the user in chat and implemented. Verification and the remaining normal-browser print acceptance check are recorded in 2026-11-03-staff-billing-reports.md.
