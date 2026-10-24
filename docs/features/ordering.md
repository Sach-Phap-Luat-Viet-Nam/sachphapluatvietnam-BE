# Ordering: đặt hàng và xử lý đơn

## Mục đích

Đặt hàng từ giỏ (FR-33, FR-37), theo dõi và tra cứu đơn (FR-40), hủy đơn (FR-41, BR-12), quản lý đơn trên Admin
(FR-70) với trạng thái đúng mục 16.1. Module `SachLuat.Modules.Ordering`, schema `ordering`.

## Luồng xử lý

1. `POST /orders` đọc giỏ hiện tại, kiểm tra SKU còn bán và ≤ 20 cuốn/SKU, chụp snapshot tên sách, ấn bản, ISBN,
   giá, địa chỉ (mục 10.3).
2. **Tính tiền** (`CheckoutPricingService`, dùng chung với xem trước nên hai nơi không thể lệch nhau):
   `subtotal` (giá hiện hành × số lượng) → **phí ship** theo tỉnh và trọng lượng, miễn phí khi `subtotal` ≥ ngưỡng
   (tính trên tạm tính **trước** giảm giá, BR-14; xem `docs/features/shipping.md`) → **khuyến mãi** (giảm trên đơn,
   giảm phí ship; xem `docs/features/promotion.md`).
   `total = subtotal − discount + shippingFee − shippingDiscount`. Mức giảm trên đơn được phân bổ về từng dòng
   (`order_items.discount_amount`) để hoàn tiền một phần và báo cáo lợi nhuận gộp (FR-95).
3. Nếu request có `expectedTotal` mà khác tổng vừa tính (chương trình hết hạn, mã hết lượt, giá đổi, ...) → 409
   `CHECKOUT_TOTAL_CHANGED`, FE gọi lại `POST /checkout/preview` cho khách xem số mới. Không gửi `expectedTotal`
   thì bỏ qua bước này.
4. Sinh mã đơn `SLVN` + `yyMMdd` (giờ Việt Nam) + số thứ tự trong ngày, ví dụ `SLVN2610230001`.
5. Tạm giữ tồn (BR-11); thiếu hàng → `OUT_OF_STOCK`, không tạo đơn. Sau đó **ghi nhận lượt dùng khuyến mãi**;
   hết lượt giữa chừng → trả tồn đã giữ và báo `COUPON_EXPIRED` hoặc `PROMOTION_UNAVAILABLE`. Lưu đơn lỗi → tự
   hoàn tồn và lượt dùng đã giữ.
6. Trạng thái đầu: COD → `pending_confirm`; chuyển khoản → `pending_payment`, hạn thanh toán 30 phút.
7. Xóa giỏ, trả đơn. Header `Idempotency-Key` gửi lại → trả đúng đơn đã tạo (chống bấm nhiều lần).

**Xem trước thanh toán** (`POST /checkout/preview`): cùng cách tính ở bước 2 cho giỏ hiện tại (`source = cart`) hoặc
một SKU (`source = buy_now`), kèm thử mã giảm giá. Không ghi gì, không trừ lượt dùng mã. Trả từng dòng, `subtotal`,
`discount`, `shippingFee`, `shippingDiscount`, `total`, các chương trình đã áp, `couponApplied`, ngưỡng miễn ship,
số tiền còn thiếu để được miễn ship và thời gian giao dự kiến. Có giới hạn tần suất theo IP (chống dò mã).

**Mua ngay** (`POST /orders/buy-now`, nút "Mua ngay" ở trang chi tiết sách): request gửi kèm `skuId`, `quantity` và
các thông tin checkout như trên, đơn chỉ gồm SKU đó. Các bước 1–6 và `Idempotency-Key` giống hệt đặt từ giỏ, khác
ở chỗ **không đọc và không xóa giỏ hàng**, nên những gì khách đã bỏ vào giỏ vẫn còn nguyên.

**Đổi trạng thái** chỉ theo bảng 16.1 (`OrderStatusMachine`); dòng đơn bị khóa (`FOR UPDATE`) trong lúc đổi.
Tác động kèm theo:

| Chuyển sang | Tác động |
| --- | --- |
| `paid` | `payment_status = paid` |
| `packing` | Trừ tồn thật FIFO, ghi giá vốn `cost_price` từng dòng |
| `cancelled` | Hoàn tồn tạm giữ và trả lượt dùng khuyến mãi (mã dùng lại được) |
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
| POST | `/orders` | Tùy chọn | `{customerName, phone, email?, shippingAddress: {province, ward, streetAddress}, paymentMethod: cod\|bank_transfer, invoiceInfo?: {companyName, taxCode, address, email}, note?, couponCode?, expectedTotal?}` + header `Idempotency-Key` → 201 `OrderResponse` |
| POST | `/orders/buy-now` | Tùy chọn | `{skuId, quantity, ...}` + các field như `POST /orders`, header `Idempotency-Key` → 201 `OrderResponse`; giỏ giữ nguyên |
| POST | `/checkout/preview` | Tùy chọn | `{source: cart\|buy_now, skuId?, quantity?, province?, couponCode?, phone?}` → `CheckoutPreviewResponse` (`skuId`, `quantity` bắt buộc với `buy_now`) |
| GET | `/orders` | Bearer | Đơn của tôi (phân trang) |
| GET | `/orders/{code}` | Bearer | Chi tiết đơn của tôi kèm lịch sử trạng thái |
| POST | `/orders/lookup` | Không | `{orderCode, phone}` (giới hạn tần suất) |
| POST | `/orders/{code}/cancel` | Bearer | `{reason?}` |
| POST | `/shipping/quote` | Không | `{subtotal, province?, items?: [{skuId, quantity}]}` → `{shippingFee, freeShippingThreshold, amountToFreeShipping, eta: {minDays, maxDays}?, zoneName?}`. Chỉ gửi `subtotal` (không `province`) thì dùng phí cố định như trước. `freeShippingThreshold` là `null` khi Admin tắt miễn ship theo ngưỡng |
| GET | `/admin/orders?status=&paymentMethod=&from=&to=&q=` | orders_read | Lọc theo trạng thái, phương thức, ngày (giờ VN), mã đơn/SĐT |
| GET | `/admin/orders/{code}` | orders_read | `{order, userId, nextStatuses[], costOfGoods}` |
| POST | `/admin/orders/{code}/status` | orders_manage | `{status, note?}` |

`OrderResponse`: mã đơn, trạng thái, phương thức/tình trạng thanh toán, thông tin khách, địa chỉ, `items[]`,
`subtotal, discount, shippingFee, shippingDiscount, total`, `appliedPromotions[]` (snapshot tên, mã, số tiền),
hóa đơn, `paymentExpiresAt`, `canCancel`, `history[]`. Mỗi dòng đơn có thêm `discountAmount`.
`shippingFee` là phí khách phải trả theo biểu giá sau miễn phí theo ngưỡng; khách thực trả
`shippingFee − shippingDiscount`.

Mã lỗi: `CART_EMPTY` (422, chỉ đặt từ giỏ), `SKU_NOT_AVAILABLE` (422), `MAX_QTY_EXCEEDED` (422), `OUT_OF_STOCK` (409),
`PAYMENT_METHOD_NOT_SUPPORTED` (422), `IDEMPOTENCY_KEY_REUSED` (409), `ORDER_NOT_CANCELLABLE` (422),
`ORDER_INVALID_TRANSITION` (409), `ORDER_NOT_PAID` (422), `NOT_FOUND` (404), `CHECKOUT_TOTAL_CHANGED` (409),
`ADDRESS_NOT_SUPPORTED` (422, tỉnh nằm ngoài vùng giao hàng), cùng các mã mã giảm giá `COUPON_INVALID`,
`COUPON_EXPIRED`, `COUPON_MIN_ORDER`, `COUPON_NOT_APPLICABLE`, `COUPON_LOGIN_REQUIRED`, `PROMOTION_UNAVAILABLE`
(xem `docs/features/promotion.md`).

## Cấu hình cần thiết

| Key | Mặc định |
| --- | --- |
| `Ordering:PaymentTimeoutMinutes` | 30 |
| `Ordering:MaxQuantityPerSku` | 20 |
| `Ordering:EnableExpiredPaymentSweep`, `Ordering:ExpiredPaymentSweepSeconds` | true, 60 |
| `RateLimiting:CheckoutPermitPerMinute` | 60 (số request `POST /checkout/preview` mỗi phút mỗi IP) |

`Ordering:ShippingFlatFee` và `Ordering:FreeShippingThreshold` **đã bỏ**: phí cố định và ngưỡng miễn ship nay do
Admin sửa trong CSDL (module Shipping); giá trị mặc định khi Admin chưa lưu là `Shipping:DefaultFlatFee` và
`Shipping:DefaultFreeShippingThreshold`. Đặt lại hai biến cũ trên môi trường (nếu có) sẽ không còn tác dụng.

## Thay đổi CSDL (migration liên quan)

`CreateOrderingSchema` (schema `ordering`): `orders` (`shipping_address`, `invoice_info` dạng JSONB,
unique `order_code`, `idempotency_key`), `order_items`, `order_status_logs`, `order_code_counters`.
Không đặt khóa ngoại sang `auth.users` hay `catalog.skus`.

`AddOrderPromotionsAndShippingDiscount`: `orders` thêm `shipping_base_fee` (phí theo biểu giá trước miễn phí theo
ngưỡng, để báo cáo phần doanh nghiệp bù), `shipping_discount`, `applied_promotions` (JSONB); `order_items` thêm
`discount_amount`; siết `ck_orders_amounts` (`discount <= subtotal`, `shipping_base_fee >= shipping_fee`,
`shipping_discount <= shipping_fee`) và thêm `ck_order_items_discount`. Đơn cũ được backfill
`shipping_base_fee = shipping_fee` **trước** khi tạo constraint (migration có câu `UPDATE`), nên đơn cũ từng được
miễn ship theo ngưỡng sẽ có phí biểu giá bằng 0 (không khôi phục được). Production tắt
`Database:ApplyMigrationsOnStartup`, cần áp migration này ở bước deploy.

## Hạn chế / việc còn lại

- Mua ngay chỉ đặt một SKU mỗi lần.
- Chỉ hỗ trợ COD và chuyển khoản đối soát tay; VNPAY/MoMo/ZaloPay/VietQR và webhook (FR-35, mục 11.4) chưa làm.
- Phí ship tính theo bảng giá tự quản lý, chưa gọi API đơn vị vận chuyển (FR-34); chưa tạo vận đơn (FR-71) nên
  chưa có phí thực trả đơn vị vận chuyển để đối chiếu với phí thu của khách.
- Chưa có pre-order (FR-38), đổi trả (FR-42), hóa đơn điện tử, email/ZNS xác nhận đơn (FR-37).
- Hoàn tiền một phần chưa làm (FR-73); khi làm, dùng `order_items.discount_amount` để tính phần đã giảm của từng dòng.
- Lượt dùng khuyến mãi chỉ được trả khi **hủy** đơn, chưa trả khi đơn `returned`/`refunded`.
- Đơn đã thanh toán (`paid`) không hủy được theo bảng 16.1; hủy đơn `confirmed` đã chuyển khoản thì hoàn tiền
  đang xử lý tay.
- Kế toán chưa có quyền đổi `paid` (chỉ `orders_manage`); tạo đơn thủ công (FR-72) chưa làm.
- Đơn và tồn kho nằm ở hai DbContext, đồng bộ bằng khóa dòng đơn + thao tác kho idempotent, không phải
  transaction chung.
