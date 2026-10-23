# Cài đặt môi trường và cấu trúc dự án

## Mục đích

Hướng dẫn dựng môi trường local cho backend Sách Luật Việt Nam và giải thích cấu trúc
modular monolith để thêm module mới đúng quy ước.

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

# 2. Khởi động PostgreSQL
cp .env.example .env          # rồi đổi POSTGRES_PASSWORD trong .env
docker compose up -d

# 3. Khai báo connection string (lưu ngoài repo, không commit)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=sachluat;Username=sachluat;Password=<mật khẩu trong .env>" \
  --project src/SachLuat.Api

# 4. Chạy API
dotnet run --project src/SachLuat.Api
```

Sau khi chạy:

- API reference (Scalar): <http://localhost:5279/scalar> (chỉ ở môi trường Development)
- OpenAPI JSON: <http://localhost:5279/openapi/v1.json>
- Health: `/health/live` (tiến trình còn sống), `/health/ready` (đã kết nối được CSDL)

Nếu thiếu connection string, API dừng ngay khi khởi động với lỗi
`Connection string 'Default' is not configured`.

### Docker Engine trong WSL (không dùng Docker Desktop)

- Chạy compose từ Windows: `wsl -d Ubuntu --cd "<đường dẫn repo dạng /mnt/c/...>" -- docker compose up -d`,
  hoặc mở terminal Ubuntu, `cd` vào repo rồi chạy `docker compose up -d`.
- **WSL tự tắt distro khi không còn tiến trình `wsl` nào mở**, kéo theo Docker và PostgreSQL tắt theo.
  Dấu hiệu: `/health/ready` trả 503, Npgsql báo `actively refused`, `wsl -l -v` hiện `Stopped`.
  Cách xử lý: giữ một terminal Ubuntu mở trong lúc làm việc. Container có `restart: unless-stopped`
  nên tự chạy lại khi Docker khởi động.
- Sau khi container vừa chạy, `wslrelay` cần vài giây mới chuyển tiếp cổng sang `localhost` của Windows.
- Cổng mặc định là **5433** (không phải 5432) để tránh trùng với PostgreSQL của dự án khác trên cùng máy.

## Cấu trúc solution

```text
SachLuat.slnx
Directory.Build.props        # net10.0, Nullable, analyzer cho mọi project
Directory.Packages.props     # version package tập trung (Central Package Management)
global.json                  # bật Microsoft.Testing.Platform cho dotnet test
docker-compose.yml           # PostgreSQL 16 cho local
src/
  SachLuat.Api/              # Host: Program.cs, middleware, xử lý lỗi, đăng ký module
  SachLuat.SharedKernel/     # Dùng chung: IModule, BusinessException, ErrorCodes, ApiRoutes
  Modules/
    Catalog/
      SachLuat.Modules.Catalog/
        CatalogModule.cs     # Điểm đăng ký DI của module
        Domain/              # Entity, value object
        Application/         # Use case, DTO, validator
        Infrastructure/
          Persistence/       # CatalogDbContext, cấu hình entity, Migrations/
        Controllers/         # Controller của module (public)
tests/
  SachLuat.UnitTests/
  SachLuat.IntegrationTests/ # WebApplicationFactory<Program>
```

### Quy tắc ranh giới module

- Mỗi module là **một project**. Mọi thứ để `internal`, trừ class `XxxModule` và controller.
- Mỗi module sở hữu **một schema PostgreSQL riêng** (`catalog`, `ordering`, ...) và một `DbContext` riêng,
  có bảng `__EFMigrationsHistory` riêng trong schema đó.
- Tên bảng/cột tự chuyển sang `snake_case` (package `EFCore.NamingConventions`) để khớp thiết kế dữ liệu
  ở mục 10 của `project_overview.md`.
- Module **không** reference trực tiếp project của module khác. Khi cần giao tiếp, tạo project
  `SachLuat.Modules.<Ten>.Contracts` chứa interface/DTO công khai.

## Thêm module mới

1. Tạo `src/Modules/<Ten>/SachLuat.Modules.<Ten>/SachLuat.Modules.<Ten>.csproj`, copy cấu trúc csproj
   của Catalog (FrameworkReference `Microsoft.AspNetCore.App`, reference SharedKernel, package EF Core).
2. Tạo `<Ten>Module : IModule` đăng ký `DbContext` (schema riêng) và service của module.
3. Thêm reference vào `SachLuat.Api.csproj` và thêm `new <Ten>Module()` vào `builder.AddModules(...)`
   trong `Program.cs`.
4. `dotnet sln SachLuat.slnx add <đường dẫn csproj>`.

## Migration

Mỗi module tạo migration cho `DbContext` của chính nó:

```bash
dotnet ef migrations add <TenMigration> \
  --project src/Modules/Catalog/SachLuat.Modules.Catalog \
  --startup-project src/SachLuat.Api \
  --context CatalogDbContext \
  --output-dir Infrastructure/Persistence/Migrations

dotnet ef database update \
  --project src/Modules/Catalog/SachLuat.Modules.Catalog \
  --startup-project src/SachLuat.Api \
  --context CatalogDbContext
```

## Xử lý lỗi (API)

Mọi lỗi trả `application/problem+json` (RFC 9457), có thêm `code` và `traceId`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Sách đã hết hàng hoặc không đủ số lượng",
  "status": 409,
  "code": "OUT_OF_STOCK",
  "traceId": "0HN..."
}
```

- Lỗi nghiệp vụ: `throw new BusinessException("OUT_OF_STOCK", "<thông báo cho khách>", 409);`
  (mặc định 422).
- Lỗi không lường trước: trả 500, `code = INTERNAL_ERROR`, không lộ message/stack trace; chi tiết nằm trong log.
- 404 routing, 405, lỗi validation model: `code` suy ra từ mã HTTP (`ErrorCodes.FromStatusCode`).
- Route dùng hằng số `ApiRoutes.V1` (`api/v1`) và `ApiRoutes.AdminV1` (`api/v1/admin`).

## Kiểm tra trước khi báo xong

```bash
dotnet build SachLuat.slnx
dotnet test --solution SachLuat.slnx
dotnet format SachLuat.slnx --verify-no-changes
```

Test chạm CSDL thật (`tests/SachLuat.IntegrationTests/Catalog`) chỉ chạy khi có biến môi trường
`SACHLUAT_TEST_DB`, nếu không sẽ bị skip. Dùng database riêng, test tự chạy migration và tự tạo database:

```bash
export SACHLUAT_TEST_DB="Host=localhost;Port=5433;Database=sachluat_test;Username=sachluat;Password=<mật khẩu>"
dotnet test --solution SachLuat.slnx
```

## Đổi mật khẩu PostgreSQL local

`POSTGRES_PASSWORD` trong `.env` chỉ có tác dụng **lần đầu** container khởi tạo dữ liệu. Đổi sau đó thì:

- Giữ dữ liệu: đổi mật khẩu trong DB cho khớp `.env`
  (`docker compose exec postgres psql -U sachluat -d postgres` rồi `ALTER USER sachluat PASSWORD '...';`).
- Hoặc xóa sạch dữ liệu và khởi tạo lại: `docker compose down -v && docker compose up -d`.
- Sau đó cập nhật lại `user-secrets` (bước 3 ở trên). Dấu hiệu lệch mật khẩu: `28P01: password authentication failed`.

## Hạn chế / việc còn lại

- Chưa có entity và migration nào: cần chốt thiết kế bảng ở mục 10 trước.
- Mục 9 (công nghệ: NestJS, BullMQ) và 11.1 (định dạng lỗi) của `project_overview.md` chưa cập nhật theo
  quyết định dùng ASP.NET Core và `ProblemDetails`.
- Chưa chọn thư viện validate (FluentValidation hay DataAnnotations), xác thực JWT, hàng đợi nền
  cho worker, Redis, Meilisearch.
- Khóa ngoại xuyên module (ví dụ `order_items` → `skus`) cần quyết định: FK chéo schema hay chỉ lưu ID.
- Chưa có Dockerfile cho API và pipeline CI.
