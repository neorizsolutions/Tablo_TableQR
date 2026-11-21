# TABLO local restaurant demo

The runner creates a fresh database per invocation on PostgreSQL at `127.0.0.1:55439`. It checks the exact PostgreSQL data directory before creating the database, applies EF migrations, seeds fictitious reference data, and uses TABLO services for transactions. It writes the database name, QR links, bill IDs and reconciliation evidence to `demo.json` after the scenario checks pass.

## Start the demo

Run from the repository root in PowerShell. Use a new cluster directory and an unused port 55439. If you already initialized a dedicated demo cluster on that port, use its directory as `$cluster` and start at the password step.

```powershell
$pg = 'C:/Program Files/PostgreSQL/17/bin'
$cluster = Join-Path $env:TEMP ('tablo-demo-pg-' + [guid]::NewGuid().ToString('N'))
$artifacts = Join-Path $env:TEMP ('tablo-demo-output-' + [guid]::NewGuid().ToString('N'))
& "$pg/initdb.exe" -D $cluster -U tablo_test -A trust --no-locale -E UTF8
if ($LASTEXITCODE) { throw 'Cluster initialization failed' }
& "$pg/pg_ctl.exe" -D $cluster -l "$cluster/server.log" -o '-h 127.0.0.1 -p 55439' -w start
if ($LASTEXITCODE) { throw 'Cluster startup failed' }
$env:TABLO_DEMO_PASSWORD = Read-Host 'Choose a local employee demo password (12+ characters)' -MaskInput
dotnet run --project tools/local-demo/Tablo.LocalDemo.csproj -- $cluster $artifacts
if ($LASTEXITCODE) { throw 'Demo scenario checks failed' }
$manifest = Get-Content "$artifacts/demo.json" -Raw | ConvertFrom-Json
```

Build the API before starting the hidden local processes. Use local demo credentials for Super Admin and use only the database named by the successful runner.

```powershell
$repo = (Get-Location).Path
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=55439;Database=$($manifest.database);Username=tablo_test;Pooling=false"
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5166'
$env:AllowedOrigins__0 = 'http://127.0.0.1:5173'
$env:SeedDummyData = 'false'
$env:Jwt__Key = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SuperAdmin__Username = 'superadmin@example.test'
$env:SuperAdmin__Password = Read-Host 'Choose a separate local Super Admin password' -MaskInput
dotnet build Tablo.Api/Tablo.Api.csproj
if ($LASTEXITCODE) { throw 'API build failed' }
$api = Start-Process dotnet -ArgumentList 'bin/Debug/net8.0/Tablo.Api.dll' -WorkingDirectory "$repo/Tablo.Api" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifacts/api.log" -RedirectStandardError "$artifacts/api.error.log"

$env:VITE_API_URL = 'http://127.0.0.1:5166/api'
$vite = Start-Process node -ArgumentList 'node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '5173', '--strictPort' -WorkingDirectory "$repo/tablo" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifacts/vite.log" -RedirectStandardError "$artifacts/vite.error.log"
Invoke-RestMethod 'http://127.0.0.1:5166/health'
```

PostgreSQL writes to `$cluster/server.log`. The API and Vite logs live in `$artifacts`. Use `$api.Id` and `$vite.Id` to identify these local processes.

## Browser walkthrough

Open [TABLO](http://127.0.0.1:5173/) and use separate browser profiles or sessions for each role. Employee accounts use the password chosen during seed. Super Admin uses the separate password chosen at API startup. For destructive or negative scenarios, run the seeder again and point a separate local session at that fixture.

| Workflow | Steps and expected evidence |
| --- | --- |
| Public pricing | Open **View Plans**. Compare Starter ₹4,990/₹2,990, Standard ₹5,999/₹3,999, Business ₹7,200/₹5,200, and Premium ₹8,990/₹6,990. Each first-year price includes one ₹2,000 setup fee. |
| Admin | Sign in as `admin@lantern.example.test`. Open [Menu & tables](http://127.0.0.1:5173/admin), inspect Malabar mains and Drinks, then open Tables and inspect the QR links. Open [Staff](http://127.0.0.1:5173/staff) and review Waiter and Kitchen users. |
| Customer | From the manifest, open Table 2 for Malabar Lantern. Inspect the pending QR order. Use Table 9 for a new order, then open order tracking and try **Call Waiter**. |
| Kitchen | Sign in as `kitchen@lantern.example.test`, open [Dashboard](http://127.0.0.1:5173/dashboard), and compare the separate New and Accepted tickets on Table 2. Table 3 has Preparing and Table 4 has Ready. Advance the Table 9 QR ticket from New through Accepted, Preparing, Ready and Served. |
| Waiter and billing | Sign in as `waiter@lantern.example.test`, open [Dashboard](http://127.0.0.1:5173/dashboard), then **Orders & billing**. Check Table 2 for distinct QR and staff tickets. Review Table 5 for the ₹50 Card payment and remaining balance. At Table 1, open the settled receipt and review Cash ₹200 and UPI ₹472. |
| Dining groups and table transfer | In **Orders & billing**, open Table 6 and choose B. Open Table 7 and choose A. Inspect the two open bill shares for the moved group. Compare the Table 6 and Table 7 QR links and the group-specific bill request. |
| Payments | As the Lantern admin, open [Reports](http://127.0.0.1:5173/reports), choose payment reports, select the fixture collection date in IST, and export Excel. Compare Table 1's two collections and Table 5's Card collection to the payment workbook. |
| Ingredients and recipes | Open [Inventory](http://127.0.0.1:5173/inventory), choose Ingredients and open Bone-in chicken. Switch to Recipes and inspect the 21 biryani ingredients. Open Movements and trace opening stock, sale consumption and the later restock. |
| Reservations | Open the Table 8 QR and inspect the pending biryani order. Compare availability with on-hand chicken stock. On a separate fixture, cancel the Table 8 order in Dashboard and check the restored availability. |
| Suppliers and purchasing | In Inventory, open Suppliers and review the two lime vendors and pack prices. Open Purchasing and review the pending and partially received POs, both supplier invoices with the same invoice reference, and the purchase return. |
| Batches and lots | Open Preparations and inspect lime cordial's 450 ml output and 50 ml production loss. Open Lots and review near-expiry stock and the quarantined lime lot. |
| Outlets and stocktakes | Open Transfers and compare the requested transfer with the received transfer, including 200 ml dispatched, 190 ml received and the 10 ml discrepancy. Open Stocktakes and inspect the closing count draft. |
| Gross profit and stock exports | In Inventory, open Stock Report and export Excel. Open Sales & Gross Profit, select the report day and biryani menu item from the manifest, expand the bill and export Excel. The target is exactly one bill with two portions, ₹640 net sales, ₹32 tax, ₹227.55 ingredient cost, ₹412.45 gross profit, 64.45% margin, and 21 ingredient snapshots. |
| Tenant and subscription | Sign in as `admin@lime.example.test`. Confirm the Basic trial account sees its own café, menu and table. Check the direct Inventory route and the Inventory export API with that account. Use separate sessions to confirm the Kitchen and Waiter also lack Inventory access. |
| Super Admin | Open the [Super Admin workspace](http://127.0.0.1:5173/super-admin) and use `superadmin@example.test` at Operator Sign In. Review Overview, Restaurants, Plans & Features, Subscriptions and Audit History. Look for the Harbour Lime trial change. Run expiry and downgrade checks on a separate fixture. |

## Verification status for this checkout

| Area | Status | Evidence |
| --- | --- | --- |
| Browser pricing | Pass | Local browser rendered all four price pairs and the ₹2,000 setup amount. The sales links exposed prefilled mailto targets. Existing cards still display detailed feature packaging. |
| Frontend automated checks | Pass | 16 Node tests, TypeScript check, Vite build. |
| Demo runner | Build only | Windows Application Control blocked the compiled runner with error 4551. The scenario assertions, generated QR links, and workbooks require a successful run. |
| API startup and backend tests | Blocked | Windows Application Control blocked the API DLL and xUnit test DLL with `0x800711C7`. Test discovery ran zero tests. No database-backed browser flows or migration upgrades were verified. |
| Historical ₹824.90 | Pending | The new demo checks a single two-portion bill. The historical aggregate still requires the original database or an export with bill-level detail. |
| Invoice scope | Source verified | The model, migration and receipt lookup use restaurant, supplier and invoice number. Runtime verification is in the demo runner. |
| Transfer valuation | Source finding | A dispatch of 200 ml and receipt of 190 ml writes ledger changes of −200, +190 and −10. A restaurant-wide sum of those entries gives −20 ml for a 10 ml physical loss. The demo checks transfer quantities. Valuation and existing movement reconciliation still require database verification. |
| Advanced and entitlement edge cases | Blocked | Last-portion concurrency, reservation release, expiry and quarantine, costing-method cutover, partial transfer receipts, outlet enforcement, and open-session entitlement refresh still require backend execution. |
| Hardware | Blocked | Physical thermal printing requires a connected restaurant printer. |
| Later blueprint | No matching implemented workflow found in the inspected routes and controllers | Dedicated KOT routing, ESC/POS workers, parcel and car-side flows, hold/fire, Khata, cash shifts, public token display and multi-brand operations remain outside this fixture. |

The backend plan codes in the seed are `basic`, `plus`, and `premium`. The four public pricing labels are display configuration.
