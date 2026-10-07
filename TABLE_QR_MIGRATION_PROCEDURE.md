# TABLE QR — Explicit EF Core Migration Procedure

## 1. Purpose and Approved Strategy
In the TABLO application, normal API startup no longer applies EF Core migrations. The automatic execution of `context.Database.Migrate()` has been explicitly removed from `Program.cs` to prevent race conditions and database corruption in multi-instance deployments. 

The approved strategy is **Option B**: Database migrations must be applied through a separate, deliberate, explicit operation before starting or updating any production API instances.

## 2. Preconditions
Before executing any migration operation, the operator must:
* **Confirm the Target:** Verify the exact target environment (e.g., Production vs. Staging) and the correct database host/instance.
* **Confirm the Version:** Ensure you are executing the migration set that corresponds exactly to the application version being deployed.
* **Confirm Backups:** Ensure a suitable, recent database backup or recovery plan exists and has been tested.
* **Confirm API Halting:** Ensure no API deployment is started, scaled up, or updated until the migration operation succeeds completely. 
* **Use Restricted Accounts:** Use an appropriately restricted database account that has permission to alter schema but is separate from the application runtime account if possible.

## 3. Connection-String Security
Production connection strings are highly sensitive secrets. They **must not** be committed to source control, embedded in script files, or pasted into shared logs or ticketing systems.

* **Secure Handling:** Prefer injecting the connection string securely via a secret-management mechanism or ephemeral environment variables during a CI/CD pipeline step.
* **Command-Line Risks:** Passing a password directly as a command-line argument (e.g., `--connection "Host=...Password=SECRET"`) is dangerous and exposes the password in shell history (e.g., `.bash_history`) and process inspection tools (e.g., `htop`, `ps`).

## 4. Explicit Migration Command

The `Tablo.Api` project uses an `IDesignTimeDbContextFactory<ApplicationDbContext>`. Because of this, the `dotnet ef` CLI **ignores** the `--connection` argument. 

Instead, you must target the database by setting the `ConnectionStrings__DefaultConnection` environment variable. The design-time factory loads environment variables last, which allows them to safely override any default connection string defined in `appsettings.json`.

**CRITICAL WARNINGS:**
* **Verify Target:** Do not run the migration command unless the intended database connection has been configured and verified.
* **Silent Fallback:** If the environment variable is missing or mistyped, the factory will silently fall back to the `appsettings.json` connection string (which targets the localhost development database).
* **Secret Protection:** Never paste production credentials into command-line arguments, documentation, logs, or source control.
* **Avoid Echoing:** Do not print or echo the full connection string as part of your verification checks.

### Execution Procedure

**Working Directory:** `Tablo.Api`

**PowerShell Example:**
```powershell
# 1. Set the environment variable securely
$env:ConnectionStrings__DefaultConnection = $SecureDbConnectionString

# 2. Preflight check: Verify the variable is present and non-empty (without printing the value)
if (-not $env:ConnectionStrings__DefaultConnection) { throw "Connection string missing!" } else { Write-Host "Connection string configured." }

# 3. Execute migrations
dotnet ef database update

# 4. Clear the environment variable
$env:ConnectionStrings__DefaultConnection = $null
```

**Bash Example:**
```bash
# 1. Set the environment variable securely
export ConnectionStrings__DefaultConnection="$SECURE_DB_CONNECTION_STRING"

# 2. Preflight check: Verify the variable is present and non-empty (without printing the value)
if [ -z "$ConnectionStrings__DefaultConnection" ]; then echo "Connection string missing!" && exit 1; else echo "Connection string configured."; fi

# 3. Execute migrations
dotnet ef database update

# 4. Clear the environment variable
unset ConnectionStrings__DefaultConnection
```

**EF TOOLING STARTUP BEHAVIOR:**
When you run `dotnet ef` CLI commands, EF Core discovers the factory and uses it to instantiate the `ApplicationDbContext`. This safely bypasses the application's `Program.cs` top-level statements. As a result:
* Normal application initialization is not executed.
* `DbSeeder.Seed()` is not invoked.
* The API server does not start.
* Database initialization risks on empty schemas are avoided.

## 5. Verification
Once the command is rendered safe and executed, operators must verify its success:
* **Check Tool Output:** The EF Core CLI should report `Done.` without preceding SQL or application-level exceptions. 
* **Check Migration History:** Connect directly to the PostgreSQL database using `psql` or `pgAdmin` and query the `__EFMigrationsHistory` table. Verify that the final migration ID in the table matches the most recent migration applied by the source code.
* Do not rely solely on the CLI's success exit code as absolute proof that the intended database was updated (e.g., if the connection string resolved to a different environment by mistake).

## 6. Failure Handling
If an explicit migration fails:
1. **Stop Deployment:** Immediately halt the deployment pipeline. Do not start the new API instances.
2. **Capture Error Securely:** Review the terminal or CI/CD logs to capture the error message, ensuring no secrets (like the connection string) are copied or exposed.
3. **Inspect Actual State:** Connect directly to the database and query `__EFMigrationsHistory` to determine if the migration partially applied. 
4. **Follow Recovery:** Do not repeatedly rerun an uncertain or partially applied migration. Follow the project's established database recovery or manual rollback procedure.

## 7. API Startup and Deployment Order
When deploying a new version of TABLO that requires a schema update, the strict sequence of operations is:
1. **Prepare and Verify:** Ensure backups are taken and the target database is ready.
2. **Apply Migrations:** Execute the explicit migration command.
3. **Verify Completion:** Verify success in `__EFMigrationsHistory`.
4. **Start/Update APIs:** Only after verification, start or update the API instances to the new version.
5. **Verify Health:** Verify application health checks and ensure database-dependent endpoints function correctly.

## 8. Current Limitations
* **No Database Migrated:** No production database has been migrated as part of the current task.
* **No Infrastructure Provisioned:** No AWS resources have been provisioned or modified.
* **SuperAdmin Bootstrap:** The production environment lacks a secure way to initialize the SuperAdmin account. This remains an unresolved requirement.
