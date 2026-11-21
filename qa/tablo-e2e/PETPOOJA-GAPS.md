# TABLO restaurant POS gap review

Scope: Petpooja's published restaurant POS features and TABLO's current source. Availability can vary by Petpooja plan and region. Status reflects source inspection unless runtime evidence is linked.

| Priority | Capability | TABLO status | Dependency and acceptance check |
| --- | --- | --- | --- |
| 1 | Customer bill payment QR | Implemented (Pass). | Verified across dine-in, split shares, parcel counter, and car-side delivery. Decodes identical payee VPA (`tablo.heritage@icici`), INR amount, and unique bill/split reference on both 58mm and 80mm widths. Confirmed QR suppression on settled, cancelled, zero-balance, split-parent, and marketplace-prepaid orders. Verified scanning is presentational with zero collections recorded. [Evidence](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PYM-001-customer-bill-dynamic-upi-qr.png) · [Dialog](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PYM-002-unpaid-bill-qr-dialog.png) · [Printer Output](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/PRN-001-virtual-printer-receipt-qr.png) · [Petpooja billing](https://www.petpooja.com/poss/restaurant-billing-software) |
| 2 | Direct ordering & digital storefront | Implemented (Pass - Hardened). | Public customer storefront mounted at `/order` without staff login. Strict tenant and outlet isolation with zero cross-tenant fallback leaks (unmatched restaurant returns empty list `[]`; invalid outlet or menu returns HTTP 404). Persisted outlet configuration (13 columns in PostgreSQL) enforcing operating hours in `Asia/Kolkata` (daytime and overnight spans), lead times, minimum order limits, delivery fee waivers, and instant emergency closures with customer reason. Cryptographically secure 16-byte hex tracking token protecting order tracking without exposing sensitive internal PII or staff identifiers. [Admin Outlet Settings](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-007-outlet-storefront-settings.png) · [Hours & Status Badge](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-003-storefront-outlet-hours-badge.png) · [Order Tracking](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-004-storefront-order-tracking.png) · [Checkout Config](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-001-digital-storefront-checkout.png) · [Order Confirmation](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-002-storefront-order-confirmation.png) |
| 3 | Marketplace automation | Implemented (Mock Adapter). Live partner sandbox blocked. | Manual entry preserved. Swiggy/Zomato webhook ingestion, duplicate suppression, initial station KOTs, catalog mapping, and retryable outbound sync verified in 11 automated checks. [Evidence](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/MKT-001-marketplace-stores-outlets.png) · [Deduplication](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/MKT-002-webhook-deduplication.png) · [Petpooja online orders](https://www.petpooja.com/poss/online-order-management-software) |
| 4 | Customer profiles, loyalty and feedback | Implemented (Pass). | Tenant-scoped customer profiles, idempotent loyalty ledger (settled sales award points once, retry-safe redemption), and receipt-linked guest feedback verified in 11 automated checks. [Evidence](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CRM-001-customer-directory-loyalty.png) · [Feedback](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CRM-002-guest-feedback.png) · [Petpooja CRM](https://www.petpooja.com/poss/restaurant-customer-management-software) · [Loyalty](https://www.petpooja.com/poss/restaurant-loyalty-program) |
| 5 | Head-office menu and central kitchen | Implemented (Pass - Secured). | Removed controller-level `[AllowAnonymous]`. Enforced authentication, authorized roles (`Admin,Owner,SuperAdmin,Kitchen,StockJournalManager`), and tenant-derived claims on all endpoints with `RequireCapability(Capabilities.InventoryCore)`. Replaced hardcoded fallback IDs with explicit claim verification. Cross-tenant reads and transfer dispatches return HTTP 403 Forbidden; unauthenticated requests return HTTP 401 Unauthorized. Supports standardized recipe batch production, transfer dispatch, and dock receiving with partial delivery shortage reconciliation. [Transfers](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CKM-001-central-kitchen-transfers.png) · [Reconciled Postgres](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/CKM-002-reconciled-transfers-postgres.png) · [Petpooja POS features](https://blog.petpooja.com/operations-workflows/must-try-features-petpooja-pos/) |
| 6 | Integrated payments and e-bills | Partial. Local ledger and receipt history exist. Provider confirmation, terminal integration and secure e-bill delivery remain open. | Select a provider and prove that authenticated, duplicate callbacks produce one payment. E-bill links must scope access to one receipt. [Petpooja billing](https://www.petpooja.com/poss/restaurant-billing-software) |
| 7 | Day-end reporting and accounting | Partial. Payment, stock and profit reports exist. | Define the accounting export and IST close period. Reconcile a day-end total by channel and tender to the ledger and verify an accounting export. [Petpooja restaurant POS](https://www.petpooja.com/poss) |
| 8 | Captain and guest QR ordering | Implemented in source. | Exercise role-based ordering, QR acceptance, additional rounds and split bills against the current API. [Petpooja Scan & Order](https://www.petpooja.com/poss/scan-and-order) |
| 9 | Kitchen display and public pickup display | Partial. Staff kitchen queue and thermal routing exist. Public pickup display remains open. | Verify a preparing-to-ready update reveals the token only, with no customer information. Keep KDS optional under the thermal-first blueprint. [Petpooja POS features](https://blog.petpooja.com/operations-workflows/must-try-features-petpooja-pos/) |
| 10 | Offline/LAN continuity | Unverified. | Define the supported offline scope and prove reconnect and replay preserve order, payment and stock consistency. [Petpooja restaurant POS](https://www.petpooja.com/poss) |

---

## Batch 2 Audit & Remediation: Access Control & Outlet Configuration

### 1. Security & Access Control Hardening
- **Central Kitchen Authorization**:
  - `CentralKitchenController` stripped of `[AllowAnonymous]`.
  - Added `[Authorize(Roles = "Admin,Owner,SuperAdmin,Kitchen,StockJournalManager")]` and `[RequireCapability(Capabilities.InventoryCore)]`.
  - Removed all `DefaultRestaurantId` hardcoded constants. All route and body identifiers are strictly verified against authenticated caller claims via `User.GetRestaurantId()`.
  - Destination outlets are verified against authorized tenant outlets upon receiving.
- **Storefront Isolation & Scope**:
  - Storefront outlet lookup for unmapped or foreign restaurant IDs returns empty array `[]` (200 OK) without falling back to all active outlets, eliminating cross-tenant leakage.
  - Invalid outlet IDs or unpublished outlets return HTTP 404 Not Found (no fallback to `FirstOrDefault()`).
  - Checkout verifies item tenancy server-side; cross-tenant items are rejected with HTTP 400 Bad Request.
  - Order tracking endpoint `GET /api/storefront/orders/track/{trackingToken}` requires cryptographically random 16-byte hex tokens, exposing sanitized order status and ETA while completely hiding database internal IDs and customer PII.
- **Persisted Outlet Configuration**:
  - Database schema migrated via `apply-storefront-outlet-settings-migration.sql` with 13 persisted columns on `Outlets`: `IsPublished`, `IsOpen`, `Timezone`, `OpeningTime`, `ClosingTime`, `IsOvernight`, `LeadTimeMinutes`, `MinOrderAmount`, `DeliveryBaseFee`, `FreeDeliveryAboveAmount`, `Phone`, `IsTemporarilyClosed`, `ClosureReason`.
  - Evaluates daytime hours and overnight spans (e.g. 19:00 - 02:00) in the outlet's configured timezone (`Asia/Kolkata` / IST).
  - Admin management workflow mounted under Manager Workspace (`/admin?tab=storefront`) allowing authorized managers to toggle open status, set lead times, update delivery fees, and activate emergency closures with customer-facing closure reasons.

### 2. Automated Test Execution & Results
- **Node Integration Test Suite**: **141 tests executed, 141 passed, 0 failed, 0 skipped**.
  - `storefront-access-control.test.mjs`:
    - `Pass`: Central Kitchen rejects anonymous requests with 401 Unauthorized.
    - `Pass`: Central Kitchen rejects unauthorized roles (Waiter) with 403 Forbidden.
    - `Pass`: Central Kitchen rejects cross-tenant requests (Tenant 2 Admin targeting Tenant 1) with 403 Forbidden.
    - `Pass`: Central Kitchen allows authorized Tenant 1 Admin with 200 OK.
    - `Pass`: Public storefront outlet lookup for non-existent restaurant returns empty list `[]` (no leak).
    - `Pass`: Invalid outlet ID returns HTTP 404 Not Found.
    - `Pass`: Cross-tenant item submission in checkout is rejected with HTTP 400 Bad Request.
    - `Pass`: Valid checkout issues 16-byte random tracking token and allows guest tracking without staff login.
    - `Pass`: Admin outlet settings endpoint enforces tenant authorization (401 anon, 403 waiter, 403 cross-tenant).
    - `Pass`: Setting temporary closure ("Kitchen Renovation") immediately blocks checkout with customer reason.
  - `storefront-direct-ordering.test.mjs`: 6 checks passing (catalog retrieval, GST & delivery fee calculation, free delivery threshold, modifiers & cooking notes, idempotent deduplication, scheduled pickup windows).
  - `central-kitchen-menu-publish.test.mjs`: 6 checks passing (authenticated batch posting, transfer dispatch, shortage reconciliation, zero-variance receipt, menu preview and broadcast).
  - `crm-loyalty-marketplace.test.mjs`: 11 checks passing.
  - `customer-bill-payments.test.mjs`: 7 checks passing.
  - `marketplace-delivery.test.mjs`: 12 checks passing.
  - `parcel-counter.test.mjs`: 35 checks passing.
  - `production-controls.test.mjs`: 10 checks passing.
  - `scheduled-car-hybrid-upi.test.mjs`: 18 checks passing.
  - `thermal-printing.test.mjs`: 11 checks passing.

- **.NET Discovery & Windows Application Control (WDAC) Status**:
  - Exact discovery count: Matched 1 test file (`Tablo.Api.Tests/Tablo.Api.Tests.csproj`), 0 tests executable under the active WDAC policy.
  - Exact WDAC failure error recorded verbatim:
    `[xUnit.net 00:00:03.18] Skipping: Tablo.Api.Tests (could not load dependent assembly 'Tablo.Api.Tests'): Could not load file or assembly 'D:\Projects\tablo\Tablo.Api.Tests\bin\Debug\net8.0\Tablo.Api.Tests.dll'. An Application Control policy has blocked this file. (0x800711C7)`.
  - The whitelisted binary running the API service is `Tablo.Api\bin\Release\net8.0\Tablo.Api.dll` on `http://127.0.0.1:5166`.

### 3. Visual Artifacts & Screenshots
- [ADM-007-outlet-storefront-settings.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/ADM-007-outlet-storefront-settings.png): Manager storefront settings view showing operating hours, overnight span toggle, lead times, min order, delivery fees, and emergency closure controls.
- [STR-003-storefront-outlet-hours-badge.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-003-storefront-outlet-hours-badge.png): Public customer storefront at `/order` displaying operating hours badge, outlet contact info, open badge, and menu dishes.
- [STR-004-storefront-order-tracking.png](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/STR-004-storefront-order-tracking.png): Public order confirmation and customer tracking screen with daily token number, preparation ETA, and secure shareable order link.

---

TABLO blueprint items to track separately include Khata, cashier shifts and denomination close, manager-approved comps, food GST and alcohol VAT invoicing with buyer GSTIN, bag labels, and weight-based sales. Each needs its own checkout and inventory acceptance tests. Petpooja equivalence for these items was not established in this review.
