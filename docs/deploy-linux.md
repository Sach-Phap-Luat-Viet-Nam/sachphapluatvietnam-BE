# Deploy production lên máy Linux (Docker Compose + Cloudflare Tunnel)

## Mục đích

Chạy backend production trên một máy Ubuntu Server tự quản lý (thay Render + Neon), public ra internet qua
Cloudflare Tunnel: không cần IP tĩnh, không mở cổng router, HTTPS do Cloudflare cấp.

```text
Khách ─► Vercel (FE, proxy /api/v1) ─► https://api.<domain> (Cloudflare)
                                              │
            máy Ubuntu, docker compose ─ cloudflared ─► api:8080 ─► postgres:5432, redis:6379
```

| File | Việc |
| --- | --- |
| `deploy/docker-compose.prod.yml` | 4 service `postgres`, `redis`, `api` (build từ `Dockerfile`), `cloudflared` |
| `deploy/.env.example` | Biến của compose: `POSTGRES_PASSWORD`, `POSTGRES_VERSION`, `CLOUDFLARE_TUNNEL_TOKEN` |
| `deploy/api.env.example` | Cấu hình app: JWT, `TrustedProxy`, Cloudinary, SePay, Zalo, ... |
| `deploy/update.sh` | `git pull`, build lại API, `up -d`, chờ `/health/ready` |
| `deploy/backup-db.sh` | `pg_dump` ra `.dump`, xóa bản cũ hơn `KEEP_DAYS` ngày |

`deploy/.env` và `deploy/api.env` (giá trị thật) đã nằm trong `.gitignore`.

## Luồng xử lý

- Mọi service `restart: unless-stopped`; Docker tự chạy khi bật máy nên cả stack tự lên lại sau khi mất điện
  hoặc khởi động lại. `api` chỉ khởi động khi `postgres` và `redis` đã healthy.
- `postgres`, `redis` không mở cổng ra máy chủ; `api` chỉ mở `127.0.0.1:8080` để kiểm tra health tại chỗ. Internet
  chỉ vào được qua `cloudflared` (kết nối chiều ra tới Cloudflare), nên router và `ufw` không cần mở cổng nào.
- Connection string và Redis do compose ghép từ `POSTGRES_PASSWORD` (tên host `postgres`, `redis` trong mạng compose).
- `Database__ApplyMigrationsOnStartup=true`: chỉ một instance nên tự áp migration khi khởi động; deploy bản có
  migration mới chỉ cần chạy `update.sh`.
- Log mỗi container giới hạn 5 file × 10 MB.

## Cài đặt lần đầu

### 1. Phần cứng và BIOS

- Cài hệ điều hành lên SSD.
- BIOS: **Restore on AC Power Loss** (tên khác: *AC Back*, *After Power Failure*) = **Power On**, để máy tự bật lại
  khi có điện.

### 2. Ubuntu Server 24.04 LTS

- Lúc cài: chọn **Install OpenSSH server**. Ở màn hình *Featured server snaps* **không** chọn `docker` (bản snap);
  cài Docker theo bước 3.
- Router: đặt DHCP reservation để máy luôn có cùng IP LAN (tiện SSH).
- Sau khi cài:

  ```bash
  sudo apt update && sudo apt full-upgrade -y
  sudo timedatectl set-timezone Asia/Ho_Chi_Minh
  sudo ufw allow OpenSSH && sudo ufw enable
  ```

- Bản vá bảo mật tự cài hằng ngày (`unattended-upgrades` bật sẵn). Cho phép tự khởi động lại khi bản vá cần, trong
  `/etc/apt/apt.conf.d/50unattended-upgrades`:

  ```text
  Unattended-Upgrade::Automatic-Reboot "true";
  Unattended-Upgrade::Automatic-Reboot-Time "03:30";
  ```

### 3. Docker Engine

Cài theo <https://docs.docker.com/engine/install/ubuntu/> (mục *Install using the apt repository*), rồi:

```bash
sudo usermod -aG docker $USER   # đăng xuất, đăng nhập lại để có hiệu lực
docker run --rm hello-world
```

### 4. Lấy code và cấu hình

Repo private: đăng nhập trước bằng `gh auth login` hoặc thêm deploy key.

```bash
sudo mkdir -p /opt/sachluat && sudo chown $USER: /opt/sachluat
git clone https://github.com/Sach-Phap-Luat-Viet-Nam/sachphapluatvietnam-BE.git /opt/sachluat
cd /opt/sachluat
cp deploy/.env.example deploy/.env && cp deploy/api.env.example deploy/api.env
chmod 600 deploy/.env deploy/api.env
openssl rand -hex 32            # dùng làm POSTGRES_PASSWORD
nano deploy/.env
nano deploy/api.env             # lấy giá trị thật từ Environment của service trên Render
```

- `POSTGRES_PASSWORD` chỉ dùng chữ và số (nằm trong connection string).
- `POSTGRES_VERSION` phải trùng major version của Neon nếu chuyển dữ liệu (bước 5).
- `CLOUDFLARE_TUNNEL_TOKEN` để `change-me` cho tới bước 7.
- Giữ nguyên `Jwt__SigningKey` của Render thì phiên đăng nhập cũ còn hiệu lực; đổi khóa thì mọi người đăng nhập lại.
- Trong `api.env`, dòng nào chưa dùng thì để comment: bỏ comment với giá trị rỗng là ghi đè mặc định bằng chuỗi rỗng.

### 5. Chuyển dữ liệu từ Neon

Làm trong khung giờ bảo trì: trong lúc chuyển, đơn mới ghi vào Neon sẽ bị mất.

1. Xem phiên bản trong SQL Editor của Neon: `SELECT version();`, đặt `POSTGRES_VERSION` cùng major version.
2. Dừng ghi vào Neon: Render Dashboard → service → **Suspend**. FE báo lỗi API trong lúc này.
3. Khởi động riêng PostgreSQL (CSDL còn trống, chưa chạy API):

   ```bash
   docker compose -f deploy/docker-compose.prod.yml up -d postgres
   ```

4. Dump từ Neon bằng **direct connection** (Neon → Connect, tắt *Connection pooling*: host không có `-pooler`).
   Image `postgres` cùng major version với Neon:

   ```bash
   source deploy/.env
   docker run --rm -v "$PWD":/backup postgres:${POSTGRES_VERSION}-alpine \
     pg_dump "postgresql://<user>:<password>@<host-direct>/sachluat?sslmode=require" \
     --format=custom --no-owner --no-acl -f /backup/neon.dump
   ```

5. Restore rồi so số dòng vài bảng chính với Neon:

   ```bash
   docker compose -f deploy/docker-compose.prod.yml exec -T postgres \
     pg_restore -U sachluat -d sachluat --no-owner --no-acl < neon.dump
   docker compose -f deploy/docker-compose.prod.yml exec postgres psql -U sachluat -d sachluat \
     -c 'select count(*) from auth.users' -c 'select count(*) from ordering.orders'
   rm neon.dump
   ```

   Lỗi về schema `public` hoặc role/extension riêng của Neon có thể bỏ qua; lỗi khác thì dừng lại kiểm tra.
6. Redis không chuyển: giỏ hàng đang mở trên Render mất, khách thêm lại sách vào giỏ.

### 6. Chạy API

```bash
bash deploy/update.sh --no-pull
```

Kết thúc bằng `OK: http://127.0.0.1:8080/health/ready trả 200`. Lúc này `cloudflared` khởi động lại liên tục vì
token còn là `change-me`, bước 7 sẽ sửa.

### 7. Cloudflare Tunnel

1. Thêm domain vào Cloudflare (gói Free), đổi nameserver ở nhà đăng ký domain theo hướng dẫn của Cloudflare.
2. Cloudflare Dashboard → Zero Trust → Networks → Tunnels → **Create a tunnel** → Cloudflared, đặt tên
   `sachluat-api`, chọn môi trường **Docker**, copy chuỗi sau `--token` vào `CLOUDFLARE_TUNNEL_TOKEN` trong `deploy/.env`.
3. Thêm **Public Hostname**: subdomain `api`, domain của Anh, Service `HTTP` → `api:8080`.
4. `docker compose -f deploy/docker-compose.prod.yml up -d cloudflared`, rồi mở
   `https://api.<domain>/health/ready` từ máy khác: trả `Healthy`.

Tên menu trên Cloudflare có thể thay đổi theo thời gian; tìm mục Tunnels trong Zero Trust.

### 8. Chuyển FE và dịch vụ ngoài sang URL mới

- Vercel: đổi biến URL backend của FE sang `https://api.<domain>`, `PROXY_SECRET` trùng `TrustedProxy__Secret`, redeploy.
- SePay: sửa webhook thành `https://api.<domain>/api/v1/payments/sepay/webhook` (`docs/features/sepay-payment.md`).
- Đăng nhập Admin, đặt thử một đơn, kiểm tra tin nhắn Zalo và ảnh Cloudinary.
- Giữ Render (đã suspend) và Neon vài tuần để quay lại khi cần (Resume service, trả URL cũ trên Vercel). Dữ liệu mới
  chỉ có ở máy này.

### 9. Backup hằng ngày và theo dõi

```bash
mkdir -p ~/backups/sachluat
crontab -e
# thêm dòng (2 giờ sáng mỗi ngày, giữ 14 ngày):
0 2 * * * BACKUP_DIR=$HOME/backups/sachluat /usr/bin/bash /opt/sachluat/deploy/backup-db.sh >> $HOME/backups/sachluat/backup.log 2>&1
```

- **Nên chép backup ra ngoài máy**: hỏng SSD là mất cả dữ liệu lẫn backup. Ví dụ dùng
  [rclone](https://rclone.org/drive/) đồng bộ `~/backups/sachluat` lên Google Drive bằng một dòng cron chạy sau backup.
- Theo dõi: tạo monitor HTTP miễn phí (UptimeRobot hoặc tương đương) cho `https://api.<domain>/health/ready` để nhận
  email khi API ngừng.

## Vận hành

| Việc | Lệnh (trong `/opt/sachluat`) |
| --- | --- |
| Cập nhật bản mới | `bash deploy/update.sh` |
| Đổi cấu hình `api.env` | `docker compose -f deploy/docker-compose.prod.yml up -d api` |
| Xem log | `docker compose -f deploy/docker-compose.prod.yml logs -f --tail 200 api` |
| Trạng thái | `docker compose -f deploy/docker-compose.prod.yml ps` |
| Backup tay | `BACKUP_DIR=$HOME/backups/sachluat bash deploy/backup-db.sh` |

Cập nhật làm API ngừng vài giây lúc thay container (build chạy trước, trong lúc bản cũ vẫn phục vụ).

Khôi phục từ backup (ghi đè toàn bộ dữ liệu hiện tại):

```bash
docker compose -f deploy/docker-compose.prod.yml stop api
docker compose -f deploy/docker-compose.prod.yml exec -T postgres \
  pg_restore -U sachluat -d sachluat --clean --if-exists --no-owner < ~/backups/sachluat/<file>.dump
docker compose -f deploy/docker-compose.prod.yml start api
```

## API

Không đổi route. Production không có Scalar/OpenAPI (chỉ Development). Health: `/health/live`, `/health/ready`.

## Cấu hình cần thiết

Xem `deploy/.env.example` và `deploy/api.env.example`. Không thêm key cấu hình mới cho app.

## Thay đổi CSDL (migration liên quan)

Không có migration mới. Không đổi code ứng dụng.

## Hạn chế / việc còn lại

- Một máy, không dự phòng: mất điện, mất mạng, hỏng phần cứng là API ngừng; khôi phục dữ liệu dựa vào backup ngoài máy.
- `cloudflared` dùng tag `latest`: `update.sh` không tự kéo bản mới. Cập nhật bằng
  `docker compose -f deploy/docker-compose.prod.yml pull cloudflared && docker compose -f deploy/docker-compose.prod.yml up -d cloudflared`.
- Request không đi qua proxy FE (ví dụ webhook SePay) có IP kết nối là của container `cloudflared`, giống
  sau proxy Render (`docs/features/auth.md`).
- Đã chạy thử trên Docker trong WSL: dựng stack (trừ `cloudflared`), `/health/ready`, `update.sh --no-pull`,
  backup và restore. Chưa chạy thử trên máy Ubuntu thật và với tunnel thật.
