# TABLO Production Release Readiness Checklist

**Release Scope:** TABLO POS & Management System v1.41.0 Production Deployment  
**Repository Branch:** `Rizal-Rstrct-1`  
**Git Revision:** `b888348816b4d9ebfabf5b88363f1e0d4a1efaf4`  
**Target Environment:** AWS Lightsail (`ap-south-1` Mumbai)  
**Deployment Target Instance:** `tablo-prod-api` (`13.126.116.187`, bundle `micro_3_1`)  
**Production Database:** `tablo-qr-prod-db` (PostgreSQL 16 on AWS Lightsail Managed Relational Database)  
**Production HTTPS API Endpoint:** `https://13-126-116-187.nip.io` (Valid Let's Encrypt TLS certificate)  
**Production Frontend (Amplify):** `https://production.d25iywxxvasd5l.amplifyapp.com`  
**Amplify App ID:** `d25iywxxvasd5l` (Branch: `production`, Active Job ID: `7`, Commit: `bbce4910e9dfdfb3240e900358e3a5ebec5062f4`)  

---

## 1. Verified Release Scope & Modules

| Module / Check | Execution Mode | Verification Result | Notes |
| :--- | :---: | :---: | :--- |
| **HTTPS Certificate Validation** | Direct `curl` (Schannel) | **PASS** | Valid Let's Encrypt TLS certificate expiring 2027-01-05 (no `-k` needed) |
| **Backend Service Health** | HTTPS GET `/health` | **PASS** | `200 OK` (`{"status":"Healthy","service":"Tablo API"}`) |
| **Database Migrations** | Remote RDS Execution | **PASS** | Sequential execution up to `20261122040000`, 0 data loss |
| **Admin & Staff Authentication** | UI & API E2E | **PASS** | JWT generated, SignalR WebSocket established, state persisted |
| **Staff & Menu Management** | Live UI (`/admin`, `/staff`) | **PASS** | Verified in `PROD-012-admin-view.png` and `PROD-013-staff-view.png` |
| **Live Kitchen Queue & Display** | Live UI (`/dashboard`) | **PASS** | Verified in `PROD-011-post-login.png` and `PROD-016-kitchen-role-login.png` |
| **Reports & Order Analytics** | Live UI & API (`/reports`) | **PASS** | Verified in `PROD-015-reports-view.png` (Sales charts, item rankings, totals) |
| **Customer Dine-In Menu & Order** | Live UI (`/t/:qrToken`) | **PASS** | Verified in `PROD-017-customer-menu-table3.png` & `PROD-018-customer-bill-or-tracking.png` |
| **Role-Based Access Control** | Live UI & API | **PASS** | Kitchen role restricted from Admin/Reports in UI & API (403 Forbidden) |
| **Cross-Tenant Isolation** | Production API | **PASS** | Tenant 2 Admin accessing Tenant 1 tables rejected (403 Forbidden) |
| **Amplify Frontend Build** | AWS Amplify Console | **PARTIAL / DISCOVERY** | Active Job ID 7 (2026-10-26). v1.41 dist built; repo merge required for Amplify sync |

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
   - Verified local Vite production build (`npm --prefix tablo run build`): Completed in 5.37s with 0 errors.

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
3. **Estimated Recovery Time:** ~10 to 15 minutes for Lightsail RDS instance provisioning and storage initialization.
4. **Handling Post-Snapshot Writes:**
   - Because the snapshot was captured at `2026-11-22 03:56:32 IST`, any orders, payments, or ledger records written after that time are not present in the snapshot.
   - Before repointing the application container to the restored database, dump transactional delta rows:
     ```bash
     PGPASSWORD=*** pg_dump -h $OLD_HOST -U tabloqr_admin -d dbtabloqr \
       --data-only \
       -t '"Orders"' -t '"OrderItems"' -t '"BillPayments"' -t '"TableBills"' -t '"LoyaltyLedgerEntries"' \
       --where "CreatedAt > '2026-11-21 22:26:32Z'" > delta_post_snapshot.sql
     ```
   - Replay `delta_post_snapshot.sql` into the restored instance before resuming public traffic.
