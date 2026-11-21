# TABLO Full End-to-End QA Test Package

This directory contains the complete end-to-end quality assurance deliverables, automated test results, page coverage index, application flow guides, defect log, and full-resolution browser screenshot captures for **TABLO** in `D:\Projects\tablo`.

---

## 1. Executive Summary & Environment

- **Target Workspace**: `D:\Projects\tablo`
- **Frontend URL**: [http://127.0.0.1:5173](http://127.0.0.1:5173) (Vite Dev Server, React 19, TanStack Router & Query, Tailwind CSS)
- **Backend API URL**: [http://127.0.0.1:5166](http://127.0.0.1:5166) (.NET 8 Web API, SignalR, EF Core)
- **Database**: Local Disposable PostgreSQL Cluster at port `55439` (Cluster directory: `C:\Users\neonk\AppData\Local\Temp\tablo-demo-pg-ecada15c5b5c452f8c633bcfaaeeff27`, Database: `tablo_demo`)
- **Thermal Printer Simulators**:
  - Food Kitchen Station: `127.0.0.1:9100` (Raw TCP ESC/POS receiver)
  - Beverage Station: `127.0.0.1:9101` (Raw TCP ESC/POS receiver)
- **Primary Tenant**: **Tablo Heritage Restaurant** (`22222222-0000-0000-0000-000000000001`) — **Premium Subscription Tier**
- **Secondary Tenant (Isolation)**: **Tablo Coastal Cafe** (`22222222-0000-0000-0000-000000000009`) — **Basic Subscription Tier**

---

## 2. Test Execution Summary

| Test Domain | Executed | Passed | Failed | Blocked | Details |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Node.js Automated Test Suite** | 97 | 97 | 0 | 0 | Ran via `npm --prefix tablo test`. Full pass across floor, parcel, receipt, production, and thermal printing. |
| **TypeScript Compilation & Build** | 2 | 2 | 0 | 0 | `npx tsc -b` (0 errors), `npm --prefix tablo run build` (vite v7.3.6 production bundle in 5.12s). |
| **Browser E2E Discovered Pages & Tabs** | 34 | 34 | 0 | 0 | Full-resolution screenshot captures for every reachable URL, tab, and role session. |
| **Workflow QA Test Cases** | 42 | 41 | 0 | 1 | 41 Passed, 1 Blocked by host OS WDAC policy (`Tablo.Api.Tests.dll` in-process load blocked). |
| **Total Test Checks** | **175** | **174** | **0** | **1** | **99.4% Pass Rate** |

---

## 3. Discovered Roles & Demo Credentials

| Role | Email / Username | Password | Assigned Scope & Tenant |
| :--- | :--- | :--- | :--- |
| **Platform Super Admin** | `superadmin@example.test` | `SuperAdminPassword123!` | Global platform governance, plans, subscription overrides, audit logs. |
| **Restaurant Admin** | `admin@tablodemo.com` | `password123` | Tablo Heritage Restaurant. Full menu, inventory, floor, staff, and report administration. |
| **Waiter** | `waiter@tablodemo.com` | `password123` | Tablo Heritage Restaurant. Direct table ordering, dining groups, bill splits, settlements. |
| **Kitchen Chef** | `kitchen@tablodemo.com` | `password123` | Tablo Heritage Restaurant. Station queue, status advance, KOT reprints, hold/fire courses. |
| **Cashier** | `cashier@tablodemo.com` | `password123` | Tablo Heritage Restaurant. Bill review, payment recording (Cash, UPI, Card), receipt printing. |
| **Secondary Tenant Admin** | `coastal.admin@tablodemo.com` | `password123` | Tablo Coastal Cafe. Restricted to Basic tier capabilities. |

---

## 4. Deliverables Directory Index

1. [TEST-CASES.md](file:///D:/Projects/tablo/qa/tablo-e2e/TEST-CASES.md) — Comprehensive executable test catalog with step-by-step reproduction and evidence links.
2. [PAGE-COVERAGE.md](file:///D:/Projects/tablo/qa/tablo-e2e/PAGE-COVERAGE.md) — Discovered page/tab coverage inventory with role requirements and screenshot links.
3. [APPLICATION-FLOWS.md](file:///D:/Projects/tablo/qa/tablo-e2e/APPLICATION-FLOWS.md) — Step-by-step role-by-role walkthrough guide for users and operators.
4. [DEFECTS.md](file:///D:/Projects/tablo/qa/tablo-e2e/DEFECTS.md) — Prioritized defect findings, root causes, fixes applied, and environmental constraints.
5. [screenshots/](file:///D:/Projects/tablo/qa/tablo-e2e/screenshots/) — 34 browser screenshots capturing every page and outcome state.

---

## 5. How to Rerun the Full QA Verification

### Step 1: Ensure Background Daemons are Active
Verify the PostgreSQL cluster (`55439`), API (`5166`), Frontend (`5173`), and printer simulators (`9100`, `9101`) are running:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:5166/health"
Test-NetConnection -ComputerName 127.0.0.1 -Port 5173
```

### Step 2: Run Unit, Integration, and Build Checks
```powershell
# 1. Type-check frontend
cd D:\Projects\tablo\tablo
npx tsc -b

# 2. Build production bundle
npm run build

# 3. Run Node.js 97-test suite
npm test
```

### Step 3: Run Automated Browser E2E Pass and Screenshot Captures
```powershell
node scripts/run-qa-suite.mjs
node scripts/snap-interactions.mjs
```
All screenshots will be written into `qa/tablo-e2e/screenshots/`.
