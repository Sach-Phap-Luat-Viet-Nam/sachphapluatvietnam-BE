# Deploy lên máy Windows (Windows Service + Cloudflare Tunnel)

## Mục đích

Chạy backend production trên một máy Windows tự quản lý (thay Render + Neon), public ra internet qua
Cloudflare Tunnel: không cần IP tĩnh, không mở cổng router, HTTPS do Cloudflare cấp.

```text
Khách ─► Vercel (FE, proxy /api/v1) ─► https://api.<domain> (Cloudflare) ─► cloudflared (service)
                                                                              │
                                              SachLuatApi (Windows Service) ◄─┘ http://127.0.0.1:5080
                                                │
                           WSL Ubuntu ─ docker compose: PostgreSQL :5433, Redis :6380
```

Script nằm ở `deploy/windows/`:

| File | Việc |
| --- | --- |
| `deploy.ps1` | Publish self-contained `win-x64`, tạo/cập nhật service `SachLuatApi`, nạp biến môi trường, chờ `/health/ready` |
| `service.env.example` | Mẫu cấu hình production (giá trị giả) |
| `register-tasks.ps1` | Scheduled Task giữ WSL luôn chạy + backup CSDL hằng ngày |
| `backup-db.ps1` | `pg_dump` ra file `.dump`, xóa bản cũ hơn `KeepDays` ngày |

Script chỉ dùng ký tự ASCII vì Windows PowerShell 5.1 đọc file UTF-8 không BOM theo bảng mã ANSI.

## Luồng xử lý

- Service chạy bằng virtual account `NT SERVICE\SachLuatApi`, khởi động `delayed-auto`, tự chạy lại khi thoát lỗi
  (5 giây, 10 giây, rồi 30 giây mỗi lần). Lúc bật máy WSL chưa sẵn sàng thì API thoát lỗi ở bước áp migration
  và được chạy lại cho tới khi kết nối được CSDL.
- Cấu hình nằm trong giá trị `Environment` của key service trong registry
  (`HKLM\SYSTEM\CurrentControlSet\Services\SachLuatApi`), không nằm trong thư mục cài đặt hay repo.
- `Program.cs` dùng `WindowsServiceHelpers.IsWindowsService()` để đặt content root là thư mục exe (thư mục hiện tại
  của service là `C:\Windows\System32`) và `AddWindowsService()` để báo trạng thái cho Service Control Manager.
  Ngoài service (`dotnet run`, Docker, test) không có tác dụng.
- Log mức Warning trở lên vào **Event Viewer → Windows Logs → Application**, nguồn `SachLuat.Api`.
- `Database__ApplyMigrationsOnStartup=true`: chỉ một instance nên tự áp migration khi khởi động, deploy bản có
  migration mới chỉ cần chạy lại `deploy.ps1`.

## Cài đặt lần đầu

Mọi lệnh PowerShell dưới đây chạy bằng **Run as Administrator**, trong thư mục repo.

### 1. Chuẩn bị máy

- .NET SDK 10 (để publish; máy chỉ chạy thì không cần runtime vì bản publish là self-contained), Git.
- WSL + Ubuntu, cài Docker Engine trong Ubuntu (xem `docs/setup.md`), bật systemd để Docker tự chạy cùng WSL:

  ```bash
  # Trong Ubuntu
  printf '[boot]\nsystemd=true\n' | sudo tee -a /etc/wsl.conf
  sudo systemctl enable docker
  # Từ PowerShell: wsl --shutdown rồi mở lại Ubuntu
  ```

- Tắt Sleep/Hibernate khi cắm điện: `powercfg /change standby-timeout-ac 0` và `powercfg /change hibernate-timeout-ac 0`.
- Không bật `networkingMode=mirrored` trong `%USERPROFILE%\.wslconfig`: ở chế độ đó cổng 5433/6380 của container
  có thể lộ ra mạng LAN. Chế độ mặc định (NAT) chỉ chuyển tiếp về `localhost`.

### 2. PostgreSQL + Redis

```powershell
git clone <repo> ; cd <repo>
# .env cho docker compose: chỉ cần POSTGRES_PASSWORD (mật khẩu mạnh, ngẫu nhiên)
Set-Content .env 'POSTGRES_PASSWORD=<mật khẩu>' -Encoding ascii
wsl -d Ubuntu --cd "$((wsl -d Ubuntu -- wslpath -a ($PWD.Path -replace '\\','/')))" -- docker compose up -d
```

Container có `restart: unless-stopped` nên tự chạy lại mỗi khi Docker khởi động.

### 3. Chuyển dữ liệu từ Neon (nếu thay production cũ)

Làm trong khung giờ bảo trì: trong lúc chuyển, đơn mới ghi vào Neon sẽ bị mất.

1. Kiểm tra phiên bản PostgreSQL của Neon (`SELECT version();` trong SQL Editor của Neon). `docker-compose.yml`
   đang dùng `postgres:16`. Nếu Neon là bản mới hơn (17, 18) thì đổi image trong compose cho cùng major version
   **trước khi** `docker compose up` lần đầu (đổi sau khi đã có dữ liệu thì phải dump/restore lại).
2. Dừng ghi vào Neon: suspend service trên Render (Dashboard → service → Suspend). FE sẽ báo lỗi API trong lúc này.
3. Dump từ Neon bằng **direct connection** (host không có `-pooler`; lấy ở Neon → Connect, tắt "Connection pooling"),
   dùng image `postgres` cùng major version với Neon:

   ```bash
   # Trong Ubuntu
   mkdir -p ~/migrate
   docker run --rm -v ~/migrate:/backup postgres:16-alpine \
     pg_dump "postgresql://<user>:<password>@<host-direct>/sachluat?sslmode=require" \
     --format=custom --no-owner --no-acl -f /backup/neon.dump
   ```

4. Restore vào CSDL local **trước khi** chạy API lần đầu (CSDL còn trống):

   ```bash
   docker exec -i sachluat-postgres-1 pg_restore -U sachluat -d sachluat --no-owner --no-acl < ~/migrate/neon.dump
   ```

   Lỗi liên quan schema `public` hoặc role/extension của Neon có thể bỏ qua; lỗi khác thì dừng lại kiểm tra.
   So số dòng vài bảng chính giữa Neon và local, ví dụ
   `SELECT count(*) FROM ordering.orders;`, `SELECT count(*) FROM auth.users;`.
5. Redis không chuyển: giỏ hàng đang mở trên Render mất, khách thêm lại sách vào giỏ.

### 4. Cấu hình và cài service

```powershell
mkdir "$env:USERPROFILE\sachluat"
Copy-Item deploy\windows\service.env.example "$env:USERPROFILE\sachluat\service.env"
notepad "$env:USERPROFILE\sachluat\service.env"   # điền giá trị thật (lấy từ Environment của Render)
.\deploy\windows\deploy.ps1 -EnvFile "$env:USERPROFILE\sachluat\service.env"
```

- `ConnectionStrings__Default` trỏ `localhost:5433` với mật khẩu ở bước 2; `ConnectionStrings__Redis=localhost:6380`.
- Giữ nguyên `Jwt__SigningKey` của Render thì phiên đăng nhập cũ còn hiệu lực; đổi khóa thì mọi người đăng nhập lại.
- Script kết thúc bằng `Xong: http://127.0.0.1:5080/health/ready tra 200.`

### 5. Giữ WSL chạy và backup hằng ngày

```powershell
.\deploy\windows\register-tasks.ps1 -BackupDir 'D:\Backups\SachLuat'
```

Script hỏi mật khẩu tài khoản Windows (task chạy cả khi chưa đăng nhập). Nên để `BackupDir` ở ổ khác hoặc thư mục
được đồng bộ lên cloud (OneDrive, Google Drive): hỏng ổ đĩa mà backup nằm cùng ổ thì mất cả hai.

### 6. Cloudflare Tunnel

1. Thêm domain vào Cloudflare (gói Free) và đổi nameserver ở nhà đăng ký domain theo hướng dẫn của Cloudflare.
2. Cài cloudflared: `winget install --id Cloudflare.cloudflared`.
3. Cloudflare Dashboard → Zero Trust → Networks → Tunnels → Create a tunnel → Cloudflared, đặt tên `sachluat-api`,
   chọn Windows, chạy lệnh `cloudflared.exe service install <token>` được hiển thị (tạo service `cloudflared`).
4. Thêm Public Hostname: subdomain `api`, domain của Anh, Service `HTTP` → `127.0.0.1:5080`.
5. Kiểm tra: `https://api.<domain>/health/ready` trả `Healthy`.

### 7. Chuyển FE và dịch vụ ngoài sang URL mới

- Vercel: đổi biến URL backend của FE sang `https://api.<domain>`, `PROXY_SECRET` trùng `TrustedProxy__Secret`, redeploy.
- SePay: sửa webhook thành `https://api.<domain>/api/v1/payments/sepay/webhook` (xem `docs/features/sepay-payment.md`).
- Đăng nhập Admin, đặt thử một đơn, kiểm tra tin nhắn Zalo và ảnh Cloudinary.
- Giữ Render (đã suspend) và Neon vài tuần để quay lại khi cần; dữ liệu mới chỉ còn ở máy này.

## Cập nhật bản mới

```powershell
git pull
.\deploy\windows\deploy.ps1 -EnvFile "$env:USERPROFILE\sachluat\service.env"
```

API ngừng trong lúc dừng service, copy file và khởi động (thường dưới 1 phút). Chỉ đổi cấu hình:
sửa `service.env` rồi chạy lại với `-SkipPublish`.

## Backup và khôi phục

- Chạy tay: `.\deploy\windows\backup-db.ps1 -BackupDir 'D:\Backups\SachLuat'`.
- Kết quả task hằng ngày: Task Scheduler → `SachLuat DB backup` → Last Run Result (`0x0` là thành công).
- Khôi phục (ghi đè toàn bộ dữ liệu hiện tại):

  ```powershell
  Stop-Service SachLuatApi
  ```

  ```bash
  # Trong Ubuntu, <file> là đường dẫn WSL của file .dump, ví dụ /mnt/d/Backups/SachLuat/sachluat-20261028-020000.dump
  docker exec -i sachluat-postgres-1 pg_restore -U sachluat -d sachluat --clean --if-exists --no-owner < <file>
  ```

  ```powershell
  Start-Service SachLuatApi
  ```

## API

Không đổi route. Production không có Scalar/OpenAPI (chỉ Development). Health: `/health/live`, `/health/ready`.

## Cấu hình cần thiết

Danh sách biến: `deploy/windows/service.env.example`. Tham số `deploy.ps1`:

| Tham số | Mặc định | Ghi chú |
| --- | --- | --- |
| `-EnvFile` | (bắt buộc) | Thiếu `ConnectionStrings__Default`, `ConnectionStrings__Redis` hoặc `Jwt__SigningKey` thì dừng |
| `-InstallDir` | `C:\Services\SachLuat.Api` | `robocopy /MIR` vào đây: script từ chối thư mục đã có file khác |
| `-ServiceName` | `SachLuatApi` | |
| `-Port` | `5080` | Đặt `ASPNETCORE_URLS=http://127.0.0.1:<Port>`, ghi đè giá trị trong file env |
| `-SkipPublish` | | Dùng lại bản publish trong `artifacts/publish/SachLuat.Api` |

## Thay đổi CSDL (migration liên quan)

Không có migration mới. Package mới: `Microsoft.Extensions.Hosting.WindowsServices` (chính thức của Microsoft).

## Hạn chế / việc còn lại

- Một máy, không dự phòng: mất điện, mất mạng, Windows Update khởi động lại là API ngừng cho tới khi máy chạy lại
  (service và task tự chạy khi bật máy).
- Registry key của service đọc được bởi mọi tài khoản trên máy (quyền mặc định của Windows): chỉ phù hợp máy không có
  người dùng khác.
- Task giữ WSL chạy dưới session không đăng nhập. Sau khi cài, khởi động lại máy **chưa đăng nhập** rồi kiểm tra
  `https://api.<domain>/health/ready` từ máy khác; nếu PostgreSQL/Redis không lên thì đổi trigger task sang
  "At log on" và bật tự đăng nhập Windows.
- `deploy.ps1` và `register-tasks.ps1` chưa chạy thử trên máy có quyền Administrator; phần publish, chạy exe ở
  Production và `backup-db.ps1` đã chạy thử.
