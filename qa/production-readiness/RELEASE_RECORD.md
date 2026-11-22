# TABLO Production Release Record

**Release Identifier:** `v1.41.0-prod`  
**Execution Date & Time:** 2026-11-22 05:06:00 IST / 2026-11-21 23:36:00 UTC  
**Environment:** AWS Lightsail Production (`ap-south-1` Mumbai)  
**AWS Account ID:** `907620281917` (IAM User: `tablo-ops`)  
**Git Branch:** `Rizal-Rstrct-1`  
**Git Commit Revision:** `b888348816b4d9ebfabf5b88363f1e0d4a1efaf4`  
**Backend Container Image:** `tablo-api:v1.41`  
**Backend Image Digest:** `sha256:d602673c06564f38d9e9c1b85488ea3e2d883227a4f259df0d4c045614edc99f` (Image ID `d602673c0656`)  
**Exact Source Used to Build Backend:** Branch `Rizal-Rstrct-1` (HEAD commit `b888348816b4d9ebfabf5b88363f1e0d4a1efaf4`) staged to `/home/ubuntu/release-v1.41/Tablo.Api` and compiled inside Docker on Ubuntu 24.04 with .NET SDK 8.0.  

---

## 1. Production Topology & Endpoints

| Resource | Target Identifier / URL | Status | Verification Result |
| :--- | :--- | :---: | :--- |
| **API Host (Compute)** | `tablo-prod-api` (`13.126.116.187`, bundle `micro_3_1`) | Active | 2 GB Swap active, Docker engine running |
| **HTTPS Live API** | `https://13-126-116-187.nip.io` | Healthy | `HTTP/1.1 200 OK` (`{"status":"Healthy","service":"Tablo API"}`) |
| **HTTPS Certificate** | `13-126-116-187.nip.io` (Let's Encrypt ECDSA) | Valid | Expiry: 2027-01-05 13:21:47 UTC (44 days valid), verified via normal Windows Schannel TLS |
| **Managed Database** | `tablo-qr-prod-db` (`ls-a291...rds.amazonaws.com:5432`) | Available | All migrations applied up to `20261122040000` |
| **Frontend (Amplify)** | `https://production.d25iywxxvasd5l.amplifyapp.com` | Active | Active Job ID `7` (`bbce4910e9dfdfb3240e900358e3a5ebec5062f4`). `VITE_API_URL` verified |

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
```sql
SELECT column_name, data_type, column_default 
FROM information_schema.columns 
WHERE table_name = 'Outlets' 
  AND column_name IN ('IsPublished', 'IsOpen', 'LeadTimeMinutes', 'MinOrderAmount', 'DeliveryBaseFee');
```
Output:
- `IsPublished` | `boolean` | `true`
- `IsOpen` | `boolean` | `true`
- `LeadTimeMinutes` | `integer` | `25`
- `MinOrderAmount` | `numeric` | `200.00`
- `DeliveryBaseFee` | `numeric` | `40.00`
- `Orders.TrackingToken` | `character varying(64)` | Index `IX_Orders_TrackingToken` verified
- `ModifierOptions.RestaurantId` | `uuid` | Populated and isolated

---

## 4. Frontend & Backend Deployment Details

### Backend Container Configuration
- **Image Digest:** `sha256:d602673c06564f38d9e9c1b85488ea3e2d883227a4f259df0d4c045614edc99f`
- **Host Binding:** `127.0.0.1:8080->8080/tcp` (Strictly internal; traffic reverse-proxied via Nginx over port 443)
- **Environment:** Production (`ASPNETCORE_ENVIRONMENT=Production`), configured with `/home/ubuntu/tablo-api.env`
- **Build Method:** `docker build -t tablo-api:v1.41 /home/ubuntu/release-v1.41/Tablo.Api` with 2 GB host swapfile

### Amplify Frontend Status & Finding
- **Amplify App:** `tablo-production` (`d25iywxxvasd5l`)
- **Active Deployment:** Job ID `7` (Commit `bbce4910e9dfdfb3240e900358e3a5ebec5062f4`, deployed 2026-10-26)
- **Configured Environment Variable:** `"VITE_API_URL": "https://13-126-116-187.nip.io/api"`
- **Finding:** Amplify is configured with GitHub repository continuous deployment. Manual zip upload via AWS CLI (`create-deployment`) returns `BadRequestException: Operation not supported. App is already connected a repository`. Deploying the newly built frontend (`tablo/dist/`) to Amplify requires merging `Rizal-Rstrct-1` to the repository's `production` branch on GitHub.

---

## 5. End-to-End Live Verification Results

Testing was conducted using isolated test records (`*@test.tablo.local`) against live production:
- Tenant 1: **Alakapuri** (`b7226b97-7e7f-489c-ab2e-cc1b0164afc9`)
- Tenant 2: **Abc Restro** (`ceb29ac5-5a00-416b-a245-361ecc2fb905`)

| Verification Check | Method | Expected Result | Actual Result | Status | Screenshot Reference |
| :--- | :--- | :--- | :--- | :---: | :--- |
| **HTTPS Certificate** | Windows `curl.exe -v` (no `-k`) | TLS handshake succeeds | `HTTP/1.1 200 OK`, Let's Encrypt | **PASS** | Console logs |
| **Admin UI Authentication** | Browser Puppeteer | Login, store JWT, SignalR connect | `https://.../dashboard`, WebSocket connected | **PASS** | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Menu & Table Admin** | Browser Puppeteer | Load 8 dishes, 3 active tables | Verified dish prices, QR code status | **PASS** | [PROD-012-admin-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-012-admin-view.png) |
| **Staff Roster** | Browser Puppeteer | List active employees and roles | 5 employees listed (Admin, Waiter, Kitchen) | **PASS** | [PROD-013-staff-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-013-staff-view.png) |
| **Kitchen Queue / Orders** | Browser Puppeteer | Live service view & pending tickets | ORD-0006 for Table 3, Table 2 bill request | **PASS** | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Analytics & Reports** | Browser Puppeteer | Render sales charts and order table | ₹2,645.00 revenue, 5 orders, top items | **PASS** | [PROD-015-reports-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-015-reports-view.png) |
| **Kitchen Role Restriction** | Browser & API | Restrict Admin/Reports views & APIs | Navigation restricted; API returns 403 Forbidden | **PASS** | [PROD-016-kitchen-role-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-016-kitchen-role-login.png) |
| **Cross-Tenant Rejection** | Production API | Reject cross-restaurant data access | Tenant 2 Admin calling Tenant 1 tables -> 403 | **PASS** | Test script log |
| **Customer QR Menu (Table 3)** | Browser Puppeteer | Resolve QR token `2x02rywxc` | Alakapuri Table 3 menu loads, categories visible | **PASS** | [PROD-017-customer-menu-table3.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-017-customer-menu-table3.png) |
| **Customer Live Order Tracking** | Browser Puppeteer | Track active order ORD-0006 | Progress stepper, item breakdown, ₹399 total | **PASS** | [PROD-018-customer-bill-or-tracking.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-018-customer-bill-or-tracking.png) |
| **Customer Bill Service Alert** | Browser Puppeteer | Resolve QR token `qd4x8f2tp` | Table 2 bell alert active, bill requested badge | **PASS** | [PROD-019-table2-bill-request.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-019-table2-bill-request.png) |

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
- **Estimated Recovery Time:** ~10 to 15 minutes.
- **Post-Snapshot Writes Strategy:**
  - Dump new records created after snapshot time (`2026-11-21 22:26:32 UTC`):
    ```bash
    pg_dump -h $CURRENT_HOST -U tabloqr_admin -d dbtabloqr --data-only \
      -t '"Orders"' -t '"OrderItems"' -t '"BillPayments"' -t '"TableBills"' \
      --where "CreatedAt > '2026-11-21 22:26:32Z'" > delta_post_snapshot.sql
    ```
  - Replay `delta_post_snapshot.sql` into the restored instance before updating `/home/ubuntu/tablo-api.env` to point to the new database endpoint.
