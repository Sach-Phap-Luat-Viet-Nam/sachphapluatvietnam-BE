# Catalog: đầu sách, ấn bản, SKU

## Mục đích

Quản lý dữ liệu sản phẩm theo mô hình **Đầu sách → Ấn bản → SKU** (mục 10 của `project_overview.md`),
cùng danh mục, tác giả và văn bản pháp luật mà ấn bản cập nhật. Đây là nền dữ liệu cho trang chi tiết sách,
danh mục, tìm kiếm và cho module Order/Inventory tham chiếu SKU.

Tồn kho, lô in và thẻ kho **không** thuộc Catalog mà thuộc module Inventory (chưa làm), tham chiếu `skus.id`.

## Luồng xử lý

- `Book` là aggregate root, sở hữu `Edition`; `Edition` sở hữu `Sku`. Tạo ấn bản qua `Book.AddEdition`,
  tạo SKU qua `Edition.AddSku`.
- `Category`, `Author`, `LegalDocument` là aggregate riêng, gắn vào sách/ấn bản qua bảng nối.
- Quy tắc nghiệp vụ (vi phạm thì ném `BusinessException`, API trả `ProblemDetails` với `code` tương ứng):

| Quy tắc | Nơi kiểm tra | Mã lỗi |
| --- | --- | --- |
| BR-02: mỗi đầu sách tối đa 1 ấn bản mới nhất | `Book.SetLatestEdition` + partial unique index `ux_editions_book_id_latest` | `EDITION_NOT_FOUND` (ấn bản không thuộc sách) |
| BR-04: ấn bản phải có ISBN và số QĐXB | `Edition.UpdatePublishingInfo`, cột NOT NULL | `VALIDATION_ERROR` |
| ISBN-13 hợp lệ (978/979, đúng số kiểm tra), lưu dạng 13 chữ số | `Isbn13.TryNormalize`, check constraint | `ISBN_INVALID` |
| ISBN duy nhất | Trong sách: domain; toàn hệ thống: unique index | `ISBN_DUPLICATE` |
| Slug: chữ thường không dấu, số, gạch nối | `Slug.IsValid` | `SLUG_INVALID` |
| Mã SKU: chữ in hoa, số, gạch nối, ≤ 50 ký tự; duy nhất | `Sku`, unique index | `SKU_CODE_INVALID` |
| Mỗi ấn bản một SKU cho mỗi loại bìa | `Edition.AddSku`, unique index | `SKU_COVER_DUPLICATE` |
| Giá: số nguyên VNĐ > 0, giá bán ≤ giá bìa | `Sku.ChangePrices`, check constraint | `PRICE_INVALID` |
| Danh mục không được làm cha của chính nó/con cháu | `Category.MoveTo` | `CATEGORY_PARENT_INVALID` |
| Lịch giá: giá thấp hơn giá bán thường, kết thúc sau bắt đầu và ở tương lai | `Sku.AddPriceSchedule`, check constraint | `PRICE_INVALID`, `VALIDATION_ERROR` |
| Lịch giá của một SKU không chồng thời gian | `Sku` + khóa dòng `skus` (`FOR UPDATE`) | `PRICE_SCHEDULE_OVERLAP` |
| Lịch giá đã kết thúc không sửa/xóa (giữ lịch sử giá) | `Sku.UpdatePriceSchedule`, `RemovePriceSchedule` | `PRICE_SCHEDULE_ENDED` |

- `created_at`/`updated_at` (UTC) do `AuditableEntityInterceptor` tự gán khi `SaveChanges`.
- Xóa mềm (`deleted_at`) cho `categories`, `authors`, `legal_documents`: gọi `Remove` sẽ chỉ gán `deleted_at`,
  query filter tự ẩn các dòng đã xóa (`IgnoreQueryFilters()` để xem lại).
- Đầu sách, ấn bản, SKU **không xóa mềm**: ẩn bằng trạng thái (`hidden`, `discontinued`) vì đơn hàng cũ vẫn
  tham chiếu SKU.

**Lịch giá (giảm giá có thời hạn, FR-63)**

- Mỗi SKU có giá bìa `listPrice`, giá bán thường `salePrice` và các lịch giá `[startsAt, endsAt)` (UTC).
- **Giá hiện hành** = giá của lịch đang chạy nếu thấp hơn giá bán thường, không thì giá bán thường
  (`Sku.CurrentPriceAt`). Vì luôn lấy giá thấp hơn nên hạ giá thường sau khi đặt lịch vẫn an toàn và giá hiện hành
  không bao giờ vượt giá bìa.
- Giá hiện hành dùng ở mọi nơi khách thấy và trả tiền: danh sách/chi tiết sách (kể cả lọc `minPrice`/`maxPrice`
  và sắp xếp theo giá), `ICatalogSkuReader` (giỏ hàng và đơn hàng lấy giá từ đây, đơn chụp giá lúc đặt).
- Không cần job nền: giá tính theo thời điểm mỗi request, lịch tự bắt đầu/kết thúc đúng giờ.
- Một thời điểm tối đa một lịch cho mỗi SKU; hai lịch nối tiếp (`endsAt` của lịch trước = `startsAt` lịch sau)
  hợp lệ. Xóa lịch đang chạy thì dừng giảm giá ngay.

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1`. Enum trong JSON và query string dạng snake_case (`on_sale`, `paperback`).

**Website (không cần đăng nhập)** — chỉ đầu sách `active` có ấn bản mới nhất đã công bố:

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/books?q=&category=&type=&audience=&cover=&minPrice=&maxPrice=&onSale=&legalDocument=&sort=&page=&limit=` | Danh sách (FR-12, FR-13, FR-14); `sort`: `newest`, `price_asc`, `price_desc`; `onSale=true`: chỉ sách có SKU đang bán thấp hơn giá bìa; `limit` mặc định 24, tối đa 100 |
| GET | `/books/{slug}` | Chi tiết + ấn bản (mới nhất trước, `isLatest` cho cảnh báo BR-03) + SKU (`inStock`, `discountPercent`) + văn bản pháp luật |
| GET | `/categories` | Cây danh mục |

Danh sách trả `{items, meta: {page, limit, total}}`; mỗi item lấy giá SKU rẻ nhất của ấn bản mới nhất, kèm `skuCode` của đúng SKU đó
(`null` khi sách chưa có ấn bản mới nhất hoặc chưa có SKU).

Giá trong response (danh sách, chi tiết, và `AdminBookResponse`):

| Field | Ý nghĩa |
| --- | --- |
| `listPrice` | Giá bìa |
| `salePrice` | Giá khách trả **lúc này** (đã áp lịch giá) |
| `regularSalePrice` | Giá bán thường (chỉ ở SKU). Admin dùng field này điền form `PUT /admin/skus/{id}` |
| `discountPercent` | % giảm của `salePrice` so với `listPrice`, làm tròn xuống |
| `saleEndsAt` | Thời điểm hết giá lịch (UTC), để hiện đếm ngược; `null` khi đang bán giá thường |
`category` gồm cả danh mục con; `legalDocument` khớp văn bản gắn với ấn bản hoặc nhãn "Cập nhật đến".

**Admin** (policy `catalog_admin`: super_admin, product_manager, bắt buộc 2FA):

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/admin/books?q=&status=` | Danh sách mọi trạng thái |
| GET/PUT | `/admin/books/{id}` | Chi tiết (mọi ấn bản, tồn từng SKU) / sửa |
| POST | `/admin/books` | Tạo: `{title, slug, bookType, status, description?, targetAudiences[], metaTitle?, metaDescription?, categoryIds[], authorIds[]}` |
| POST | `/admin/books/{id}/editions` | Thêm ấn bản; ấn bản đầu tiên tự là mới nhất |
| PUT | `/admin/editions/{id}` | Sửa ấn bản (ISBN, QĐXB, "cập nhật đến", trạng thái, `legalDocumentIds[]`, ...) |
| POST | `/admin/editions/{id}/set-latest` | Đổi ấn bản mới nhất (BR-02) |
| POST | `/admin/editions/{id}/skus` | `{skuCode, coverType, listPrice, salePrice, allowPreorder}` |
| PUT | `/admin/skus/{id}` | `{listPrice, salePrice, allowPreorder}` (`salePrice` là giá bán thường) |
| GET/POST/PUT/DELETE | `/admin/categories`, `/admin/authors`, `/admin/legal-documents` | CRUD, xóa là xóa mềm |
| POST | `/admin/uploads/cover-signature` | Chữ ký upload ảnh bìa lên Cloudinary (xem mục Ảnh bìa) |

Các API sửa đầu sách/ấn bản/SKU trả lại toàn bộ đầu sách (`AdminBookResponse`).

**Lịch giá** (policy `pricing_admin`: super_admin, product_manager, marketing, bắt buộc 2FA):

| Method | Route | Request → Response |
| --- | --- | --- |
| GET | `/admin/skus/{skuId}/price-schedules` | → `PriceScheduleResponse[]`, mới nhất trước, gồm cả lịch đã kết thúc |
| POST | `/admin/skus/{skuId}/price-schedules` | `UpsertPriceScheduleRequest` → 201 `PriceScheduleResponse` |
| PUT | `/admin/price-schedules/{id}` | `UpsertPriceScheduleRequest` → `PriceScheduleResponse` |
| DELETE | `/admin/price-schedules/{id}` | → 204 |

```json
{
  "salePrice": 150000,
  "startsAt": "2026-11-01T00:00:00+07:00",
  "endsAt": "2026-11-08T00:00:00+07:00",
  "note": "Sale mùa thi"
}
```

`PriceScheduleResponse`: `{id, skuId, salePrice, discountPercent, startsAt, endsAt, note, status, createdAt,
updatedAt}`, `status` là `scheduled` | `running` | `ended` (tính lúc trả về). `note` ≤ 255 ký tự, chỉ hiện ở Admin.
Mã lỗi thêm: `UPLOAD_NOT_CONFIGURED` (503, chưa cấu hình Cloudinary), `SLUG_DUPLICATE` (409), `SKU_CODE_DUPLICATE` (409), `CATEGORY_HAS_CHILDREN` (409),
`REFERENCE_NOT_FOUND` (422, id danh mục/tác giả/văn bản không tồn tại), `NOT_FOUND` (404),
`PRICE_SCHEDULE_OVERLAP` (409), `PRICE_SCHEDULE_ENDED` (422).

**Ảnh bìa (Cloudinary, upload có ký)**: file không đi qua API.

1. Admin FE gọi `POST /admin/uploads/cover-signature` → `{uploadUrl, apiKey, timestamp, folder, allowedFormats,
   signature}`. Chữ ký chỉ có hiệu lực 1 giờ (giới hạn của Cloudinary).
2. FE gửi `multipart/form-data` tới `uploadUrl` với field `file` cùng **đúng** `api_key`, `timestamp`, `folder`,
   `allowed_formats`, `signature` (thêm/bớt tham số thì chữ ký sai). Cloudinary trả `secure_url`.
3. FE lưu `secure_url` vào `coverImageUrl` qua `PUT /admin/editions/{id}`. Khi đã cấu hình Cloudinary, link phải
   bắt đầu bằng `https://res.cloudinary.com/{CloudName}/image/upload/`, nếu không trả 400 `VALIDATION_ERROR`.
4. Khi hiển thị, FE nên chèn `f_auto,q_auto` sau `/image/upload/` để Cloudinary tự nén.

**Cho module khác**: `ICatalogSkuReader` (project `SachLuat.Modules.Catalog.Contracts`) trả snapshot SKU
(tên sách, ấn bản, ISBN, giá, `isSellable` = đầu sách active và ấn bản `on_sale`/`clearance`).
`SalePrice` trong snapshot là **giá hiện hành** (đã áp lịch giá).

## Cấu hình cần thiết

- `ConnectionStrings:Default` (user-secrets ở local), xem `docs/setup.md`.
- `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret` (**bí mật**): lấy ở Cloudinary Console.
  Local dùng user-secrets, Render dùng `Cloudinary__CloudName`, ... Thiếu thì API ký trả 503 và không kiểm tra link.
- `Cloudinary:CoverFolder`: mặc định `sachluat/covers`.
- Tài khoản Cloudinary dùng chung với module Content (ảnh popup, xem `docs/features/popup.md`); code ký nằm ở
  `SachLuat.SharedKernel.Media`.

## Thay đổi CSDL (migration liên quan)

Migration `CreateCatalogSchema`, schema `catalog`:

| Bảng | Ghi chú |
| --- | --- |
| `books` | `book_type`, `status` dạng chuỗi; `target_audiences` là mảng `varchar(30)[]` |
| `editions` | `isbn char(13)` unique; thêm `cover_image_url`, `table_of_contents` so với tài liệu gốc |
| `skus` | `list_price`, `sale_price numeric(12,0)`; không có cột tồn kho |
| `categories` | cây qua `parent_id`; `sort_order`; slug unique khi chưa xóa |
| `authors` | `biography`, `sort_order`; slug unique khi chưa xóa |
| `legal_documents` | index `doc_number` để tìm theo số hiệu |
| `book_categories`, `book_authors`, `edition_legal_documents` | bảng nối, khóa chính ghép |

Migration `AddSkuPriceSchedules`: bảng `sku_price_schedules` (`sku_id` → `skus` cascade, `sale_price numeric(12,0)`,
`starts_at`, `ends_at`, `note`), check `sale_price > 0` và `ends_at > starts_at`, index `(sku_id, ends_at)`.
Chỉ thêm bảng, không đổi dữ liệu cũ.

Enum lưu dạng chuỗi snake_case (`on_sale`, `joint_circular`). Đổi tên member enum = đổi dữ liệu,
phải có migration cập nhật giá trị cũ.

## Hạn chế / việc còn lại

- Tìm kiếm tạm dùng `ILIKE` theo tên sách/ISBN, chưa tìm không dấu, theo tác giả, gợi ý (FR-11): chờ Meilisearch.
- Ảnh bìa đã upload được qua Cloudinary; chưa xóa ảnh cũ trên Cloudinary khi đổi ảnh (ảnh mồ côi).
- Chưa có upload file đọc thử (chỉ lưu URL), watermark đọc thử (NFR-16).
- Chưa có concurrency token cho chỉnh sửa đồng thời trên Admin.
- Thứ tự tác giả trong một đầu sách chưa lưu (`book_authors` chưa có `sort_order`).
- Tag (FR-61), combo (FR-16), giá sỉ theo bậc (FR-63, chờ tài khoản tổ chức FR-09), import Excel (FR-65) chưa làm.
- Lọc "còn hàng", sắp xếp "bán chạy", "đánh giá cao" (FR-12, FR-13) chưa làm.
- Lịch giá đặt từng SKU, chưa có API đặt hàng loạt cho nhiều SKU một lần.
- Chưa ghi nhật ký thay đổi giá ai/lúc nào/cũ → mới (FR-98); hiện chỉ ghi log ứng dụng.
- Giỏ hàng hiển thị giá hiện hành tại lúc xem; lịch giá kết thúc giữa chừng thì đơn tính theo giá lúc đặt.
