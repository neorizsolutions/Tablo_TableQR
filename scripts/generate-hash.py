import subprocess
import sys

try:
    import bcrypt
    pw = b"TestPass123!"
    h = bcrypt.hashpw(pw, bcrypt.gensalt(10)).decode()
    print("BCRYPT_HASH:", h)
except Exception as e:
    print("Error:", e)
    # Install bcrypt or use dotnet
