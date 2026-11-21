#!/bin/bash
set -e

echo "=== 1. Rotating tablo-api container ==="
sudo docker stop tablo-api || true
sudo docker rm tablo-api || true

sudo docker run -d --name tablo-api \
  -p 127.0.0.1:8080:8080 \
  --env-file /home/ubuntu/tablo-api.env \
  --restart unless-stopped \
  tablo-api:v1.41

echo "=== 2. Waiting 10 seconds for DB migrations & startup ==="
sleep 10

echo "=== 3. Container Logs ==="
sudo docker logs --tail 40 tablo-api

echo "=== 4. Testing HTTP endpoint ==="
curl -i http://127.0.0.1:8080/health
