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

- `created_at`/`updated_at` (UTC) do `AuditableEntityInterceptor` tự gán khi `SaveChanges`.
- Xóa mềm (`deleted_at`) cho `categories`, `authors`, `legal_documents`: gọi `Remove` sẽ chỉ gán `deleted_at`,
  query filter tự ẩn các dòng đã xóa (`IgnoreQueryFilters()` để xem lại).
- Đầu sách, ấn bản, SKU **không xóa mềm**: ẩn bằng trạng thái (`hidden`, `discontinued`) vì đơn hàng cũ vẫn
  tham chiếu SKU.

## API (method, route, request, response, mã lỗi)

Chưa có. Bước tiếp theo: API storefront `GET /api/v1/books/{slug}`, `GET /api/v1/categories` và API admin
CRUD (cần chốt thư viện validate và phân quyền).

## Cấu hình cần thiết

- `ConnectionStrings:Default` (user-secrets ở local), xem `docs/setup.md`.

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

Enum lưu dạng chuỗi snake_case (`on_sale`, `joint_circular`). Đổi tên member enum = đổi dữ liệu,
phải có migration cập nhật giá trị cũ.

## Hạn chế / việc còn lại

- Chưa có API, validator đầu vào và phân quyền Admin.
- Chưa có concurrency token cho chỉnh sửa đồng thời trên Admin.
- Thứ tự tác giả trong một đầu sách chưa lưu (`book_authors` chưa có `sort_order`).
- Tag (FR-61) chưa có bảng.
- Tìm kiếm không dấu (FR-11) sẽ dùng Meilisearch, chưa làm.
- Mã lỗi `ISBN_DUPLICATE` từ unique index (khác đầu sách) cần map `DbUpdateException` → 409 khi làm API.
