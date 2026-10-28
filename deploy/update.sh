#!/usr/bin/env bash
# Cập nhật production: lấy code mới (BE + FE), build lại, khởi động lại, chờ từng service sẵn sàng.
# Dùng: bash deploy/update.sh [--no-pull]. Hướng dẫn: docs/deploy-linux.md.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
compose=(docker compose -f deploy/docker-compose.prod.yml)

# FE_DIR trong deploy/.env tính từ thư mục deploy/, giống cách compose hiểu build context.
fe_dir_setting="$(sed -n 's/^FE_DIR=//p' deploy/.env)"
fe_dir="$(cd deploy && cd "${fe_dir_setting:-../../sachphapluatvietnam-FE}" && pwd)"

wait_for() {
  local name="$1" url="$2"
  for _ in $(seq 1 40); do
    if curl -fsS "$url" > /dev/null 2>&1; then
      echo "OK: $name ($url) trả 200"
      return 0
    fi
    sleep 3
  done
  echo "$name chưa sẵn sàng sau 120 giây. Xem log: ${compose[*]} logs --tail 100 $name" >&2
  exit 1
}

if [[ "${1:-}" != "--no-pull" ]]; then
  git pull --ff-only
  git -C "$fe_dir" pull --ff-only
fi

# API chạy trước: lúc build, Next.js render trước trang tĩnh/ISR bằng dữ liệu gọi từ API.
"${compose[@]}" build api
"${compose[@]}" up -d api
wait_for api http://127.0.0.1:8080/health/ready

# Bản FE cũ vẫn phục vụ trong lúc build, chỉ ngừng vài giây khi thay container.
"${compose[@]}" build storefront admin
"${compose[@]}" up -d
wait_for storefront http://127.0.0.1:3000/sach
wait_for admin http://127.0.0.1:3001/dang-nhap

# Image cũ thành dangling sau mỗi lần build lại.
docker image prune -f > /dev/null
