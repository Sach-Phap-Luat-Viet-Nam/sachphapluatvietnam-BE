# Deploy production lên máy Linux (Docker Compose + Cloudflare Tunnel)

## Mục đích

Chạy toàn bộ production (backend, PostgreSQL, Redis, storefront, admin) trên một máy Ubuntu Server tự quản lý, thay
Render + Neon + Vercel. Public ra internet qua Cloudflare Tunnel: không cần IP tĩnh, không mở cổng router, HTTPS do
Cloudflare cấp.

```text
Khách ─► Cloudflare ─► cloudflared ─┬─► storefront:3000 ─┐  (sachluat.vn, www)
                                    ├─► admin:3000 ──────┤  (admin.sachluat.vn)
                                    └─► api:8080 ◄───────┘  (api.sachluat.vn: webhook SePay)
                                          │   FE gọi API trong mạng compose (http://api:8080)
                                          └─► postgres:5432, redis:6379
```

Một tên miền là đủ: storefront ở tên miền chính, admin và api là subdomain.

| File | Việc |
| --- | --- |
| `deploy/docker-compose.prod.yml` | `postgres`, `redis`, `api` (`Dockerfile` repo BE), `storefront`, `admin` (`Dockerfile` repo FE), `cloudflared` |
| `deploy/.env.example` | Biến dùng chung: mật khẩu CSDL, `PROXY_SECRET`, URL public, Pusher cho FE, token tunnel |
| `deploy/api.env.example` | Cấu hình riêng của API: JWT, Cloudinary, SePay, Zalo, Pusher, ... |
| `deploy/update.sh` | `git pull` 2 repo, build API rồi FE, khởi động lại, chờ từng service trả 200 |
| `deploy/backup-db.sh` | `pg_dump` ra `.dump`, xóa bản cũ hơn `KEEP_DAYS` ngày |

`deploy/.env` và `deploy/api.env` (giá trị thật) đã nằm trong `.gitignore`. Cách build image FE: `docs/deploy-self-host.md`
của repo FE.

## Luồng xử lý

- Mọi service `restart: unless-stopped`; Docker tự chạy khi bật máy nên cả stack tự lên lại sau khi mất điện
  hoặc khởi động lại. `api` chỉ khởi động khi `postgres` và `redis` đã healthy.
- Không service nào mở cổng ra mạng: `api`, `storefront`, `admin` chỉ mở `127.0.0.1` (8080, 3000, 3001) để kiểm tra
  tại chỗ. Internet chỉ vào qua `cloudflared` (kết nối chiều ra tới Cloudflare), nên router và `ufw` không mở cổng nào.
- FE gọi API qua `http://api:8080` trong mạng compose. Proxy `/api/v1` của FE lấy IP khách từ `cf-connecting-ip`
  (Cloudflare tự đặt, khách không giả được) và gửi kèm `PROXY_SECRET`; compose truyền cùng giá trị này cho
  `TrustedProxy__Secret` của API.
- **Thứ tự build**: lúc `next build`, Next.js render trước trang tĩnh/ISR bằng dữ liệu gọi từ API, nên `update.sh`
  build và chạy API trước, rồi build FE với `network: host` để gọi được `127.0.0.1:8080`. Không dùng
  `docker compose up --build` cho cả stack (FE build lỗi khi API chưa chạy).
- `NEXT_PUBLIC_*` và `STOREFRONT_URL` được gắn vào JavaScript lúc build: đổi giá trị thì chạy lại `update.sh`.
- `Database__ApplyMigrationsOnStartup=true`: chỉ một instance nên API tự áp migration khi khởi động.
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

Hai repo nằm cạnh nhau (`FE_DIR` mặc định là `../../sachphapluatvietnam-FE` tính từ `deploy/`). Repo private: đăng
nhập trước bằng `gh auth login` hoặc thêm deploy key.

```bash
sudo mkdir -p /opt/sachluat && sudo chown $USER: /opt/sachluat && cd /opt/sachluat
git clone https://github.com/Sach-Phap-Luat-Viet-Nam/sachphapluatvietnam-BE.git
git clone https://github.com/Sach-Phap-Luat-Viet-Nam/sachphapluatvietnam-FE.git
cd sachphapluatvietnam-BE
cp deploy/.env.example deploy/.env && cp deploy/api.env.example deploy/api.env
chmod 600 deploy/.env deploy/api.env
openssl rand -hex 32            # chạy 2 lần: POSTGRES_PASSWORD và PROXY_SECRET
nano deploy/.env
nano deploy/api.env             # lấy giá trị thật từ Environment của service trên Render
```

URL repo FE ở trên suy theo tên repo BE, Anh kiểm tra lại trên GitHub.

- `POSTGRES_PASSWORD` chỉ dùng chữ và số (nằm trong connection string).
- `POSTGRES_VERSION` phải trùng major version của Neon nếu chuyển dữ liệu (bước 5).
- `STOREFRONT_URL`, `ADMIN_URL` theo tên miền đã mua, ví dụ `https://sachluat.vn`, `https://admin.sachluat.vn`.
- `NEXT_PUBLIC_PUSHER_KEY`, `NEXT_PUBLIC_PUSHER_CLUSTER` trùng `Pusher__Key`, `Pusher__Cluster` trong `api.env`.
- `CLOUDFLARE_TUNNEL_TOKEN` để `change-me` cho tới bước 7.
- Giữ nguyên `Jwt__SigningKey` của Render thì phiên đăng nhập cũ còn hiệu lực; đổi khóa thì mọi người đăng nhập lại.
- Trong `api.env`, dòng nào chưa dùng thì để comment: bỏ comment với giá trị rỗng là ghi đè mặc định bằng chuỗi rỗng.

### 5. Chuyển dữ liệu từ Neon

Làm trong khung giờ bảo trì: trong lúc chuyển, đơn mới ghi vào Neon sẽ bị mất.

1. Xem phiên bản trong SQL Editor của Neon: `SELECT version();`, đặt `POSTGRES_VERSION` cùng major version.
2. Dừng ghi vào Neon: Render Dashboard → service → **Suspend**. Web báo lỗi API trong lúc này.
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
6. Redis chỉ chứa giỏ hàng nên không chuyển: giỏ đang mở trên Render mất, khách thêm lại sách vào giỏ.

### 6. Build và chạy

```bash
bash deploy/update.sh --no-pull
```

Lần đầu mất khoảng 10–20 phút (tải image, `pnpm install`, build 2 app Next.js). Kết thúc bằng 3 dòng `OK:` của `api`,
`storefront`, `admin`. Lúc này `cloudflared` khởi động lại liên tục vì token còn là `change-me`, bước 7 sẽ sửa.

### 7. Cloudflare Tunnel

1. Thêm tên miền vào Cloudflare (gói Free), đổi nameserver ở nhà đăng ký tên miền theo hướng dẫn của Cloudflare.
2. Cloudflare Dashboard → Zero Trust → Networks → Tunnels → **Create a tunnel** → Cloudflared, đặt tên `sachluat`,
   chọn môi trường **Docker**, copy chuỗi sau `--token` vào `CLOUDFLARE_TUNNEL_TOKEN` trong `deploy/.env`.
3. Thêm **Public Hostname** (Service đều là `HTTP`):

   | Hostname | Service |
   | --- | --- |
   | `<tên miền>` (để trống subdomain) | `storefront:3000` |
   | `www.<tên miền>` | `storefront:3000` |
   | `admin.<tên miền>` | `admin:3000` |
   | `api.<tên miền>` | `api:8080` |

4. `docker compose -f deploy/docker-compose.prod.yml up -d cloudflared`, rồi mở từ máy khác:
   `https://<tên miền>/sach`, `https://admin.<tên miền>/dang-nhap`, `https://api.<tên miền>/health/ready`.

Tên menu trên Cloudflare có thể thay đổi theo thời gian; tìm mục Tunnels trong Zero Trust.

### 8. Chuyển hẳn sang máy nhà

- SePay: sửa webhook thành `https://api.<tên miền>/api/v1/payments/sepay/webhook` (`docs/features/sepay-payment.md`).
- Đăng nhập Admin, đặt thử một đơn, kiểm tra tin nhắn Zalo, ảnh Cloudinary, nút đăng nhập hộ khách.
- Giữ Render (đã suspend), Neon và 2 project Vercel vài tuần để quay lại khi cần (Resume service trên Render). Dữ liệu
  mới chỉ có ở máy này.
- Domain `*.vercel.app` cũ vẫn trỏ về bản Vercel: tắt hoặc xóa project Vercel khi không cần quay lại nữa.

### 9. Backup hằng ngày và theo dõi

```bash
mkdir -p ~/backups/sachluat
crontab -e
# thêm dòng (2 giờ sáng mỗi ngày, giữ 14 ngày):
0 2 * * * BACKUP_DIR=$HOME/backups/sachluat /usr/bin/bash /opt/sachluat/sachphapluatvietnam-BE/deploy/backup-db.sh >> $HOME/backups/sachluat/backup.log 2>&1
```

- **Nên chép backup ra ngoài máy**: hỏng SSD là mất cả dữ liệu lẫn backup. Ví dụ dùng
  [rclone](https://rclone.org/drive/) đồng bộ `~/backups/sachluat` lên Google Drive bằng một dòng cron chạy sau backup.
- Theo dõi: tạo monitor HTTP miễn phí (UptimeRobot hoặc tương đương) cho `https://api.<tên miền>/health/ready` và
  `https://<tên miền>/sach` để nhận email khi web ngừng.

## Vận hành

Các lệnh chạy trong `/opt/sachluat/sachphapluatvietnam-BE`, `c` là `docker compose -f deploy/docker-compose.prod.yml`.

| Việc | Lệnh |
| --- | --- |
| Cập nhật bản mới (BE + FE) | `bash deploy/update.sh` |
| Đổi `api.env` | `c up -d api` |
| Đổi `PROXY_SECRET`, URL, Pusher của FE | `bash deploy/update.sh --no-pull` (build lại FE) |
| Xem log | `c logs -f --tail 200 api` (hoặc `storefront`, `admin`, `cloudflared`) |
| Trạng thái | `c ps` |
| Backup tay | `BACKUP_DIR=$HOME/backups/sachluat bash deploy/backup-db.sh` |

Cập nhật làm từng service ngừng vài giây lúc thay container; build chạy trước trong lúc bản cũ vẫn phục vụ.

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

Xem `deploy/.env.example` và `deploy/api.env.example`. Không thêm key cấu hình mới cho API. FE có thêm biến
`CLIENT_IP_HEADER` (compose đặt `cf-connecting-ip`).

## Thay đổi CSDL (migration liên quan)

Không có migration mới. Không đổi code API.

## Hạn chế / việc còn lại

- Một máy, không dự phòng: mất điện, mất mạng, hỏng phần cứng là cả web ngừng; khôi phục dữ liệu dựa vào backup
  ngoài máy.
- Không còn tự deploy khi push và preview deploy theo PR như Vercel: cập nhật bằng `update.sh`.
- `cloudflared` dùng tag `latest`: `update.sh` không tự kéo bản mới. Cập nhật bằng
  `docker compose -f deploy/docker-compose.prod.yml pull cloudflared && docker compose -f deploy/docker-compose.prod.yml up -d cloudflared`.
- Request không đi qua proxy FE (ví dụ webhook SePay) có IP kết nối là của container `cloudflared`.
- Trang tĩnh/ISR được render lúc build bằng dữ liệu lúc đó, sau đó tự làm mới theo `revalidate` của từng trang.
- Đã chạy thử trên Docker trong WSL: dựng stack (trừ `cloudflared`) bằng `update.sh --no-pull`, backup và restore.
  Chưa chạy thử trên máy Ubuntu thật và với tunnel thật.
