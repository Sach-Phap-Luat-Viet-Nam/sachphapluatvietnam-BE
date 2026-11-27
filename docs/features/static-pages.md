# Trang tĩnh (Điều khoản, Chính sách...)

## Mục đích

Lưu nội dung các trang tĩnh của Storefront (FR-51) trong CSDL để Admin sửa được mà không cần deploy lại FE (FR-91).
Hiện có hai trang được seed sẵn: `dieu-khoan-su-dung`, `chinh-sach-bao-mat`.

## Luồng xử lý

- Storefront render trang `/dieu-khoan-su-dung`, `/chinh-sach-bao-mat` ở server, gọi `GET /api/v1/pages/{slug}`
  (cache 60 giây).
- Admin tạo/sửa trang qua `/api/v1/admin/pages`. `isPublished = false` thì API công khai trả 404.
- `contentHtml` được **lưu nguyên văn**, BE không lọc. Nơi render (Storefront, màn xem trước Admin) bắt buộc sanitize,
  giống mô tả sách. Storefront giữ thẻ văn bản cơ bản (`p`, `h2`–`h4`, `ul/ol/li`, `strong/em`, `blockquote`, `a`, `hr`)
  và thuộc tính `id` của `h2`/`h3`.
- Mục lục Storefront sinh từ các thẻ `<h2>`. Nên đặt `id` kebab-case cho mỗi `<h2>` để link `#id` giữ ổn định
  khi sửa tiêu đề; thiếu `id` thì FE tự sinh từ chữ trong tiêu đề.

## API

### Công khai

| Method | Route                     | Response                                                                 |
| ------ | ------------------------- | ------------------------------------------------------------------------ |
| GET    | `/api/v1/pages/{slug}`    | 200 `StaticPageResponse`; 404 `NOT_FOUND` khi không có hoặc chưa xuất bản |

`slug` không phân biệt hoa thường.

```jsonc
// StaticPageResponse
{
  "slug": "dieu-khoan-su-dung",
  "title": "Điều khoản sử dụng",
  "description": "Điều kiện sử dụng tài khoản, ...", // null được
  "contentHtml": "<blockquote>...</blockquote><h2 id=\"pham-vi\">...</h2><p>...</p>",
  "updatedAt": "2026-11-11T17:00:00Z"
}
```

### Admin (policy `content_admin`)

| Method | Route                          | Body                       | Response                                          |
| ------ | ------------------------------ | -------------------------- | ------------------------------------------------- |
| GET    | `/api/v1/admin/pages?page=&limit=` | —                      | Phân trang `AdminStaticPageSummaryResponse` theo slug A→Z |
| GET    | `/api/v1/admin/pages/{id}`     | —                          | 200 `AdminStaticPageResponse`; 404                |
| POST   | `/api/v1/admin/pages`          | `UpsertStaticPageRequest`  | 201 `AdminStaticPageResponse`; 409 `SLUG_DUPLICATE` trùng slug |
| PUT    | `/api/v1/admin/pages/{id}`     | `UpsertStaticPageRequest`  | 200 `AdminStaticPageResponse`; 404; 409           |
| DELETE | `/api/v1/admin/pages/{id}`     | —                          | 204; 404                                          |

```jsonc
// UpsertStaticPageRequest (PUT thay thế toàn bộ)
{
  "slug": "chinh-sach-doi-tra",    // bắt buộc, ≤ 100, ^[a-z0-9]+(-[a-z0-9]+)*$
  "title": "Chính sách đổi trả",   // bắt buộc, ≤ 255
  "description": "…",              // ≤ 500, rỗng thành null
  "contentHtml": "<h2 id=\"…\">…</h2><p>…</p>", // bắt buộc, ≤ 200 000 ký tự
  "isPublished": true
}
// AdminStaticPageSummaryResponse: { id, slug, title, isPublished, updatedAt }
// AdminStaticPageResponse: { id, slug, title, description, contentHtml, isPublished, createdAt, updatedAt }
```

Lỗi validate: 400 `VALIDATION_ERROR`.

## Cấu hình cần thiết

Không có.

## Thay đổi CSDL

- Migration `AddStaticPages` (Content): bảng `content.static_pages`, unique index `ux_static_pages_slug`.
- Migration seed hai trang chính sách (bản dự thảo 12/11/2026, chuyển từ file viết cứng
  `apps/storefront/src/features/legal/content.ts` của FE), `is_published = true`. Thông báo "dự thảo" nằm trong
  `contentHtml` (khối `<blockquote>` đầu trang): khi áp dụng chính thức thì sửa nội dung, không cần deploy.

## Hạn chế / việc còn lại

- Màn Admin soạn thảo WYSIWYG: FE `docs/features/admin-static-pages.md`. Chưa có khóa khi hai người cùng sửa (người lưu sau ghi đè).
- Chưa lưu lịch sử phiên bản. Phiên bản điều khoản khách đồng ý khi đăng ký vẫn là cấu hình `Auth:TermsVersion`,
  không tự đổi khi sửa trang.
- Storefront chỉ có route cho hai slug đã seed; trang tĩnh mới (đổi trả, vận chuyển, FAQ...) cần thêm route bên FE.
