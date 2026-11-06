# Table groups, bill splitting, mixed payments and transfers

## Requested scope

Add all four features requested in chat: independent dining groups such as 4A/4B, split bills between guests/items, Cash/UPI/Card payments on the same bill, and table transfers. Extend the existing staff service desk, QR ordering, receipts and payment reports.

## Recommended approach

Introduce explicit dining visits/groups, bill allocations and an immutable payment ledger. Preserve original kitchen orders and item snapshots; splitting a bill must never manufacture kitchen orders or re-send food for preparation. Physical tables retain their existing QR tokens and integer numbers. Group labels are separate from physical table identity.

The simpler alternative, treating 4A/4B as unrelated physical tables, would require duplicate physical table numbers and would not correctly support sharing an existing order between bills. The requested combined scope needs explicit groups and allocations.

## User-visible behaviour

### Independent groups on a table

- Staff can create groups A, B, C, etc. under an active physical table and select them when ordering.
- Each group has its own orders, open bills and balances. Dashboard, customer tracking, receipts and reports display the complete label, e.g. 4A.
- The existing physical-table QR opens group selection when multiple groups are present. A guest must select a group before ordering; no automatic assignment to another group's bill.
- Existing unsplit orders retain their group and history. Staff can split their bill explicitly; creating a group alone does not move existing food/orders.
- Group names can be reused after the previous visit closes; visit IDs prevent mixing a new group with old receipts.

### Split bills

- Staff can split an unpaid bill by selecting item quantities or by entering guest shares/amounts. The screen previews the resulting bills and exact totals before confirmation.
- A quantity can be allocated only once. Allocations cannot exceed the original ordered quantity. Cancelled/rejected quantities are excluded.
- Tax and discount allocations preserve the existing stored order totals. Allocate fractional paise deterministically and assign the remainder so child totals equal the original total exactly. Do not recalculate a new 5% charge per split.
- Amount splits create explicit shares of the source bill; they do not claim that specific dishes belong to a guest. Receipts identify amount shares accordingly.
- Original kitchen order IDs, statuses and quantities remain intact. Allocation records link split receipts to their source items/bill.
- Reject splitting after any payment has been recorded. Refunds and redistributing existing payments are separate work.

### Mixed payment methods

- Show total, paid and remaining on each bill. Record Cash, UPI or Card payments individually, each with its own durable operation ID, employee, timestamp and acknowledgement snapshot.
- Example: a bill of 1,000 can accept Cash 400 and UPI 600; it settles only when remaining reaches zero.
- Preserve the current rule that all chargeable food must be served before recording payment. Reject negative, zero ordinary payments and amounts above the outstanding balance; preserve zero-value bill closure as a separate operation.
- Freeze bill allocations/amounts after the first payment. Additional food creates another bill for the same group/visit.
- Retrying a successful payment returns its original acknowledgement; simultaneous payments cannot overpay a bill.
- The final bill receipt lists every payment method and amount. Payment collection continues to mean externally collected money, with no gateway processing claim.

### Table transfers

- Staff select an open dining group and an active destination physical table, preview the move and confirm it once.
- Transfer to an empty destination. Reject an occupied destination rather than merging unrelated bills implicitly.
- Move the group's open visit, active orders, unpaid balances and outstanding service requests together. Preserve any partial payments and all original IDs.
- Lock source and destination in stable ID order and revalidate after locking. Concurrent submissions, payments and transfers must serialize safely.
- Settled receipt snapshots retain the original recorded table/group. Record an audit event with source, destination, actor and timestamp.
- Refresh staff/KDS labels and customer tracking after commit. Guests scanning the old table after the move cannot append to the transferred visit; existing tracking shows the new table and guests use the new table QR for subsequent orders.
- Future independent KOT routing must be able to consume the transfer audit/event. KOT generation and physical printer commands are not introduced by this feature.

## Reporting and historical compatibility

- Payment collections switch to ledger entries, including partial collections on open bills. Date filters use the actual payment time in Asia/Kolkata.
- Clearly distinguish payment count, collected amount, fully settled bill count and outstanding balances. Cash/UPI/Card totals sum the ledger entries rather than classifying a mixed bill under one method.
- Preserve original payment dates/methods/amounts for existing settled bills when initializing ledger history. Use deterministic unique legacy-payment identifiers to prevent duplicate initialization.
- Historical receipt JSON is retained. New split receipts and mixed-payment details use explicit snapshot fields; missing legacy information remains labelled.
- Order sales and top-item reports continue to count original orders/items exactly once, irrespective of the number of bill splits or payments.

## Implementation and migration

Add tenant-scoped group/visit, allocation, payment and operation/audit records, plus explicit references on orders/bills. Review and replace the current one-open-bill-per-physical-table uniqueness rule with visit/group-aware constraints. Retain tenant composite foreign keys.

All changes are additive except necessary constraint replacements. Generate and inspect the migration SQL; test upgrading an existing staff/billing database and prove old receipts and totals survive. Apply only to a newly isolated local test/demo database. No existing application/production migration, commit or deployment is authorized by this document.

## Acceptance tests

- Two groups on one table order concurrently without mixing their items or bills; QR selection routes correctly.
- Item and amount splits preserve total/subtotal/tax/discount, including awkward rounding and quantity splits.
- Duplicate/concurrent split requests neither lose nor duplicate allocations. Paid bills reject splitting.
- Concurrent partial payments cannot exceed the balance; retries return the original result.
- Payments across different days appear on their actual collection days; a later final settlement does not count earlier payments again.
- Transfers preserve balances/partial payments, reject occupied/inactive/foreign-tenant targets and do not deadlock opposing requests.
- Original kitchen orders and sales reports remain counted once after splits and transfers.
- Upgrade tests preserve existing bills, legacy payment totals and receipt snapshots.
- Endpoint authorization, real PostgreSQL concurrency/rollback, frontend tests/typecheck/build and local staff/QR/report browser flows pass.

## Status

Concrete design prepared for approval. Production implementation has not started for this additional scope.
