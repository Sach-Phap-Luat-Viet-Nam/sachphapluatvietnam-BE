# Cart: giỏ hàng

## Mục đích

Giỏ hàng cho cả khách vãng lai và thành viên (FR-30, FR-31, FR-33), lưu trong **Redis**.
Module `SachLuat.Modules.Cart`.

## Luồng xử lý

- Mỗi giỏ là một Redis hash `{prefix}u:{userId}` (thành viên) hoặc `{prefix}g:{guid}` (khách), field = `skuId`,
  value = số lượng. Mỗi lần sửa gia hạn 30 ngày. Redis bật `appendonly` để giỏ không mất khi khởi động lại.
- Khách vãng lai nhận diện bằng cookie `sl_cart` (GUID ngẫu nhiên, HttpOnly, Secure, SameSite=Lax, path `/api/v1`).
- **Gộp giỏ khi đăng nhập**: request có cả access token và cookie `sl_cart` → cộng giỏ khách vào giỏ tài khoản
  (không vượt 20 cuốn/SKU), xóa giỏ khách và cookie.
- Thêm/sửa số lượng kiểm tra: SKU còn bán (`IsSellable`), ≤ 20 cuốn/SKU (FR-31), tồn khả dụng đủ, ≤ 50 dòng.
- Giỏ trả kèm `isLatestEdition` để frontend hiện hộp xác nhận khi thêm ấn bản cũ (US-02) và `isSellable`,
  `availableQuantity`. SKU đã bị xóa khỏi Catalog tự bị bỏ khỏi giỏ.
- Giỏ chưa tạm giữ tồn; tồn chỉ được giữ khi tạo đơn (BR-11).

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1/cart`, đăng nhập là tùy chọn.

| Method | Route | Request → Response |
| --- | --- | --- |
| GET | `/cart` | → `{items[], totalQuantity, subtotal}` |
| POST | `/cart/items` | `{skuId, quantity}` (cộng thêm) → giỏ |
| PATCH | `/cart/items/{skuId}` | `{quantity}` (đặt số lượng, 0 = xóa) → giỏ |
| DELETE | `/cart/items/{skuId}` | → giỏ |
| DELETE | `/cart` | → 204 |

Mỗi dòng: `skuId, skuCode, bookTitle, bookSlug, editionName, coverType, coverImageUrl, quantity, unitPrice,
listPrice, lineTotal, isLatestEdition, isSellable, availableQuantity`.

Mã lỗi: `MAX_QTY_EXCEEDED` (422), `SKU_NOT_AVAILABLE` (422), `OUT_OF_STOCK` (409), `CART_FULL` (422).

## Cấu hình cần thiết

| Key | Ghi chú |
| --- | --- |
| `ConnectionStrings:Redis` | Local: `localhost:6380` (docker compose) |
| `Cart:MaxQuantityPerSku` | Mặc định 20 |
| `Cart:MaxLines`, `Cart:LifetimeDays`, `Cart:KeyPrefix` | 50 dòng, 30 ngày, `sachluat:cart:` |

## Thay đổi CSDL (migration liên quan)

Không có (dữ liệu nằm trong Redis).

## Hạn chế / việc còn lại

- Chưa áp mã giảm giá (`/cart/apply-coupon`, FR-32): chờ module Promotion.
- Thêm cùng lúc từ nhiều tab có thể ghi đè số lượng của nhau (đọc–ghi không nguyên tử), chấp nhận cho giỏ hàng.
