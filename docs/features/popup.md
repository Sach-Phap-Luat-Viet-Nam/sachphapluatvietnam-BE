# Popup quảng cáo

## Mục đích

Marketing hiển thị một popup quảng cáo (ảnh + link) trên Storefront theo lịch chạy, thuộc nhóm "Nội dung" (FR-91).
Module `SachLuat.Modules.Content`, schema `content`. Module này cũng là chỗ cho banner, bài viết, trang tĩnh sau này.

## Luồng xử lý

1. **Upload ảnh** (Cloudinary, upload có ký, giống ảnh bìa trong `docs/features/catalog.md`):
   `POST /admin/uploads/popup-signature` → FE gửi file thẳng lên Cloudinary (folder `sachluat/popups`) → lấy
   `secure_url`. Khi đã cấu hình Cloudinary, `imageUrl` phải bắt đầu bằng
   `https://res.cloudinary.com/{CloudName}/image/upload/`, nếu không trả 400 `VALIDATION_ERROR`.
2. **Tạo/sửa popup** trên Admin: tiêu đề, ảnh, link, lịch chạy, bật/tắt, độ ưu tiên, tần suất hiển thị.
3. **Storefront** gọi `GET /popups/active` khi tải trang:
   - Popup được chọn: `isActive = true`, `startsAt <= now`, và `endsAt` trống hoặc `> now`.
   - Nhiều popup cùng chạy → lấy `priority` lớn nhất; bằng nhau thì `startsAt` mới hơn, rồi `id` lớn hơn.
   - Không có popup nào → `204 No Content`.
   - `GET /popups/running` trả các popup đang chạy cùng điều kiện và thứ tự ưu tiên như trên, **không giới
     hạn số lượng**, mảng rỗng khi không có. Phần tử đầu chính là popup `active`. Dùng cho khu "Chương trình đang diễn
     ra" ở trang chủ để khách xem lại popup lỡ tắt; khu này không áp tần suất. Có bao nhiêu popup đang chạy thì hiện
     bấy nhiêu (trước đây cắt ở 3 popup ưu tiên cao nhất).
4. **Tần suất** do FE tự áp, lưu dấu "đã xem" ở trình duyệt với khóa `popup:{id}:{updatedAt}` (popup vừa sửa thì
   `updatedAt` đổi nên hiện lại):

| `displayFrequency` | FE hiển thị |
| --- | --- |
| `every_visit` | Mỗi lần tải trang |
| `once_per_session` | Một lần mỗi phiên trình duyệt (`sessionStorage`) |
| `once_per_day` | Một lần mỗi ngày theo giờ Việt Nam (`localStorage` lưu ngày đã xem) |

- **Trạng thái trên Admin** (`status`, tính lúc trả về, không lưu): `inactive` (đang tắt), `scheduled` (chưa tới
  `startsAt`), `running` (đang chạy), `ended` (đã qua `endsAt`).
- **Link an toàn**: `linkUrl` chỉ nhận đường dẫn trong site (`/khuyen-mai`) hoặc URL `http`/`https`. Chặn
  `javascript:`, `data:`, `//host`, `/\host` vì FE gán thẳng vào `href`.
- Xóa là **xóa mềm** (`deleted_at`), popup đã xóa không hiện ở Admin lẫn Storefront.

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1`. API Admin dùng policy `content_admin` (`super_admin`, `marketing`, Super Admin bắt buộc 2FA).

| Method | Route | Xác thực | Request → Response |
| --- | --- | --- | --- |
| GET | `/popups/active` | Không | → 200 `PopupResponse` hoặc 204 |
| GET | `/popups/running` | Không | → 200 `PopupResponse[]` (không giới hạn số lượng, có thể rỗng) |
| GET | `/admin/popups?page=&limit=` | content_admin | → `PagedResponse<AdminPopupResponse>`, sắp theo ưu tiên |
| GET | `/admin/popups/{id}` | content_admin | → `AdminPopupResponse` |
| POST | `/admin/popups` | content_admin | `UpsertPopupRequest` → 201 `AdminPopupResponse` |
| PUT | `/admin/popups/{id}` | content_admin | `UpsertPopupRequest` → `AdminPopupResponse` |
| DELETE | `/admin/popups/{id}` | content_admin | → 204 |
| POST | `/admin/uploads/popup-signature` | content_admin | → `{uploadUrl, apiKey, timestamp, folder, allowedFormats, signature}` |

`UpsertPopupRequest`:

```json
{
  "title": "Sale sách luật mùa thi",
  "imageUrl": "https://res.cloudinary.com/<cloud>/image/upload/v1/sachluat/popups/mua-thi.jpg",
  "linkUrl": "/khuyen-mai",
  "startsAt": "2026-11-01T00:00:00+07:00",
  "endsAt": "2026-11-30T23:59:59+07:00",
  "isActive": true,
  "priority": 10,
  "displayFrequency": "once_per_day"
}
```

- `title` ≤ 255 ký tự (cũng là alt text của ảnh); `imageUrl` bắt buộc, ≤ 500; `linkUrl` tùy chọn, ≤ 500.
- `startsAt`, `endsAt` gửi kèm offset; lưu và trả về UTC (`...Z`). `endsAt` trống = chạy đến khi tắt,
  có thì phải sau `startsAt`.
- `priority` 0–1000.

`AdminPopupResponse`: các field trên + `id`, `status`, `createdAt`, `updatedAt`.
`PopupResponse` (Storefront): `{id, title, imageUrl, linkUrl, displayFrequency, updatedAt}`.

Mã lỗi: `VALIDATION_ERROR` (400), `NOT_FOUND` (404, không có popup), `UPLOAD_NOT_CONFIGURED` (503, chưa cấu
hình Cloudinary), `UNAUTHORIZED` (401), `FORBIDDEN` (403, không phải `super_admin`/`marketing` hoặc chưa 2FA).

## Cấu hình cần thiết

- `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret`: dùng chung với ảnh bìa
  (xem `docs/features/catalog.md`). Thiếu thì API ký trả 503 và không kiểm tra link ảnh.
- `Cloudinary:PopupFolder`: mặc định `sachluat/popups`.
- Code ký Cloudinary nằm ở `SachLuat.SharedKernel.Media` (`CloudinaryUploadSigner`, `CloudinaryOptions`),
  module nào cần upload ảnh thì gọi `services.AddCloudinaryUploads(configuration)`.

## Thay đổi CSDL (migration liên quan)

Migration `CreateContentSchema` (module Content), schema `content`:

| Bảng | Ghi chú |
| --- | --- |
| `popups` | `title`, `image_url`, `link_url`, `starts_at`, `ends_at` (UTC), `is_active`, `priority`, `display_frequency` (snake_case), `created_at`, `updated_at`, `deleted_at` |

Production tắt `Database:ApplyMigrationsOnStartup`, nên cần áp migration này ở bước deploy trước khi dùng API.

## Hạn chế / việc còn lại

- Storefront chỉ nhận **một** popup; chưa nhắm theo trang (trang chủ, danh mục, chi tiết sách) hay theo đối tượng
  (khách vãng lai, đã đăng nhập).
- Chưa đếm lượt hiển thị/lượt bấm.
- `GET /popups/active` chưa có cache, mỗi lần tải trang là một truy vấn (bảng nhỏ, chưa cần).
- Chưa xóa ảnh cũ trên Cloudinary khi đổi/xóa popup (ảnh mồ côi), giống ảnh bìa.
- Chưa có nhật ký thao tác (FR-98) cho việc tạo/sửa/xóa popup, hiện chỉ ghi log.
