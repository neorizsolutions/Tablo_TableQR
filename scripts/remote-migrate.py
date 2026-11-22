import os
import subprocess
import re
import sys

env_file = "/home/ubuntu/tablo-api.env"
with open(env_file, "r") as f:
    content = f.read()

m = re.search(r"Host=([^;]+);Database=([^;]+);Username=([^;]+);Password=([^;\r\n]+)", content)
if not m:
    print("Could not parse connection string from", env_file)
    sys.exit(1)

host, db, user, password = m.groups()
env = os.environ.copy()
env["PGPASSWORD"] = password

# 1. Apply storefront outlet settings SQL
sql_file = "/home/ubuntu/release-v1.41/scripts/apply-storefront-outlet-settings-migration.sql"
print(f"Applying SQL: {sql_file}")
cmd = ["psql", "-h", host, "-U", user, "-d", db, "-f", sql_file]
res = subprocess.run(cmd, env=env, capture_output=True, text=True)
print("Migration exit code:", res.returncode)
print("Migration STDOUT:\n", res.stdout)
if res.stderr:
    print("Migration STDERR:\n", res.stderr)

# 2. Verify columns
cmd_verify = [
    "psql", "-h", host, "-U", user, "-d", db, "-c",
    """SELECT column_name, data_type, column_default 
       FROM information_schema.columns 
       WHERE table_name = 'Outlets' 
         AND column_name IN ('IsPublished', 'IsOpen', 'LeadTimeMinutes', 'MinOrderAmount', 'DeliveryBaseFee');
       SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;"""
]
res_v = subprocess.run(cmd_verify, env=env, capture_output=True, text=True)
print("Verification STDOUT:\n", res_v.stdout)
if res_v.stderr:
    print("Verification STDERR:\n", res_v.stderr)
