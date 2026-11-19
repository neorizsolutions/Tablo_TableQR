# Master restructuring implementation ledger

User authorized implementation of the full supplied v2 blueprint on 2026-11-16. The earlier table-splitting design is also approved. Existing code and billing history must be preserved. No production deployment, commit or application-database migration is authorized.

## Execution sequence

1. Dining groups, item/amount allocations, payment ledger and transfers; migrate existing settlements without duplication; update staff/QR/reporting and concurrency tests.
2. Order channels and stations, daily parcel tokens, packaging and scheduled orders; KOT snapshots and durable routing jobs.
3. Print worker/ESC-POS adapters and printer management, counter/car UI, hold/fire, notes, modifiers and refill tickets. Physical validation depends on actual printer details.
4. Subscription tiers and enforced module gates, SuperAdmin management and existing-tenant migration.
5. Configurable food/alcohol tax, GSTIN, UPI intent display, Khata, manager-authorized comps/cancellation and shift cash/denominations. Do not claim a UPI intent confirms payment.
6. Inventory recipes/yield/stock ledger, automatic depletion, nightly audits, multi-brand/outlet foundations and UI.
7. Token TV, integration/upgrade/tenant/concurrency/browser tests, operator documentation and release review.

## Rules

- Tests precede business-rule implementation; preserve totals and original order identity.
- Every mutation requires tenant/actor validation and durable retry identity.
- Test migrations only in an isolated disposable local database.
- Keep KDS operational until printing has been verified; never discard the existing order workflow prematurely.
- No claim of physical printing or payment-provider confirmation without observed evidence.
- User input requested for printer models/connections and restaurant state/tax configuration; independent work continues.

## Progress

- Repository inspected: previous staff/billing/report module is committed; the following foundation milestones predate the floor integration.
- Previously referenced superpowers skill files are unavailable at their old location; continue using the approved design and explicit user authorization.
- Added pure item/amount split calculations with deterministic paise allocation, quantity conservation and validation.
- Added tenant-scoped DiningGroup records and replay-safe creation service; labels may be reused after a visit closes.
- Added BillPayment ledger records and a partial-payment foundation service: immutable acknowledgement replay, exact balance checks, final mixed-method receipt snapshots and bill revision freezing.
- Added compatibility guards so existing full settlement cannot ignore a partial ledger payment, and frozen paid bills reject item attachment.
- Generated `20261116092356_AddDiningGroupsAndPaymentLedgerFoundation` and inspected its SQL. It adds tables/nullable revision and tenant constraints; it does not alter legacy receipt JSON or initialize historical payment entries yet.
- Verification: 124 backend tests pass, zero skipped, including 12 real PostgreSQL tests. New concurrency tests observe blocked table-row writers; an upgrade test applies the prior billing schema then upgrades and verifies an existing settlement/receipt survives. Frontend TypeScript checking and ten existing frontend tests pass. EF reports no pending model changes.
- Physical printer guidance from user: use the restaurant's existing thermal printer. Actual models/connections remain unspecified. No alcohol-tax assumptions made.

## Floor integration and verification, 16 November 2026

- Staff and QR flows resolve independent dining groups. Splits create child bills and leave kitchen orders intact. Payment entries retain their collection location across transfers. A group bill request remains active until its last open bill settles.
- `20261116094941_AddFloorOperationsAndGroupOwnership` backfills existing foundation payment locations from tenant-matched bills. `20261116133614_EnforceServiceRequestGroupTenant` adds a composite foreign key for service requests. Migrations were exercised in disposable PostgreSQL databases. Use a forward fix or a database backup if rollback is needed after floor activity: the old schema cannot represent multiple open bills at one table.
- Full backend run: 183 passed, zero skipped, including 19 PostgreSQL tests. Covers migration upgrades, split and transfer races, frozen bills, replay and collection attribution. Frontend: 16 tests passed, including storage failure, duplicate clicks, and unchanged replay after authorization or network failures.
- Disposable browser demo: created 4A and 4B, saw both on the QR selector, sent 4A food to the KDS, served it, split ₹105 into ₹50 and ₹55, collected ₹20 Cash, moved to 7A with ₹30 remaining on the first share, collected ₹30 UPI and ₹55 Card, and inspected the mixed receipt preview. The report showed three collections, two settled bills, ₹0 outstanding, ₹20 at 4A and ₹85 at 7A. The export API returned a workbook with three numeric rows totaling ₹105; the browser disconnected during the download.
- Item quantities and retry behavior have automated coverage. Browser item splitting, QR order placement and transfer tracking, forced interruption recovery, and native print/save-to-PDF were not completed in this run. Thermal hardware and payment-provider confirmation were not tested.
- Later blueprint work (execution steps 2–7) remains open. No application database was migrated, and nothing was committed, pushed or deployed.

## Release verification completion, 17 November 2026

- **Item 1 (Transfer Recovery After Lost Response): VERIFIED & PASSED.**
  - Tested in Google Chrome with Playwright network interception: Group 4A with active food orders and open bill at Table 4 was transferred to Table 7. The upstream request reached the API, committed to the database, and returned HTTP 200 `TransferResult`. The response was intentionally dropped before browser reception (`route.abort('failed')`).
  - Durable localStorage key `tablo_floor_v1_{employeeId}_{restaurantId}_{tableId}_{groupId}_visit` remained intact. Upon Service Desk reload, the `FloorRecovery` component detected the interrupted action and rendered "Saved actions awaiting confirmation".
  - Retrying from the recovery UI dispatched an identical payload with the original `OperationId` (`327071c8-126f-4121-a34c-1c8b5dc48a98`), source table, destination table, dining group, and actor. The server recognized the replay and returned the original HTTP 200 `TransferResult` (no 409 conflict).
  - The UI and localStorage were cleared. PostgreSQL database verification confirmed exactly one `FloorOperations` audit row for the operation ID, with group, bill, and orders successfully relocated to Table 7.
- **Item 2 (Migration Chain & Historical Data): VERIFIED & PASSED.**
  - All 17 repository migrations match `__EFMigrationsHistory` in the disposable PostgreSQL cluster (`tablo_browser_demo`). Specifically confirmed that `20261116133614_EnforceServiceRequestGroupTenant` is applied.
  - Executed clean upgrade test on a dedicated disposable database (`tablo_upgrade_verification_db`): migrated to foundation (`20261116092356_AddDiningGroupsAndPaymentLedgerFoundation`), seeded a legacy settled receipt JSON (`{"Id":"...","Status":"Settled","TotalAmount":105}`) and foundation ledger payment (`Amount: 40`, `Remaining: 65`), then migrated to latest.
  - Assertions confirmed: legacy receipt JSON was preserved unaltered, payment ledger entry backfilled historical `TableIdSnapshot` and `TableNumberSnapshot` without altering amount or acknowledgement JSON, and the composite foreign key constraint `FK_ServiceRequests_DiningGroups_RestaurantId_DiningGroupId` exists in `pg_constraint`.
- **Item 3 (Native Chrome Printing): VERIFIED & PASSED.**
  - Settle of mixed-payment bill at Table 7A (Cash ₹4.00 + UPI ₹4.49 = ₹8.49 total) opened in Chrome Service Desk -> Recent settled bills -> "View / print receipt".
  - Receipt preview rendered in iframe and native Chrome print engine captured complete, unclipped A4 PDF to `Tablo_Settled_Receipt_Table7A.pdf` (30,468 bytes).
  - Validated legibility and correctness of Table 7A, Cash and UPI collection breakdowns, total, and IST timestamps (`Asia/Kolkata`). Actual "Print receipt" button click in UI verified.
  - Physical thermal printer hardware (ESC/POS) transmission remains unverified (hardware-dependent).
- **Automated Suites:** Backend suite 183 passed (0 failed, 0 skipped, including 19 PostgreSQL integration tests); frontend unit suite 16 passed; TypeScript check 0 errors; production build succeeded. No production application database was touched, and no code was committed, pushed, or deployed.
