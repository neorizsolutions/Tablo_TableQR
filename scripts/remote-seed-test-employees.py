import os
import subprocess
import re

env_file = "/home/ubuntu/tablo-api.env"
with open(env_file, "r") as f:
    content = f.read()

m = re.search(r"Host=([^;]+);Database=([^;]+);Username=([^;]+);Password=([^;\r\n]+)", content)
host, db, user, password = m.groups()
env = os.environ.copy()
env["PGPASSWORD"] = password

hash_val = "$2b$10$olrSvdqLFsVGpa5QCYHtPuDVKTh3C9z2gT.BULb0f3MdcUtr9OOfe"

query = f"""
UPDATE "Employees"
SET "PasswordHash" = '{hash_val}', "IsActive" = true
WHERE "Email" LIKE '%@test.tablo.local';

SELECT "Id", "Email", "FirstName", "LastName", "RoleId", "RestaurantId" FROM "Employees" WHERE "Email" LIKE '%@test.tablo.local';
"""

cmd = ["psql", "-h", host, "-U", user, "-d", db, "-c", query]
res = subprocess.run(cmd, env=env, capture_output=True, text=True)
print("Updated test employees:\n", res.stdout)
