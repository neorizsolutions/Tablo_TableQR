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

query = """
SELECT "Id", "TableNumber", "QRToken", "IsActive" FROM "RestaurantTables" WHERE "RestaurantId" = 'b7226b97-7e7f-489c-ab2e-cc1b0164afc9';
"""

cmd = ["psql", "-h", host, "-U", user, "-d", db, "-c", query]
res = subprocess.run(cmd, env=env, capture_output=True, text=True)
print("Tables and QR Tokens:\n", res.stdout)
