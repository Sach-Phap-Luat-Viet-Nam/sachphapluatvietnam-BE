# CLAUDE.md

## 0. Tổng quan dự án

- **Dự án**: Website thương mại điện tử bán trực tiếp (D2C) sách pháp luật do Sách Luật Việt Nam
  tự biên soạn và in (mã `SLVN-WEB`). Đặc tả đầy đủ: `docs/project_overview.md`.
- **Backend**: ASP.NET Core **.NET 10 (LTS)**, C#, kiến trúc **modular monolith**
- **Kiểu API**: Controllers, tiền tố `api/v1` (`ApiRoutes.V1`), lỗi trả `ProblemDetails` + `code`
- **CSDL / ORM**: PostgreSQL 16 + EF Core (Npgsql), mỗi module một schema và một `DbContext`,
  tên bảng/cột `snake_case`, không khóa ngoại chéo schema. Giỏ hàng lưu Redis 7.
- **Module hiện có**: Auth (Identity + JWT + 2FA), Catalog, Inventory, Cart, Ordering, Content (popup),
  Shipping (vùng giao hàng, phí ship), Promotion (khuyến mãi, mã giảm giá),
  Notification (tin nhắn nội bộ qua Zalo Bot, chưa có CSDL)
  (mô tả từng module: `docs/features/*.md`)
- **Bảo mật**: JWT 15 phút + refresh token cookie HttpOnly; API Admin dùng `Policies.*`, bắt buộc 2FA
- **Validate**: FluentValidation (`FluentValidationFilter` tự chạy); phân trang `PagedQuery`
- **Test**: xUnit v3 (Microsoft.Testing.Platform) + NSubstitute + AwesomeAssertions
  (fork Apache-2.0 của FluentAssertions; FluentAssertions 8+ là license thương mại).
  Test cần dịch vụ thật dùng biến `SACHLUAT_TEST_DB`, `SACHLUAT_TEST_REDIS` (thiếu thì skip)
- **CI**: GitHub Actions `.github/workflows/ci.yml` (format, build `-warnaserror`, test kèm PostgreSQL + Redis,
  build Docker image)
- **Frontend**: Next.js (React, TypeScript) + Tailwind CSS, repo riêng (quy tắc FE ở mục 8)
- Hướng dẫn cài đặt, quy tắc module, thêm module mới, quy ước API: `docs/setup.md`

### Cấu trúc thư mục

```
src/
  SachLuat.Api/                 # Host: Program.cs, xử lý lỗi, bảo mật, đăng ký module
  SachLuat.SharedKernel/        # IModule, BusinessException, ErrorCodes, ApiRoutes, Roles/Policies,
                                # ICurrentUser, PagedQuery, AuditableEntity, SnakeCaseEnumConverter
  Modules/
    <Ten>/SachLuat.Modules.<Ten>.Contracts/   # Interface/DTO public cho module khác dùng
    <Ten>/SachLuat.Modules.<Ten>/
      <Ten>Module.cs            # Đăng ký DI, DbContext, MigrateAsync của module
      Domain/                   # Entity, value object
      Application/              # Use case, DTO, validator
      Infrastructure/Persistence/ # DbContext, cấu hình entity, Migrations/
      Controllers/
tests/
  SachLuat.UnitTests/
  SachLuat.IntegrationTests/
docs/
  setup.md
  features/                     # Mỗi tính năng một file .md (mục 7)
```

- Trong module, mọi thứ để `internal` (kể cả controller, DTO); chỉ `<Ten>Module` public. Module không
  reference trực tiếp module khác, chỉ reference `SachLuat.Modules.<Ten>.Contracts`.

### Lệnh thường dùng

```bash
dotnet tool restore
docker compose up -d              # PostgreSQL :5433 + Redis :6380 (cần file .env, xem .env.example)
dotnet build SachLuat.slnx
dotnet run --project src/SachLuat.Api
dotnet test --solution SachLuat.slnx
dotnet format SachLuat.slnx                     # format theo .editorconfig
dotnet format SachLuat.slnx --verify-no-changes # kiểm tra format (dùng trước khi báo xong)
dotnet ef migrations add <TenMigration> \
  --project src/Modules/<Ten>/SachLuat.Modules.<Ten> \
  --startup-project src/SachLuat.Api \
  --context <Ten>DbContext \
  --output-dir Infrastructure/Persistence/Migrations
```

### Định nghĩa "xong việc"

Một thay đổi chỉ được báo là xong khi:

1. `dotnet build` không lỗi, không thêm warning mới.
2. `dotnet test` pass toàn bộ.
3. `dotnet format --verify-no-changes` sạch.
4. Đã viết test cho logic mới và cập nhật `docs/` nếu là tính năng (mục 7).

---

## 1. Giao tiếp

- Gọi người dùng là **"Anh"**, tự xưng **"Em"**, lịch sự, tự nhiên.
- Câu trả lời đầu tiên của mỗi phiên: giới thiệu tên model **nếu biết chắc**;
  nếu không chắc thì nói rõ là không chắc, không tự đoán phiên bản.
- Trình bày ngắn gọn, dùng bullet và **in đậm** từ khóa quan trọng.
- Giữ nguyên văn tiếng Anh với error log, tên biến, tên API, tài liệu gốc.

## 2. Xử lý thông tin và ra quyết định

- **Không bịa**: không tự tạo class, method, package NuGet hay đường link
  không tồn tại. Không biết thì nói "Em không biết / Em chưa kiểm tra được".
- **Khi thiếu thông tin**, phân theo mức ảnh hưởng:
  - **Ảnh hưởng lớn hoặc khó quay lại** (kiến trúc, schema CSDL, xóa/ghi đè
    nhiều file, đổi contract API): dừng lại, hỏi dạng trắc nghiệm A/B/C,
    mỗi lựa chọn kèm ưu/nhược điểm và đề xuất của Em.
  - **Việc nhỏ, dễ sửa lại**: chọn phương án hợp lý nhất, **ghi rõ giả định**
    trong câu trả lời rồi làm tiếp.
- **Trước khi thực hiện** thay đổi kiến trúc, xóa/ghi đè nhiều file, hoặc chạy
  lệnh tác động CSDL: trình bày kế hoạch ngắn và chờ Anh đồng ý.
- **Debug**: đọc kỹ error log, tìm nguyên nhân gốc rễ trước, rồi mới sửa.
  Nêu nguyên nhân trong 1–3 câu trước khi đưa ra cách sửa.
- Với việc nhiều bước: liệt kê các bước logic dạng gạch đầu dòng trước khi code.

## 3. Quy chuẩn code C# / ASP.NET Core

### Định dạng

- Tuân theo `.editorconfig` của repo; chạy `dotnet format` sau khi sửa.
- Dòng tối đa **120 ký tự**; quá dài thì ngắt dòng theo tham số hoặc chuỗi
  method (fluent).
- Bật `<Nullable>enable</Nullable>`; không dùng `!` (null-forgiving) để lấp
  cảnh báo nếu chưa chắc giá trị khác null.
- Dùng file-scoped namespace (`namespace X.Y;`).

### Đặt tên

| Thành phần | Quy tắc | Ví dụ |
| --- | --- | --- |
| Class, record, struct, enum | PascalCase | `OrderService` |
| Interface | `I` + PascalCase | `IOrderRepository` |
| Method, property, event | PascalCase | `GetByIdAsync`, `TotalAmount` |
| Biến cục bộ, tham số | camelCase | `orderId` |
| Private field | `_camelCase` | `_dbContext` |
| Hằng số (`const`, `static readonly`) | PascalCase | `MaxPageSize` |
| Method bất đồng bộ | Hậu tố `Async` | `CreateOrderAsync` |
| DTO | Hậu tố rõ nghĩa | `CreateOrderRequest`, `OrderResponse` |

### Thiết kế

- Dùng **Dependency Injection** qua constructor; không `new` service thủ công,
  không dùng service locator.
- Controller/endpoint mỏng: chỉ nhận request, gọi service, trả response.
  Logic nghiệp vụ nằm ở tầng Application/Domain.
- Không trả entity EF Core ra ngoài API; luôn map sang DTO.
- Validate input bằng FluentValidation (validator `internal` trong module); trả lỗi theo
  chuẩn `ProblemDetails`.
- **Async xuyên suốt**: dùng `async/await` cho I/O; **không** dùng `.Result`,
  `.Wait()`, `async void` (trừ event handler). Truyền `CancellationToken`
  xuống các tầng dưới.
- Đường dẫn file: dùng `Path.Combine` cùng `IWebHostEnvironment.ContentRootPath`
  hoặc `AppContext.BaseDirectory`; **không** dùng đường dẫn tuyệt đối
  (`C:\...`).

### Comment

- Comment giải thích **tại sao** (thuật toán, edge case, quyết định đặc biệt)
  được phép để trong code, ngắn gọn.
- Dùng XML doc (`///`) cho public API của thư viện dùng chung.
- Mô tả **tính năng** (mục đích, cách dùng, luồng) không để trong code,
  mà viết vào `docs/` (mục 7).

### Khi trình bày code trong chat

- Chỉ đưa phần thay đổi, kèm tên file và vị trí. Không in lại cả file nếu chỉ
  sửa vài dòng, trừ khi Anh yêu cầu.
- (Khi sửa file trực tiếp trong repo thì không áp dụng quy tắc này.)

## 4. Logging và xử lý lỗi

- Dùng `ILogger<T>` qua DI; **không** dùng `Console.WriteLine` để log.
- Dùng structured logging với template, không nối chuỗi:
  `_logger.LogInformation("Created order {OrderId}", order.Id);`
- Chọn đúng mức: `Debug` (chi tiết khi dev), `Information` (sự kiện nghiệp vụ),
  `Warning` (bất thường nhưng chạy tiếp được), `Error` (lỗi cần xử lý).
- **Không log** password, token, số thẻ, dữ liệu cá nhân nhạy cảm.
- Bắt exception cụ thể (`DbUpdateConcurrencyException`, `HttpRequestException`,
  `ValidationException`, ...). Không nuốt lỗi bằng `catch { }` rỗng.
- Chỉ bắt `Exception` chung ở **tầng biên**: global exception handler
  (`IExceptionHandler` / middleware), background worker. Ở đó phải log kèm
  exception và trả `ProblemDetails`, không lộ stack trace ra client.

## 5. Cấu hình, bảo mật và package

- **Không hardcode** connection string, API key, password, token trong code
  hay `appsettings.json` được commit.
  - Local: dùng `dotnet user-secrets`.
  - Server: dùng biến môi trường hoặc secret manager.
  - Đọc cấu hình qua Options pattern (`IOptions<T>`), không đọc trực tiếp
    chuỗi key rải rác.
- Khi thêm key cấu hình mới: cập nhật `appsettings.Example.json` (giá trị giả)
  để người khác biết cần cấu hình gì.
- Thêm package bằng `dotnet add package <Ten>`; nêu rõ package mới và lý do.
  Ưu tiên package chính thức, đang được bảo trì.
- Truy vấn CSDL qua EF Core hoặc tham số hóa; **không** nối chuỗi SQL từ input.

## 6. CSDL, Git và vùng không được tự ý sửa

### EF Core migration

- Tạo migration mới cho mọi thay đổi schema; đặt tên rõ nghĩa
  (`AddOrderStatusColumn`).
- **Không sửa** migration đã được apply/merge; muốn đổi thì tạo migration mới.
- **Không tự chạy** `dotnet ef database update` lên môi trường khác local.

### Git

- **Không tự `commit` hoặc `push`** khi Anh chưa yêu cầu.
- Commit message theo Conventional Commits: `feat:`, `fix:`, `refactor:`,
  `docs:`, `test:`, `chore:`.
- Không `force push`, không sửa lịch sử nhánh `main`.

### Không tự ý sửa

- File secret và cấu hình môi trường thật (`appsettings.Production.json`,
  `.env`).
- Migration đã apply, thư mục `bin/`, `obj/`, file sinh tự động.
- Pipeline CI/CD, file `.csproj` của project khác ngoài phạm vi việc đang làm
  (trừ khi cần thêm package và đã nêu rõ).

## 7. Tài liệu (docs)

- **Khi nào viết**: mỗi tính năng mới, hoặc thay đổi làm khác hành vi/API của
  tính năng cũ. Bug fix nhỏ không cần file riêng.
- **Vị trí và tên file**: `docs/features/<ten-tinh-nang>.md` (kebab-case,
  ví dụ `docs/features/order-checkout.md`). Sửa tính năng cũ thì cập nhật file
  có sẵn, không tạo file mới.
- **Template**:

```markdown
# <Tên tính năng>

## Mục đích
## Luồng xử lý
## API (method, route, request, response, mã lỗi)
## Cấu hình cần thiết
## Thay đổi CSDL (migration liên quan)
## Hạn chế / việc còn lại
```

## 8. Frontend

- Stack: Next.js (React, TypeScript) + Tailwind CSS; Storefront render SSR (SEO), Admin là
  Next.js hoặc React SPA. Nằm ở repo riêng, không nằm trong repo backend này.
- Đặt tên: camelCase cho biến và hàm, PascalCase cho component.
- Gọi API qua `api/v1`, đọc lỗi từ field `code` của `ProblemDetails`.
- > Bổ sung lệnh chạy/test/lint khi đã dựng repo FE.
