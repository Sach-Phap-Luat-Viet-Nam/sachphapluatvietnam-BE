# Sách Luật Việt Nam Backend

Backend của hệ thống thương mại điện tử bán trực tiếp sách pháp luật do Sách Luật Việt Nam biên soạn và phát hành. Dự án sử dụng ASP.NET Core .NET 10, kiến trúc modular monolith, PostgreSQL 16, Redis 7, EF Core và các module tách theo nghiệp vụ.

## Tổng quan

- Dự án: Website bán trực tiếp sách pháp luật Việt Nam
- Mã dự án: `SLVN-WEB`
- Backend: `.NET 10`, `ASP.NET Core`, `EF Core`, `PostgreSQL`, `Redis`
- Kiến trúc: `Modular Monolith`
- Mục tiêu: quản lý catalog, tồn kho, giỏ hàng, đơn hàng, auth, nội dung và admin APIs
- Tài liệu đặc tả: [docs/project_overview.md](docs/project_overview.md)
- Hướng dẫn setup: [docs/setup.md](docs/setup.md)

## Công nghệ sử dụng

- .NET 10 / ASP.NET Core
- PostgreSQL 16
- Redis 7
- EF Core + Npgsql
- FluentValidation
- Identity + JWT + 2FA
- xUnit v3 + NSubstitute + AwesomeAssertions
- Docker Compose cho môi trường local

## Cấu trúc repo

```text
.
├── .github/                 # CI/CD workflow
├── docs/                    # Tài liệu dự án và đặc tả tính năng
│   ├── project_overview.md
│   ├── setup.md
│   └── features/
├── src/
│   ├── SachLuat.Api/        # Host, security, error handling, DI
│   ├── SachLuat.SharedKernel/
│   └── Modules/
│       ├── Auth/
│       ├── Catalog/
│       ├── Inventory/
│       ├── Cart/
│       ├── Ordering/
│       └── Content/
├── tests/
│   ├── SachLuat.UnitTests/
│   └── SachLuat.IntegrationTests/
├── docker-compose.yml
├── Dockerfile
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── SachLuat.slnx
├── .env.example
├── README.md
└── docs/
```

## Module hiện có

- `Auth`: xác thực, JWT, refresh token, 2FA, quyền và policy
- `Catalog`: đầu sách, ấn bản, SKU, giá, danh mục
- `Inventory`: tồn kho, phiếu nhập, lô in
- `Cart`: giỏ hàng lưu trên Redis
- `Ordering`: đơn hàng, trạng thái, mã đơn, thanh toán
- `Content`: popup quảng cáo / nội dung hiển thị

## Yêu cầu môi trường

- .NET SDK 10.x
- Docker + Docker Compose
- PostgreSQL 16 (qua Docker Compose)
- Redis 7 (qua Docker Compose)
- IDE: VS Code, Rider hoặc Visual Studio

## Khởi chạy nhanh

### 1) Khôi phục tool và restore

```bash
dotnet tool restore
dotnet restore
```

### 2) Chạy PostgreSQL và Redis bằng Docker

```bash
docker compose up -d
```

Mặc định:

- PostgreSQL: `localhost:5433`
- Redis: `localhost:6380`

### 3) Cấu hình secrets

Dùng `dotnet user-secrets` cho môi trường local để tránh commit dữ liệu nhạy cảm:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=sachluat;Username=sachluat;Password=<password>" --project src/SachLuat.Api
dotnet user-secrets set "Jwt:SigningKey" "<chuoi-random-khong-commit>" --project src/SachLuat.Api

dotnet user-secrets set "Auth:BootstrapAdmin:Email" "<email>" --project src/SachLuat.Api
dotnet user-secrets set "Auth:BootstrapAdmin:Password" "<mat-khau>=8" --project src/SachLuat.Api
```

> Không commit `appsettings.Production.json`, `.env`, hay bất kỳ secret nào vào Git.

### 4) Chạy API

```bash
dotnet run --project src/SachLuat.Api
```

Sau khi chạy, API sẽ sẵn sàng ở:

- Swagger / Scalar: `http://localhost:5279/scalar` (Development)
- OpenAPI: `http://localhost:5279/openapi/v1.json`
- Health: `/health/live`, `/health/ready`

## Lệnh hữu ích

```bash
# Build solution
 dotnet build SachLuat.slnx

# Chạy test
 dotnet test --solution SachLuat.slnx

# Format code theo editorconfig
 dotnet format SachLuat.slnx

# Kiểm tra format không có thay đổi
 dotnet format SachLuat.slnx --verify-no-changes
```

## Quy ước kiến trúc

- Mỗi module là một project riêng, không phụ thuộc trực tiếp module khác.
- Mỗi module có `DbContext` và schema riêng.
- Tên bảng/cột dùng `snake_case`.
- API route theo tiền tố `api/v1` và `api/v1/admin`.
- Validation sử dụng `FluentValidation`.
- JWT + refresh token cookie HttpOnly; Admin APIs có policy và bắt buộc 2FA.
- Giỏ hàng lưu trong Redis.
- Error response theo chuẩn `ProblemDetails` kèm `code`.

## Test

Test được chia thành:

- `SachLuat.UnitTests`: kiểm thử logic nghiệp vụ, domain model, service
- `SachLuat.IntegrationTests`: kiểm thử tích hợp với PostgreSQL/Redis thật (nếu biến môi trường được cấu hình)

Một số test tích hợp cần biến môi trường:

- `SACHLUAT_TEST_DB`
- `SACHLUAT_TEST_REDIS`

Nếu không có môi trường test thì test sẽ được skip theo quy ước của dự án.

## Tài liệu tham khảo

- [docs/project_overview.md](docs/project_overview.md) — đặc tả tổng quát dự án
- [docs/setup.md](docs/setup.md) — hướng dẫn cài đặt và cấu trúc module
- [docs/features/auth.md](docs/features/auth.md)
- [docs/features/catalog.md](docs/features/catalog.md)
- [docs/features/inventory.md](docs/features/inventory.md)
- [docs/features/cart.md](docs/features/cart.md)
- [docs/features/ordering.md](docs/features/ordering.md)
- [docs/features/popup.md](docs/features/popup.md)

## Ghi chú

- Không commit secret hoặc file môi trường thật.
- Luôn chạy `dotnet format` trước khi đóng task nếu có thay đổi code.
- Mọi tính năng hoặc thay đổi API nên cập nhật tài liệu tương ứng trong `docs/features`.

## License

Repository hiện tại chưa ghi rõ giấy phép. Nếu cần dùng cho môi trường production hoặc phân phối người khác, cần xác nhận rõ quy định cấp phép trước khi phát hành.

