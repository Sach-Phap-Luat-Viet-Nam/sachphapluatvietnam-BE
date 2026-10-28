#!/usr/bin/env bash
# Backup CSDL production ra file .dump, xóa bản cũ hơn KEEP_DAYS ngày. Cron gọi hằng ngày (docs/deploy-linux.md).
# Dùng: BACKUP_DIR=/var/backups/sachluat KEEP_DAYS=14 bash deploy/backup-db.sh
set -euo pipefail

backup_dir="${BACKUP_DIR:-/var/backups/sachluat}"
keep_days="${KEEP_DAYS:-14}"
compose_file="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/docker-compose.prod.yml"

mkdir -p "$backup_dir"
target="$backup_dir/sachluat-$(date +%Y%m%d-%H%M%S).dump"

# Ghi ra file tạm rồi mới đổi tên: lỗi giữa chừng không để lại file dump dở trông như bản hợp lệ.
# -T: không cấp TTY để dữ liệu nhị phân đi nguyên vẹn qua stdout.
if ! docker compose -f "$compose_file" exec -T postgres \
  pg_dump -U sachluat -d sachluat --format=custom > "$target.partial"; then
  rm -f "$target.partial"
  echo "pg_dump lỗi, không tạo backup" >&2
  exit 1
fi
mv "$target.partial" "$target"
echo "Đã backup: $target ($(du -h "$target" | cut -f1))"

find "$backup_dir" -maxdepth 1 -name 'sachluat-*.dump' -type f -mtime "+$keep_days" -print -delete
