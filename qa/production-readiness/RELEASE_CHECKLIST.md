# TABLO Production Release Readiness Checklist

**Release Scope:** TABLO POS & Management System v1.41.0 Production Deployment  
**Repository Branch:** `production` (fast-forward merged from `Rizal-Rstrct-1`)  
**Git Revision:** `41d83e5270a596addbdd459063d1b384b945d64c`  
**Target Environment:** AWS Lightsail (`ap-south-1` Mumbai)  
**Deployment Target Instance:** `tablo-prod-api` (`13.126.116.187`, bundle `micro_3_1`)  
**Production Database:** `tablo-qr-prod-db` (PostgreSQL 16 on AWS Lightsail Managed Relational Database)  
**Production HTTPS API Endpoint:** `https://13-126-116-187.nip.io` (Valid Let's Encrypt TLS certificate)  
**Production Frontend (Amplify):** `https://production.d25iywxxvasd5l.amplifyapp.com`  
**Amplify App ID:** `d25iywxxvasd5l` (Branch: `production`, Job ID: `9`, Commit: `41d83e5270a596addbdd459063d1b384b945d64c`, Status: `SUCCEED`)  

---

## 1. Verified Release Scope & Modules

| Module / Check | Execution Mode | Verification Result | Notes | Screenshot Reference |
| :--- | :---: | :---: | :--- | :--- |
| **HTTPS Certificate Validation** | Direct `curl` (Schannel) | **PASS** | Valid Let's Encrypt TLS certificate expiring 2027-01-05 (no `-k` needed) | Console logs |
| **Backend Service Health** | HTTPS GET `/health` | **PASS** | `200 OK` (`{"status":"Healthy","service":"Tablo API"}`) | Console logs |
| **Database Migrations** | Remote RDS Execution | **PASS** | Sequential execution up to `20261122040000`, 0 data loss | Console logs |
| **Admin & Staff Authentication** | UI & API E2E | **PASS** | JWT generated, SignalR WebSocket established, state persisted | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Staff & Menu Management** | Live UI (`/admin`, `/staff`) | **PASS** | Verified dishes, categories, and employees | [PROD-012-admin-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-012-admin-view.png) |
| **Live Kitchen Queue & Display** | Live UI (`/dashboard`) | **PASS** | Live service view & pending tickets | [PROD-011-post-login.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-011-post-login.png) |
| **Reports & Order Analytics** | Live UI & API (`/reports`) | **PASS** | Sales charts, item rankings, totals | [PROD-015-reports-view.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-015-reports-view.png) |
| **Customer Dine-In Menu & Order** | Live UI (`/t/:qrToken`) | **PASS** | Table 3 menu loads, categories visible | [PROD-017-customer-menu-table3.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-017-customer-menu-table3.png) |
| **Role-Based Access Control** | Live UI & API | **PASS** | Kitchen role restricted from Admin/Reports in UI & API (403 Forbidden) | [PROD-027-role-restriction.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-027-role-restriction.png) |
| **Cross-Tenant Isolation** | Production API | **PASS** | Tenant 2 Admin accessing Tenant 1 tables rejected (403 Forbidden) | Test script log |
| **Amplify Frontend Deployment** | AWS Amplify CI/CD | **PASS** | Job ID 9 succeeded (Build, Deploy, Verify) on branch `production` | AWS Amplify Console |
| **Staff Order Taking Workspace** | Live UI (`/dashboard?tab=order`) | **PASS** | Tableless parcel & dine-in ordering service desk | [PROD-021-staff-order-workspace.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-021-staff-order-workspace.png) |
| **Parcel Marketplace Entry** | Live UI (`/admin?tab=marketplace`) | **PASS** | Swiggy/Zomato inbound order ingestion, catalog mapping | [PROD-022-marketplace-entry.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-022-marketplace-entry.png) |
| **Outlet Storefront Settings** | Live UI (`/admin?tab=storefront`) | **PASS** | Operating hours, lead time, delivery fees, emergency toggle | [PROD-023-outlet-storefront-settings.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-023-outlet-storefront-settings.png) |
| **Central Kitchen Manager** | Live UI (`/inventory?tab=central-kitchen`) | **PASS** | Batch production, inter-outlet transfers, multi-tenant gating | [PROD-024-central-kitchen.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-024-central-kitchen.png) |
| **Public Storefront (Guest)** | Live UI (`/order`) | **PASS** | Accessible without authentication for pickup/delivery menu | [PROD-025-public-storefront.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-025-public-storefront.png) |
| **Customer Unpaid Bill QR** | Live UI (`/t/:qrToken`) | **PASS** | Bill preview with dynamic UPI QR code rendered | [PROD-026-customer-unpaid-bill-qr.png](file:///D:/Projects/tablo/qa/production-readiness/screenshots/PROD-026-customer-unpaid-bill-qr.png) |

---

## 2. Infrastructure & Safety Pre-Deployment Gates

1. **Native Backups & Snapshots:**
   - **Database Snapshot:** `tablo-db-snap-20261122-035631` (State: `available`, created at 2026-11-22 03:56:32 IST)
   - **Instance Snapshot:** `tablo-api-snap-20261122-035631` (State: `available`, created at 2026-11-22 03:56:33 IST)
2. **Database Migration Verification:**
   - Migration file: `Tablo.Api/Migrations/20261122040000_AddOutletStorefrontSettingsAndTrackingToken.cs`
   - SQL Script: `scripts/apply-storefront-outlet-settings-migration.sql`
   - Verified columns: `IsPublished`, `IsOpen`, `Timezone`, `OpeningTime`, `ClosingTime`, `IsOvernight`, `LeadTimeMinutes`, `MinOrderAmount`, `DeliveryBaseFee`, `FreeDeliveryAboveAmount`, `Phone`, `IsTemporarilyClosed`, `ClosureReason` on `Outlets`; `TrackingToken` on `Orders`; `RestaurantId` on `ModifierOptions`.
3. **Frontend Production Build:**
   - Verified local Vite production build (`npm --prefix tablo run build`): Completed in 18.23s with 0 errors.

---

## 3. Verified Reconciled Rollback Procedures

### Container Rollback
If the new container version `tablo-api:v1.41` experiences runtime regressions:
1. Identify verified previous stable image:
   - **Image Tag:** `tablo-api:latest`
   - **Image ID:** `4c52bfd617aa`
   - **Digest:** `sha256:4c52bfd617aac075769b3933adc5032a1686b3079a57e637f32ec31aab8ae498`
2. On the Lightsail instance `13.126.116.187`:
   ```bash
   sudo docker stop tablo-api
   sudo docker rm tablo-api
   sudo docker run -d --name tablo-api \
     -p 127.0.0.1:8080:8080 \
     --env-file /home/ubuntu/tablo-api.env \
     --restart unless-stopped \
     sha256:4c52bfd617aac075769b3933adc5032a1686b3079a57e637f32ec31aab8ae498
   ```
3. Verify status with `sudo docker ps` and `curl -s http://127.0.0.1:8080/health`.

### Managed Database Rollback & Write Handling
1. **AWS Service:** AWS Lightsail Relational Database (`relational-database`).
2. **Restore Command:**
   ```bash
   aws lightsail create-relational-database-from-snapshot \
     --region ap-south-1 \
     --relational-database-name tablo-qr-prod-db-rollback \
     --relational-database-snapshot-name tablo-db-snap-20261122-035631
   ```
3. **Exact Measured Recovery Time:** **19 minutes 56 seconds** (Verified via test instance creation `tablo-db-recovery-test` on 2026-11-22: initiated at `05:12:34 IST`, available at `05:32:30 IST`).
4. **Handling Post-Snapshot Writes:**
   - Because `pg_dump` does not support a `--where` argument, selective extraction of transactional records written after snapshot time (`2026-11-21 22:26:32 UTC`) is executed via `psql \copy`:
     ```sql
     -- 1. Export delta records from the active database
     \copy (SELECT * FROM "Orders" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_orders.csv' WITH CSV HEADER;
     \copy (SELECT * FROM "OrderItems" WHERE "OrderId" IN (SELECT "Id" FROM "Orders" WHERE "CreatedAt" > '2026-11-21 22:26:32Z')) TO 'delta_order_items.csv' WITH CSV HEADER;
     \copy (SELECT * FROM "TableBills" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_table_bills.csv' WITH CSV HEADER;
     \copy (SELECT * FROM "BillPayments" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_bill_payments.csv' WITH CSV HEADER;
     \copy (SELECT * FROM "LoyaltyLedgerEntries" WHERE "CreatedAt" > '2026-11-21 22:26:32Z') TO 'delta_loyalty.csv' WITH CSV HEADER;
     ```
   - Ingest into the restored database instance in foreign key order:
     ```sql
     -- 2. Ingest delta records into the restored database
     \copy "Orders" FROM 'delta_orders.csv' WITH CSV HEADER;
     \copy "OrderItems" FROM 'delta_order_items.csv' WITH CSV HEADER;
     \copy "TableBills" FROM 'delta_table_bills.csv' WITH CSV HEADER;
     \copy "BillPayments" FROM 'delta_bill_payments.csv' WITH CSV HEADER;
     \copy "LoyaltyLedgerEntries" FROM 'delta_loyalty.csv' WITH CSV HEADER;
     ```
   - Replay delta records into the restored instance before updating `/home/ubuntu/tablo-api.env` to point to the restored database endpoint.
