#!/usr/bin/env bash
# Cập nhật production: lấy code mới, build lại image API, khởi động lại, chờ /health/ready.
# Dùng: bash deploy/update.sh [--no-pull]. Hướng dẫn: docs/deploy-linux.md.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
compose=(docker compose -f deploy/docker-compose.prod.yml)

if [[ "${1:-}" != "--no-pull" ]]; then
  git pull --ff-only
fi

"${compose[@]}" build api
"${compose[@]}" up -d

for _ in $(seq 1 30); do
  if curl -fsS http://127.0.0.1:8080/health/ready > /dev/null 2>&1; then
    echo "OK: http://127.0.0.1:8080/health/ready trả 200"
    # Image API cũ thành dangling sau mỗi lần build lại.
    docker image prune -f > /dev/null
    exit 0
  fi
  sleep 3
done

echo "API chưa sẵn sàng sau 90 giây. Xem log: ${compose[*]} logs --tail 100 api" >&2
exit 1
