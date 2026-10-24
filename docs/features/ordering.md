# Ordering: đặt hàng và xử lý đơn

## Mục đích

Đặt hàng từ giỏ (FR-33, FR-37), theo dõi và tra cứu đơn (FR-40), hủy đơn (FR-41, BR-12), quản lý đơn trên Admin
(FR-70) với trạng thái đúng mục 16.1. Module `SachLuat.Modules.Ordering`, schema `ordering`.

## Luồng xử lý

1. `POST /orders` đọc giỏ hiện tại, kiểm tra SKU còn bán và ≤ 20 cuốn/SKU, chụp snapshot tên sách, ấn bản, ISBN,
   giá, địa chỉ (mục 10.3).
2. Sinh mã đơn `SLVN` + `yyMMdd` (giờ Việt Nam) + số thứ tự trong ngày, ví dụ `SLVN2610230001`.
3. Phí ship: cố định `ShippingFlatFee`, miễn phí khi tạm tính ≥ `FreeShippingThreshold` (BR-14).
4. Tạm giữ tồn (BR-11); thiếu hàng → `OUT_OF_STOCK`, không tạo đơn. Lưu đơn lỗi → tự hoàn tồn đã giữ.
5. Trạng thái đầu: COD → `pending_confirm`; chuyển khoản → `pending_payment`, hạn thanh toán 30 phút.
6. Xóa giỏ, trả đơn. Header `Idempotency-Key` gửi lại → trả đúng đơn đã tạo (chống bấm nhiều lần).

**Mua ngay** (`POST /orders/buy-now`, nút "Mua ngay" ở trang chi tiết sách): request gửi kèm `skuId`, `quantity` và
các thông tin checkout như trên, đơn chỉ gồm SKU đó. Các bước 1–5 và `Idempotency-Key` giống hệt đặt từ giỏ, khác
ở chỗ **không đọc và không xóa giỏ hàng**, nên những gì khách đã bỏ vào giỏ vẫn còn nguyên.

**Đổi trạng thái** chỉ theo bảng 16.1 (`OrderStatusMachine`); dòng đơn bị khóa (`FOR UPDATE`) trong lúc đổi.
Tác động kèm theo:

| Chuyển sang | Tác động |
| --- | --- |
| `paid` | `payment_status = paid` |
| `packing` | Trừ tồn thật FIFO, ghi giá vốn `cost_price` từng dòng |
| `cancelled` | Hoàn tồn tạm giữ |
| `delivered` (COD) | `payment_status = paid` |
| `returned` | Nhập lại kho đúng các lô đã xuất |
| `refunded` | Chỉ khi đã thanh toán; `payment_status = refunded` |

- **Khách tự hủy** (BR-12): chỉ khi trạng thái hiện tại còn chuyển được sang `cancelled`
  (`pending_payment`, `pending_confirm`, `preorder_waiting`, `confirmed`).
- **Tự hủy quá hạn** (BR-10 bước 4): job nền mỗi 60 giây hủy đơn `pending_payment` quá hạn và hoàn tồn.
  Chạy nhiều instance vẫn an toàn nhờ khóa dòng.

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1`.

| Method | Route | Xác thực | Mô tả |
| --- | --- | --- | --- |
| POST | `/orders` | Tùy chọn | `{customerName, phone, email?, shippingAddress: {province, ward, streetAddress}, paymentMethod: cod\|bank_transfer, invoiceInfo?: {companyName, taxCode, address, email}, note?}` + header `Idempotency-Key` → 201 `OrderResponse` |
| POST | `/orders/buy-now` | Tùy chọn | `{skuId, quantity, ...}` + các field như `POST /orders`, header `Idempotency-Key` → 201 `OrderResponse`; giỏ giữ nguyên |
| GET | `/orders` | Bearer | Đơn của tôi (phân trang) |
| GET | `/orders/{code}` | Bearer | Chi tiết đơn của tôi kèm lịch sử trạng thái |
| POST | `/orders/lookup` | Không | `{orderCode, phone}` (giới hạn tần suất) |
| POST | `/orders/{code}/cancel` | Bearer | `{reason?}` |
| POST | `/shipping/quote` | Không | `{subtotal}` → `{shippingFee, freeShippingThreshold}` |
| GET | `/admin/orders?status=&paymentMethod=&from=&to=&q=` | orders_read | Lọc theo trạng thái, phương thức, ngày (giờ VN), mã đơn/SĐT |
| GET | `/admin/orders/{code}` | orders_read | `{order, userId, nextStatuses[], costOfGoods}` |
| POST | `/admin/orders/{code}/status` | orders_manage | `{status, note?}` |

`OrderResponse`: mã đơn, trạng thái, phương thức/tình trạng thanh toán, thông tin khách, địa chỉ, `items[]`,
`subtotal, discount, shippingFee, total`, hóa đơn, `paymentExpiresAt`, `canCancel`, `history[]`.

Mã lỗi: `CART_EMPTY` (422, chỉ đặt từ giỏ), `SKU_NOT_AVAILABLE` (422), `MAX_QTY_EXCEEDED` (422), `OUT_OF_STOCK` (409),
`PAYMENT_METHOD_NOT_SUPPORTED` (422), `IDEMPOTENCY_KEY_REUSED` (409), `ORDER_NOT_CANCELLABLE` (422),
`ORDER_INVALID_TRANSITION` (409), `ORDER_NOT_PAID` (422), `NOT_FOUND` (404).

## Cấu hình cần thiết

| Key | Mặc định |
| --- | --- |
| `Ordering:ShippingFlatFee` | 30000 |
| `Ordering:FreeShippingThreshold` | 300000 (BR-14) |
| `Ordering:PaymentTimeoutMinutes` | 30 |
| `Ordering:MaxQuantityPerSku` | 20 |
| `Ordering:EnableExpiredPaymentSweep`, `Ordering:ExpiredPaymentSweepSeconds` | true, 60 |

## Thay đổi CSDL (migration liên quan)

`CreateOrderingSchema` (schema `ordering`): `orders` (`shipping_address`, `invoice_info` dạng JSONB,
unique `order_code`, `idempotency_key`), `order_items`, `order_status_logs`, `order_code_counters`.
Không đặt khóa ngoại sang `auth.users` hay `catalog.skus`.

## Hạn chế / việc còn lại

- Mua ngay chỉ đặt một SKU mỗi lần. Chưa có API xem trước (tạm tính, phí ship) cho trang checkout: FE tự tính từ
  giá trên trang chi tiết sách và gọi `POST /shipping/quote`.
- Chỉ hỗ trợ COD và chuyển khoản đối soát tay; VNPAY/MoMo/ZaloPay/VietQR và webhook (FR-35, mục 11.4) chưa làm.
- Phí ship cố định, chưa gọi API đơn vị vận chuyển (FR-34); chưa tạo vận đơn (FR-71).
- Chưa có mã giảm giá, pre-order (FR-38), đổi trả (FR-42), hóa đơn điện tử, email/ZNS xác nhận đơn (FR-37).
- Đơn đã thanh toán (`paid`) không hủy được theo bảng 16.1; hủy đơn `confirmed` đã chuyển khoản thì hoàn tiền
  đang xử lý tay.
- Kế toán chưa có quyền đổi `paid` (chỉ `orders_manage`); tạo đơn thủ công (FR-72) chưa làm.
- Đơn và tồn kho nằm ở hai DbContext, đồng bộ bằng khóa dòng đơn + thao tác kho idempotent, không phải
  transaction chung.
