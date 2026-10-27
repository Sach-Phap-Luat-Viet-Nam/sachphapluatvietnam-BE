# Cài đặt môi trường và cấu trúc dự án

## Mục đích

Hướng dẫn dựng môi trường local cho backend Sách Luật Việt Nam và giải thích cấu trúc
modular monolith để thêm module mới đúng quy ước. Chi tiết từng tính năng: `docs/features/*.md`.

## Yêu cầu

| Công cụ | Phiên bản | Ghi chú |
| --- | --- | --- |
| .NET SDK | 10.0.x | Ghim trong `global.json` (tự chọn bản 10.0 mới nhất đã cài, bỏ qua SDK 11 preview). |
| Docker | Docker Desktop (backend WSL2), hoặc Docker Engine cài trong distro WSL | Docker Desktop miễn phí cho doanh nghiệp nhỏ; công ty lớn cần license, khi đó dùng Docker Engine trong WSL. |
| IDE | Visual Studio 2026 / Rider / VS Code + C# Dev Kit | Test chạy bằng Microsoft.Testing.Platform. |

## Chạy lần đầu

```bash
# 1. Khôi phục công cụ (dotnet-ef) và package
dotnet tool restore
dotnet restore

# 2. Khởi động PostgreSQL (cổng 5433) và Redis (cổng 6380)
cp .env.example .env          # rồi đặt POSTGRES_PASSWORD trong .env
docker compose up -d

# 3. Khai báo bí mật (lưu ngoài repo, không commit)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=sachluat;Username=sachluat;Password=<mật khẩu trong .env>" \
  --project src/SachLuat.Api
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/SachLuat.Api
# Super Admin đầu tiên (tạo lúc khởi động nếu email chưa có; xóa 2 dòng này sau khi đã tạo)
dotnet user-secrets set "Auth:BootstrapAdmin:Email" "<email>" --project src/SachLuat.Api
dotnet user-secrets set "Auth:BootstrapAdmin:Password" "<mật khẩu ≥ 8 ký tự>" --project src/SachLuat.Api

# 4. Chạy API (Development tự áp migration của mọi module khi khởi động)
dotnet run --project src/SachLuat.Api
```

Sau khi chạy:

- API reference (Scalar): <http://localhost:5279/scalar> (chỉ ở môi trường Development)
- OpenAPI JSON: <http://localhost:5279/openapi/v1.json>
- Health: `/health/live` (tiến trình còn sống), `/health/ready` (đã kết nối được CSDL và Redis)
- Đăng nhập Super Admin → bật 2FA (`/auth/2fa/setup`, `/auth/2fa/enable`) trước khi gọi API Admin,
  xem `docs/features/auth.md`.

Thiếu cấu hình bắt buộc thì API dừng ngay khi khởi động (ví dụ `Connection string 'Default' is not configured`,
lỗi validate `JwtOptions`).

Lần đầu áp migration mỗi schema, log EF có dòng `fail: ... Failed executing DbCommand ... __EFMigrationsHistory`:
đây là EF thử đọc bảng lịch sử chưa tồn tại rồi tạo, không phải lỗi.

### Docker Engine trong WSL (không dùng Docker Desktop)

- Chạy compose từ Windows: `wsl -d Ubuntu --cd "<đường dẫn repo dạng /mnt/c/...>" -- docker compose up -d`,
  hoặc mở terminal Ubuntu, `cd` vào repo rồi chạy `docker compose up -d`.
- **WSL tự tắt distro khi không còn tiến trình `wsl` nào mở**, kéo theo Docker, PostgreSQL, Redis tắt theo.
  Dấu hiệu: `/health/ready` trả 503, Npgsql báo `actively refused`, `wsl -l -v` hiện `Stopped`.
  Cách xử lý: giữ một terminal Ubuntu mở trong lúc làm việc. Container có `restart: unless-stopped`
  nên tự chạy lại khi Docker khởi động.
- Sau khi container vừa chạy, `wslrelay` cần vài giây mới chuyển tiếp cổng sang `localhost` của Windows.
- Cổng mặc định **5433** (PostgreSQL) và **6380** (Redis) để không trùng dịch vụ của dự án khác trên cùng máy.

## Cấu trúc solution

```text
SachLuat.slnx
Directory.Build.props          # net10.0, Nullable, analyzer cho mọi project
Directory.Packages.props       # version package tập trung (Central Package Management)
global.json                    # ghim SDK 10, bật Microsoft.Testing.Platform cho dotnet test
docker-compose.yml             # PostgreSQL 16 + Redis 7 cho local
.github/workflows/ci.yml       # CI: format, build, test (kèm PostgreSQL + Redis), build Docker image
src/
  SachLuat.Api/                # Host: Program.cs, xử lý lỗi, bảo mật, đăng ký module
  SachLuat.SharedKernel/       # IModule, BusinessException, ErrorCodes, ApiRoutes, Roles/Policies,
                               # ICurrentUser, PagedQuery, FluentValidationFilter, AuditableEntity,
                               # Media (ký upload Cloudinary dùng chung)
  Modules/
    Auth/                      # Identity + JWT + 2FA (schema auth)
    Catalog/                   # Đầu sách, ấn bản, SKU (schema catalog) + Catalog.Contracts
    Inventory/                 # Tồn kho, phiếu nhập, lô in (schema inventory) + Inventory.Contracts
    Cart/                      # Giỏ hàng trên Redis + Cart.Contracts
    Ordering/                  # Đơn hàng (schema ordering)
    Content/                   # Nội dung: popup quảng cáo (schema content)
    Shipping/                  # Vùng giao hàng, biểu phí, ngưỡng miễn ship (schema shipping) + Shipping.Contracts
    Promotion/                 # Chương trình khuyến mãi, mã giảm giá (schema promotion) + Promotion.Contracts
tests/
  SachLuat.UnitTests/
  SachLuat.IntegrationTests/   # WebApplicationFactory<Program>, test CSDL thật, end-to-end
```

Bên trong một module:

```text
SachLuat.Modules.<Ten>/
  <Ten>Module.cs               # Đăng ký DI, DbContext, health check, MigrateAsync
  Domain/                      # Entity, quy tắc nghiệp vụ, mã lỗi
  Application/                 # Service, DTO, validator (FluentValidation)
  Infrastructure/Persistence/  # DbContext, Migrations/
  Controllers/
```

### Quy tắc ranh giới module

- Mỗi module là **một project**. Mọi thứ để `internal`, kể cả controller (host nạp controller internal qua
  `InternalControllerFeatureProvider`); chỉ `<Ten>Module` là public.
- Mỗi module sở hữu **một schema PostgreSQL riêng** và một `DbContext` riêng, có bảng `__EFMigrationsHistory`
  riêng trong schema đó. **Không đặt khóa ngoại chéo schema**; chỉ lưu id và kiểm tra qua contract.
- Tên bảng/cột tự chuyển sang `snake_case` (package `EFCore.NamingConventions`); enum lưu chuỗi snake_case.
- Module **không** reference project của module khác, chỉ reference `SachLuat.Modules.<Ten>.Contracts`
  (interface + DTO public). Hiện có: `ICatalogSkuReader`, `IInventoryService`, `ICurrentCart`.
- Thao tác giữa hai module không chung transaction: thiết kế idempotent theo mã tham chiếu (ví dụ mã đơn) và
  có bước bù trừ khi lỗi (xem `docs/features/ordering.md`).

## Quy ước API

- Route: `ApiRoutes.V1` (`api/v1`), `ApiRoutes.AdminV1` (`api/v1/admin`).
- Validate bằng FluentValidation: khai báo `AbstractValidator<TRequest>` trong module, `FluentValidationFilter`
  tự chạy và trả 400 `VALIDATION_ERROR` với `errors` theo tên field camelCase.
- Phân trang: kế thừa `PagedQuery` (`page`, `limit` mặc định 24, tối đa 100), validator kế thừa
  `PagedQueryValidator<T>`; trả `PagedResponse<T>` = `{items, meta: {page, limit, total}}`.
- Enum trong JSON dạng snake_case. Enum trong query string là `string` rồi parse bằng
  `SnakeCaseEnumConverter<T>.TryParse` (model binding mặc định không hiểu `on_sale`).
- API Admin: `[Authorize(Policy = Policies.Xxx)]`, mọi policy yêu cầu đăng nhập có 2FA.
- Người dùng hiện tại: inject `ICurrentUser` (`UserId`, `RequireUserId()`). Khi Super Admin fast login vào hộ,
  `UserId` là tài khoản đích, `ImpersonatorUserId` là id admin (`IsImpersonated = true`); dùng để ghi nhận thao tác
  do admin làm hộ (xem `docs/features/auth.md`).
- CORS: chỉ origin trong `Cors:AllowedOrigins` (`appsettings.json`) được gọi API, có `AllowCredentials` để
  cookie `sl_refresh`/`sl_cart` đi kèm (FE gọi `fetch` với `credentials: "include"`). Origin ghi dạng
  `scheme://host[:port]`. Server có thể ghi đè bằng biến môi trường `Cors__AllowedOrigins__0`, `__1`, ...

## Thêm module mới

1. Tạo `src/Modules/<Ten>/SachLuat.Modules.<Ten>/SachLuat.Modules.<Ten>.csproj` theo mẫu module có sẵn
   (FrameworkReference `Microsoft.AspNetCore.App`, reference SharedKernel, `InternalsVisibleTo` hai project test).
2. Tạo `<Ten>Module : IModule`: đăng ký `DbContext` (schema riêng, `AuditableEntityInterceptor`), service,
   `AddValidatorsFromAssembly(..., includeInternalTypes: true)`, health check; `MigrateAsync` áp migration.
3. Thêm reference vào `SachLuat.Api.csproj` và `new <Ten>Module()` vào `builder.AddModules(...)` trong `Program.cs`.
4. `dotnet sln SachLuat.slnx add <đường dẫn csproj> --solution-folder src/Modules/<Ten>`.
5. Thêm module vào `ModuleModelTests` để test phát hiện thiếu migration.
6. Thêm dòng `COPY` file `.csproj` của module (và `.Contracts` nếu có) vào `Dockerfile` trước bước
   `dotnet restore`; thiếu thì build image lỗi `NETSDK1004: Assets file ... not found`.

## Migration

Mỗi module tạo migration cho `DbContext` của chính nó (`<Ten>` = Auth, Catalog, Inventory, Ordering, Content, Shipping, Promotion):

```bash
dotnet ef migrations add <TenMigration> \
  --project src/Modules/<Ten>/SachLuat.Modules.<Ten> \
  --startup-project src/SachLuat.Api \
  --context <Ten>DbContext \
  --output-dir Infrastructure/Persistence/Migrations

dotnet ef database update \
  --project src/Modules/<Ten>/SachLuat.Modules.<Ten> \
  --startup-project src/SachLuat.Api \
  --context <Ten>DbContext
```

- `Database:ApplyMigrationsOnStartup` = true ở `appsettings.Development.json` (tự áp khi chạy local),
  **false** ở `appsettings.json`: production áp migration bằng bước deploy riêng.
- Sau `migrations add` mà chạy `migrations script --no-build` thì phải build lại trước, nếu không script thiếu
  migration mới.

## Xử lý lỗi (API)

Mọi lỗi trả `application/problem+json` (RFC 9457), có thêm `code` và `traceId`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Sách đã hết hàng hoặc không đủ số lượng",
  "status": 409,
  "code": "OUT_OF_STOCK",
  "traceId": "00-..."
}
```

- Lỗi nghiệp vụ: `throw new BusinessException("OUT_OF_STOCK", "<thông báo cho khách>", 409);` (mặc định 422).
- Lỗi không lường trước: trả 500, `code = INTERNAL_ERROR`, không lộ message/stack trace; chi tiết nằm trong log.
- 401, 403, 404, 405, 429, lỗi validation: `code` suy ra từ mã HTTP (`ErrorCodes.FromStatusCode`).

## Kiểm tra trước khi báo xong

```bash
dotnet build SachLuat.slnx
dotnet test --solution SachLuat.slnx
dotnet format SachLuat.slnx --verify-no-changes
```

Test cần dịch vụ thật chỉ chạy khi có biến môi trường, nếu không sẽ bị skip:

| Biến | Dùng cho | Ví dụ local |
| --- | --- | --- |
| `SACHLUAT_TEST_DB` | Test persistence Catalog (database trong biến) và end-to-end (database `sachluat_e2e`, bị xóa và tạo lại mỗi lần chạy) | `Host=localhost;Port=5433;Database=sachluat_test;Username=sachluat;Password=<mật khẩu>` |
| `SACHLUAT_TEST_REDIS` | End-to-end (giỏ hàng) | `localhost:6380` |

```bash
export SACHLUAT_TEST_DB="Host=localhost;Port=5433;Database=sachluat_test;Username=sachluat;Password=<mật khẩu>"
export SACHLUAT_TEST_REDIS="localhost:6380"
dotnet test --solution SachLuat.slnx
```

Test end-to-end (`tests/SachLuat.IntegrationTests/EndToEnd`) chạy trọn luồng: Super Admin bật 2FA, tạo thủ kho,
nhập Catalog, phiếu nhập được duyệt, khách đặt hàng, đóng gói trừ kho FIFO, hủy đơn hoàn kho, tự hủy đơn quá hạn.

## CI

`.github/workflows/ci.yml` chạy trên mỗi push lên `main` và mỗi pull request: restore → kiểm tra format →
build Release với `-warnaserror` → toàn bộ test, có PostgreSQL 16 và Redis 7 dạng service container.
Job `docker-build` chạy song song, build image từ `Dockerfile` (không push) để bắt lỗi như quên `COPY` csproj
của module mới.

## Đổi mật khẩu PostgreSQL local

`POSTGRES_PASSWORD` trong `.env` chỉ có tác dụng **lần đầu** container khởi tạo dữ liệu. Đổi sau đó thì:

- Giữ dữ liệu: đổi mật khẩu trong DB cho khớp `.env`
  (`docker compose exec postgres psql -U sachluat -d sachluat` rồi `ALTER USER sachluat PASSWORD '...';`).
- Hoặc xóa sạch dữ liệu và khởi tạo lại: `docker compose down -v && docker compose up -d`.
- Sau đó cập nhật lại `user-secrets`. Dấu hiệu lệch mật khẩu: `28P01: password authentication failed`.

## Hạn chế / việc còn lại

- Chưa có hàng đợi/worker riêng (Hangfire hoặc tương đương), Meilisearch, Payment, Report. Module Notification
  mới có tin nhắn nội bộ qua Zalo Bot (`docs/features/staff-notification.md`), chưa có email/ZNS gửi khách.
- Chưa có nhật ký thao tác hệ thống (FR-98).
- Container PostgreSQL local hiện không có database `postgres` mặc định (image chuẩn luôn có). Test end-to-end
  đã tránh phụ thuộc (dùng `template1`); các công cụ khác cần tạo database mới có thể báo
  `3D000: database "postgres" does not exist`, khi đó tạo lại bằng `CREATE DATABASE postgres;`.
