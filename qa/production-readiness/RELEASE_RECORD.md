# TABLO Production Release Record

**Release Identifier:** `v1.41.0-prod`  
**Execution Date & Time:** 2026-11-22 05:45:00 IST / 2026-11-22 00:15:00 UTC  
**Environment:** AWS Lightsail Production (`ap-south-1` Mumbai)  
**AWS Account ID:** `907620281917` (IAM User: `tablo-ops`)  
**Git Branch:** `production` (fast-forward merged from `Rizal-Rstrct-1`)  
**Git Commit Revision:** `41d83e5270a596addbdd459063d1b384b945d64c`  
**Backend Container Image:** `tablo-api:v1.41`  
**Backend Image Digest:** `sha256:d602673c06564f38d9e9c1b85488ea3e2d883227a4f259df0d4c045614edc99f` (Image ID `d602673c0656`)  
**Exact Source Used to Build Backend:** Branch `Rizal-Rstrct-1` staged to `/home/ubuntu/release-v1.41/Tablo.Api` and compiled inside Docker on Ubuntu 24.04 with .NET SDK 8.0.  

---

## 1. Production Topology & Endpoints

| Resource | Target Identifier / URL | Status | Verification Result |
| :--- | :--- | :---: | :--- |
| **API Host (Compute)** | `tablo-prod-api` (`13.126.116.187`, bundle `micro_3_1`) | Active | 2 GB Swap active, Docker engine running |
| **HTTPS Live API** | `https://13-126-116-187.nip.io` | Healthy | `HTTP/1.1 200 OK` (`{"status":"Healthy","service":"Tablo API"}`) |
| **HTTPS Certificate** | `13-126-116-187.nip.io` (Let's Encrypt ECDSA) | Valid | Expiry: 2027-01-05 13:21:47 UTC (44 days valid), verified via normal Windows Schannel TLS |
| **Managed Database** | `tablo-qr-prod-db` (`ls-a291b2e3901caacce744cd0e1523f73f112fbbe8.cd64u6uk49fw.ap-south-1.rds.amazonaws.com:5432`) | Available | All migrations applied up to `20261122040000` |
| **Frontend (Amplify)** | `https://production.d25iywxxvasd5l.amplifyapp.com` | Active | Job ID `9` (`41d83e5270a596addbdd459063d1b384b945d64c`). Deployed & verified |

---

## 2. Pre-Deployment Snapshots

Prior to applying any database migrations or rotating application containers, full snapshots were captured and verified:

- **Relational Database Snapshot:**  
  `tablo-db-snap-20261122-035631`  
  - Database: `tablo-qr-prod-db` (`dbtabloqr`)  
  - Status: `available`  
  - Region: `ap-south-1`  

- **Compute Instance Snapshot:**  
  `tablo-api-snap-20261122-035631`  
  - Instance: `tablo-prod-api` (`13.126.116.187`)  
  - Status: `available`  
  - Region: `ap-south-1`  

---

## 3. Database Migration Execution

All Entity Framework Core migrations from initial state through `20261122040000_AddOutletStorefrontSettingsAndTrackingToken` were executed sequentially on the production RDS:

1. `20261030102832_AddStaffOrderingAndBilling`
2. `20261116092356_AddDiningGroupsAndPaymentLedgerFoundation`
3. `20261116094941_AddFloorOperationsAndGroupOwnership`
4. `20261116133614_EnforceServiceRequestGroupTenant`
5. `20261117165609_AddInventoryModule`
6. `20261118063135_AddInventoryCostingAndProfitReporting`
7. `20261118130218_AddPurchasingLotsTransfersAndReservations`
8. `20261118142019_AddSubscriptionsPlansAndAuditLogs`
9. `20261119170000_AddThermalPrintingAndParcelCounter`
10. `20261119200000_AddKitchenProductionControls`
11. `20261119233000_AddScheduledHybridCarSideAndUpi`
12. `20261121150000_AddMarketplaceDeliveryAndPayouts`
13. `20261122040000_AddOutletStorefrontSettingsAndTrackingToken`

### Verified Schema Columns on Production:
- `Outlets.IsPublished` (`boolean`)
- `Outlets.IsOpen` (`boolean`)
- `Outlets.LeadTimeMinutes` (`integer`)
- `Outlets.MinOrderAmount` (`numeric`)
- `Outlets.DeliveryBaseFee` (`numeric`)
- `Orders.TrackingToken` (`character varying(64)`, index `IX_Orders_TrackingToken`)
- `ModifierOptions.RestaurantId` (`uuid`)

---

## 4. Frontend & Backend Deployment Details

### Backend Container Configuration
- **Image Digest:** `sha256:d602673c06564f38d9e9c1b85488ea3e2d883227a4f259df0d4c045614edc99f`
- **Host Binding:** `127.0.0.1:8080->8080/tcp` (Reverse-proxied via Nginx over port 443)
- **Environment:** Production (`ASPNETCORE_ENVIRONMENT=Production`), configured with `/home/ubuntu/tablo-api.env`
- **Build Method:** `docker build -t tablo-api:v1.41 /home/ubuntu/release-v1.41/Tablo.Api` with 2 GB host swapfile

### Amplify Frontend Deployment
- **Amplify App:** `tablo-production` (`d25iywxxvasd5l`)
- **Active Deployment:** Job ID `9`
- **Commit:** `41d83e5270a596addbdd459063d1b384b945d64c`
- **Branch:** `production`
- **Build Phase:** `SUCCEED` (vite v7.3.6, 2664 modules transformed)
- **Deploy Phase:** `SUCCEED`
- **Verify Phase:** `SUCCEED`
- **Configured Environment Variable:** `"VITE_API_URL": "https://13-126-116-187.nip.io/api"`

---

## 5. End-to-End Live Verification Results

Testing was conducted using isolated test records (`*@test.tablo.local`) against live production:
- Tenant 1: **Alakapuri** (`b7226b97-7e7f-489c-ab2e-cc1b0164afc9`)
- Tenant 2: **Abc Restro** (`ceb29ac5-5a00-416b-a245-361ecc2fb905`)

| Verification Check | Method | Expected Result | Actual Result | Status | Screenshot Reference |
| :--- | :--- | :--- | :--- | :---: | :--- |
| **HTTPS Certificate** | Windows `curl.exe -v` (no `-k`) | TLS handshake succeeds | `HTTP/1.1 200 OK`, Let's Encrypt | **PASS** | Console logs |
| **Admin UI Authentication** | Browser Puppeteer | Login, store JWT, SignalR connect | `https://.../dashboard`, WebSocket connected | **PASS** | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Menu & Table Admin** | Browser Puppeteer | Load dishes, active tables | Verified dish prices, QR code status | **PASS** | [PROD-012-admin-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-012-admin-view.png) |
| **Staff Roster** | Browser Puppeteer | List active employees and roles | 5 employees listed (Admin, Waiter, Kitchen) | **PASS** | [PROD-013-staff-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-013-staff-view.png) |
| **Kitchen Queue / Orders** | Browser Puppeteer | Live service view & pending tickets | ORD-0006 for Table 3, Table 2 bill request | **PASS** | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Analytics & Reports** | Browser Puppeteer | Render sales charts and order table | ₹2,645.00 revenue, 5 orders, top items | **PASS** | [PROD-015-reports-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-015-reports-view.png) |
| **Kitchen Role Restriction** | Browser & API | Restrict Admin/Reports views & APIs | Navigation restricted; API returns 403 Forbidden | **PASS** | [PROD-027-role-restriction.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-027-role-restriction.png) |
| **Cross-Tenant Rejection** | Production API | Reject cross-restaurant data access | Tenant 2 Admin calling Tenant 1 tables -> 403 | **PASS** | Test script log |
| **Customer QR Menu (Table 3)** | Browser Puppeteer | Resolve QR token `2x02rywxc` | Alakapuri Table 3 menu loads, categories visible | **PASS** | [PROD-017-customer-menu-table3.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-017-customer-menu-table3.png) |
| **Customer Live Order Tracking** | Browser Puppeteer | Track active order ORD-0006 | Progress stepper, item breakdown, ₹399 total | **PASS** | [PROD-018-customer-bill-or-tracking.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-018-customer-bill-or-tracking.png) |
| **Customer Bill Service Alert** | Browser Puppeteer | Resolve QR token `qd4x8f2tp` | Table 2 bell alert active, bill requested badge | **PASS** | [PROD-019-table2-bill-request.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-019-table2-bill-request.png) |
| **Staff Order Taking Workspace** | Browser Puppeteer | Navigate to `/dashboard?tab=order` | Tableless parcel counter & dine-in ordering service desk | **PASS** | [PROD-021-staff-order-workspace.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-021-staff-order-workspace.png) |
| **Parcel Marketplace Entry** | Browser Puppeteer | Navigate to `/admin?tab=marketplace` | Swiggy/Zomato inbound order ingestion, catalog mapping | **PASS** | [PROD-022-marketplace-entry.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-022-marketplace-entry.png) |
| **Outlet Storefront Settings** | Browser Puppeteer | Navigate to `/admin?tab=storefront` | Operating hours, lead time, delivery fees, emergency toggle | **PASS** | [PROD-023-outlet-storefront-settings.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-023-outlet-storefront-settings.png) |
| **Central Kitchen Manager** | Browser Puppeteer | Navigate to `/inventory?tab=central-kitchen` | Batch production, inter-outlet transfers, multi-tenant gating | **PASS** | [PROD-024-central-kitchen.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-024-central-kitchen.png) |
| **Public Storefront (Guest)** | Browser Puppeteer | Navigate to `/order` without login | Public direct ordering menu with pickup/delivery controls | **PASS** | [PROD-025-public-storefront.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-025-public-storefront.png) |
| **Customer Unpaid Bill QR** | Browser Puppeteer | Navigate to `/t/qd4x8f2tp` | Bill preview with dynamic UPI QR code rendered | **PASS** | [PROD-026-customer-unpaid-bill-qr.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-026-customer-unpaid-bill-qr.png) |

---

## 6. Reconciled Rollback & Recovery Plan

### 1. Backend Container Rollback
- **Previous Image Tag:** `tablo-api:latest`
- **Previous Image ID:** `4c52bfd617aa`
- **Previous Digest:** `sha256:4c52bfd617aac075769b3933adc5032a1686b3079a57e637f32ec31aab8ae498`
- **Execution:**
  ```bash
  ssh -i ~/.ssh/lightsail_key.pem ubuntu@13.126.116.187
  sudo docker stop tablo-api
  sudo docker rm tablo-api
  sudo docker run -d --name tablo-api \
    -p 127.0.0.1:8080:8080 \
    --env-file /home/ubuntu/tablo-api.env \
    --restart unless-stopped \
    sha256:4c52bfd617aac075769b3933adc5032a1686b3079a57e637f32ec31aab8ae498
  ```

### 2. Database Snapshot Restoration
- **Target Snapshot:** `tablo-db-snap-20261122-035631` (AWS Lightsail Relational Database)
- **Restore Command:**
  ```bash
  aws lightsail create-relational-database-from-snapshot \
    --region ap-south-1 \
    --relational-database-name tablo-qr-prod-db-rollback \
    --relational-database-snapshot-name tablo-db-snap-20261122-035631
  ```
- **Exact Measured Recovery Time:** **19 minutes 56 seconds** (tested via `tablo-db-recovery-test` on 2026-11-22 from snapshot creation to `available`).
- **Post-Snapshot Writes Strategy:**
  - Because `pg_dump` does not support `--where`, transactional delta records created after snapshot time (`2026-11-21 22:26:32 UTC`) are extracted via `psql \copy`:
    ```sql
    \copy (SELECT * FROM "Orders" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_orders.csv' WITH CSV HEADER;
    \copy (SELECT * FROM "OrderItems" WHERE "OrderId" IN (SELECT "Id" FROM "Orders" WHERE "CreatedAt" > '2026-11-21 22:26:32Z')) TO 'delta_order_items.csv' WITH CSV HEADER;
    \copy (SELECT * FROM "TableBills" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_table_bills.csv' WITH CSV HEADER;
    \copy (SELECT * FROM "BillPayments" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_bill_payments.csv' WITH CSV HEADER;
    \copy (SELECT * FROM "LoyaltyLedgerEntries" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_loyalty.csv' WITH CSV HEADER;
    ```
  - Re-imported into the restored instance preserving foreign key dependency order:
    ```sql
    \copy "Orders" FROM 'delta_orders.csv' WITH CSV HEADER;
    \copy "OrderItems" FROM 'delta_order_items.csv' WITH CSV HEADER;
    \copy "TableBills" FROM 'delta_table_bills.csv' WITH CSV HEADER;
    \copy "BillPayments" FROM 'delta_bill_payments.csv' WITH CSV HEADER;
    \copy "LoyaltyLedgerEntries" FROM 'delta_loyalty.csv' WITH CSV HEADER;
    ```
  - Replay into the restored instance before updating `/home/ubuntu/tablo-api.env` to point to the new database endpoint.
