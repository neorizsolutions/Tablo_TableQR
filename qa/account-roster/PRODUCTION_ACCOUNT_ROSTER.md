# TABLO Production Account Roster

**Database**: `dbtabloqr` (AWS Lightsail PostgreSQL / RDS private VPC)  
**Environment**: Production (`https://tablopos.neorizsolutions.com` / `13.126.116.187`)  
**Audit Timestamp**: 2026-11-23 10:18 UTC  
**Access Mode**: Read-Only Query  
**Security Classification**: Restricted - QA & Internal Audit Only  

> [!IMPORTANT]
> In accordance with security policies, all passwords, password hashes (`PasswordHash`), PIN hashes (`ManagerPinHash`), reset tokens (`ResetTokenHash`, `ResetOtpHash`), and database connection credentials have been strictly excluded from queries and this document.

---

## 1. Executive Summary & Aggregate Totals

| Account Category | Source / Management Tier | Total Accounts | Active | Inactive | Dedicated QA Accounts | Real Staff Accounts |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Platform Operators** | External Config (`tablo-api.env`) | 1 | 1 | 0 | 0 | 1 |
| **Restaurant Staff** | PostgreSQL (`Employees`) | 7 | 7 | 0 | 4 | 3 |
| **Customers / Diners** | Per-Order Transient (`Orders`) | N/A* | N/A* | 0 | 0 | N/A* |
| **Total Accounts** | | **8** | **8** | **0** | **4** | **4** |

\* *Customer accounts do not exist as persistent authenticated user entities in PostgreSQL. Customers interact via anonymous guest tokens, table QR session tokens, or direct per-order contact capture.*

### Role Breakdown (Staff & Platform)

| Role | Defined in DB | Total | Alakapuri | Abc Restro | QA Accounts | Real Accounts |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **SuperAdmin** | No (Config) | 1 | — | — | 0 | 1 |
| **Admin** | Yes (`9fce848e...`) | 4 | 2 | 2 | 2 | 2 |
| **Kitchen** | Yes (`1455e6ca...`) | 2 | 2 | 0 | 1 | 1 |
| **Waiter** | Yes (`bb4720b5...`) | 1 | 1 | 0 | 1 | 0 |
| **Cashier** | No | 0 | 0 | 0 | 0 | 0 |
| **Central Kitchen Mgr** | No | 0 | 0 | 0 | 0 | 0 |
| **Total** | | **8** | **5** | **2** | **4** | **4** |

---

## 2. Platform Operators (Managed Outside PostgreSQL)

Platform super-administrators are not stored in PostgreSQL. They are configured through host environment configuration (`/home/ubuntu/tablo-api.env`) and ASP.NET Core `IConfiguration` (`SuperAdmin:Username` / `SuperAdmin:Password`).

| Username / Identifier | Role | Tenant Scope | Storage Mechanism | Status | QA Purpose |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `sysadmin` | `SuperAdmin` | Platform-wide (Global) | Host Environment Variable (`SuperAdmin__Username`) | Active | Platform Administration |

---

## 3. Restaurant Staff Accounts (`Employees` Table)

Staff accounts are stored in the PostgreSQL `Employees` table with foreign keys referencing `Restaurants` (`RestaurantId`) and `Roles` (`RoleId`).

### Tenant 1: Alakapuri
- **Tenant ID**: `b7226b97-7e7f-489c-ab2e-cc1b0164afc9`
- **Associated Outlets**: `Main Kitchen` (`5cff7daf-fd2c-4f98-80e3-e73e4963620e`)

| Employee ID | Login Identifier (Email) | First Name | Last Name | Role | Outlet Association | Status | Account Type |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| `77777777-1111-0000-0000-000000000001` | `qa-admin@test.tablo.local` | QA | Admin | `Admin` | Tenant-level | Active | **Dedicated QA** |
| `5038a502-ea22-4ea5-9dc3-3a3b0e47b1b3` | `alakapuri@gmail.com` | Shyju | Alakapuri | `Admin` | Tenant-level | Active | Real Staff |
| `77777777-1111-0000-0000-000000000002` | `qa-waiter@test.tablo.local` | QA | Waiter | `Waiter` | Tenant-level | Active | **Dedicated QA** |
| `77777777-1111-0000-0000-000000000003` | `qa-kitchen@test.tablo.local` | QA | Kitchen | `Kitchen` | Tenant-level | Active | **Dedicated QA** |
| `c161c143-e772-4a76-9766-951c4ef3119d` | `neonkj432@gmail.com` | Neon | KJ | `Kitchen` | Tenant-level | Active | Real Staff |

### Tenant 2: Abc Restro
- **Tenant ID**: `ceb29ac5-5a00-416b-a245-361ecc2fb905`
- **Associated Outlets**: *None configured*

| Employee ID | Login Identifier (Email) | First Name | Last Name | Role | Outlet Association | Status | Account Type |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| `77777777-1111-0000-0000-000000000004` | `qa-other@test.tablo.local` | QA | CrossTenant | `Admin` | Tenant-level | Active | **Dedicated QA (Isolation)** |
| `8b416c82-bcbb-4765-8660-be1889f75687` | `abc123@gmail.com` | Abc | Abc | `Admin` | Tenant-level | Active | Real Staff |

---

## 4. Customer Accounts & Identity Management

1. **Authentication Model**:
   - Customers / Diners **do not have user accounts or login credentials** in PostgreSQL.
   - There is no `Users` or `Customers` credentials table.
2. **Order & Session Linkage**:
   - Customer identification occurs per-order via fields on `Orders`: `CustomerName`, `CustomerPhone`, and `TrackingToken`.
   - Dine-in customers access the ordering menu through table session QR tokens (`/t/{token}`), referencing ephemeral `DiningGroups` records.
   - Public storefront customers place orders anonymously or with an entered name/phone number.

---

## 5. Testing Access & Approved Password-Reset Flow

The following 4 accounts are dedicated QA accounts provisioned for testing and automated smoke suites. They support the approved password-reset flow via `POST /api/auth/forgot-password` and `POST /api/auth/reset-password`:

| Login Identifier | Role | Tenant | Primary Testing Purpose |
| :--- | :--- | :--- | :--- |
| `qa-admin@test.tablo.local` | `Admin` | Alakapuri | Admin workspace, menu, billing, inventory, settings, table management |
| `qa-waiter@test.tablo.local` | `Waiter` | Alakapuri | Floor operations, order taking, table service requests |
| `qa-kitchen@test.tablo.local` | `Kitchen` | Alakapuri | Kitchen display system (KDS), KOT batches, ticket firing |
| `qa-other@test.tablo.local` | `Admin` | Abc Restro | Cross-tenant isolation verification and 403 Forbidden checks |

> [!NOTE]
> Since `@test.tablo.local` is a synthetic domain, emails generated by the password-reset flow do not route to the public internet; reset OTPs/hashes are written to `ResetOtpHash` / `ResetTokenHash` on the employee record and logged by the server's email service.

---

## 6. Access & Coverage Gaps

1. **Missing Dedicated QA Roles for Secondary Tenant**:
   - `Abc Restro` (the isolation target) only possesses an `Admin` QA account (`qa-other@test.tablo.local`). There is no `qa-waiter-other@test.tablo.local` or `qa-kitchen-other@test.tablo.local` to verify cross-tenant waiter/kitchen role isolation.
2. **Missing Granular Role Entities in PostgreSQL**:
   - While the application UI supports workspaces for `Cashier`, `StockJournalManager`, and `CentralKitchen`, PostgreSQL only has 3 defined role rows: `Admin`, `Kitchen`, and `Waiter`.
   - Cashier and inventory journal actions currently piggyback on `Admin` or `Waiter` roles rather than using dedicated least-privilege role accounts.
3. **Platform SuperAdmin QA Account**:
   - `sysadmin` is configured via environment variables. There is no isolated sandbox QA SuperAdmin credential for platform-level automation.
