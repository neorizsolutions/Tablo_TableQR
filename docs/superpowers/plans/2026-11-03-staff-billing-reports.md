# Staff billing reports implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Finish staff billing with reconcilable payment reporting, printable historical receipts and clean frontend checks.
**Architecture:** Read settled TableBills through a tenant-scoped Admin report service. Keep order reports distinct; share receipt rendering across staff and reporting.
**Tech Stack:** .NET 8 / EF Core / PostgreSQL / React / TanStack Query.
**Spec:** 2026-11-03-staff-billing-reports-design.md (approved in chat).

## Global constraints
- No production/database configuration changes, commits or deployment.
- Preserve the existing feature in the current authorized workspace.
- No new schema migration; new identity snapshots fit existing receipt JSON.
- Payment dates are Asia/Kolkata; existing order dates remain UTC.

## Review focus
- Midnight boundaries, identical timestamps and out-of-range pagination.
- Open bills, cancelled lines, zero closures and foreign tenant receipts.
- Saved receipts after menu/profile changes; missing legacy snapshot identity.
- Reports changing while paginating/exporting; exports must not silently truncate.
- Untrusted receipt text and print content isolation.

## Tasks
- [x] Add failing backend tests for settled totals, IST boundaries, filters, pagination, export limits, receipt snapshots and endpoint authorization.
- [x] Implement DTOs/PaymentReportService/PaymentReportsController, shared filter query, consistent summary/list transaction, bounded Excel export and receipt lookup.
- [x] Add failing frontend tests for safe receipt markup/filter encoding; implement payment report panel and printable snapshot receipt.
- [x] Wire reports refresh and settlement invalidation; label sales and fix top-item pre-tax amounts.
- [x] Fix six compiler errors; run backend/PostgreSQL tests, frontend tests/typecheck/build/lint.
- [x] Verify local browser staff/QR/settlement/payment report/export/print, request final independent review and update operator guide.

## Execution ledger
- Design approved by user. Continue in existing workspace because this task completes its uncommitted module; no branch integration requested.
- Ruling: retain existing order report UTC dates and label them; payments use IST. Historical order reports should not change dates silently.
- Ruling: store restaurant name and actor ID in new receipt JSON; legacy receipts show missing identity explicitly. No retroactive snapshot invention.

- Red→green evidence: new backend tests first failed on missing report/receipt APIs; frontend print tests first failed on missing markup generator. Implementations passed focused tests, followed by 110 backend / 10 frontend tests and clean TypeScript/build.
- Final independent review found no critical/important issues.
- Browser/API evidence: waiter denied Reports; Admin collections 15 = Cash6 + UPI6 + Card3; order sales18 include a new unpaid3. UPI filter6, Excel download and both legacy/new receipt rendering verified. QR tracking updated3→6 without reload.
- Ruling: use standard URLSearchParams encoding; verify filters through browser/API tests rather than a unit test mirroring browser library internals.
- Print limitation: receipt iframe and escaped print document verified; embedded-browser native print action timed out. Normal browser print dialog/hardware output remains an operator acceptance check.
- EF Core verification: has-pending-model-changes returned no model changes since the existing migration. No new migration generated or applied.
