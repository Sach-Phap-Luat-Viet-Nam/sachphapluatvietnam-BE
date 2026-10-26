# Ordering: đặt hàng và xử lý đơn

## Mục đích

Đặt hàng từ giỏ (FR-33, FR-37), theo dõi và tra cứu đơn (FR-40), hủy đơn (FR-41, BR-12), quản lý đơn trên Admin
(FR-70) với trạng thái đúng mục 16.1. Module `SachLuat.Modules.Ordering`, schema `ordering`.

## Luồng xử lý

1. `POST /orders` đọc giỏ hiện tại, kiểm tra SKU còn bán và ≤ 20 cuốn/SKU, chụp snapshot tên sách, ấn bản, ISBN,
   giá, địa chỉ (mục 10.3). Đơn COD được kiểm tra **chống bom hàng** theo số điện thoại trước (xem bên dưới).
   Số điện thoại lưu dạng chuẩn `0xxxxxxxxx` (`+84…` được đổi về `0…`) nên tra cứu, Admin tìm kiếm và giới hạn theo
   số điện thoại khớp dù khách gõ dạng nào.
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
6. Trạng thái đầu: COD → `pending_confirm`; chuyển khoản → `pending_payment`, hạn thanh toán 30 phút. Đơn chuyển
   khoản được SePay tự đối soát và chuyển `paid` khi tiền về (xem `docs/features/sepay-payment.md`).
7. Xóa giỏ, trả đơn. Header `Idempotency-Key` gửi lại → trả đúng đơn đã tạo (chống bấm nhiều lần).

**Link xem/hủy đơn cho khách vãng lai**: đơn không đăng nhập có `guestAccessToken` (ngẫu nhiên 128 bit, 22 ký tự
base64url), chỉ trả trong response tạo đơn (`POST /orders`, `POST /orders/buy-now`, kể cả khi gửi lại cùng
`Idempotency-Key`). FE dựng trang ví dụ `/don-hang/{token}` ở bước "đặt hàng thành công"; khách mở link để xem
(`GET /orders/guest/{token}`) và tự hủy (`POST /orders/guest/{token}/cancel`, BR-12, cùng điều kiện như thành viên).
Token sai trả `NOT_FOUND`. Tra cứu bằng mã đơn + SĐT vẫn còn nhưng không trả token, nên không hủy được bằng cách đó.

**Chống đặt nhầm / bom hàng COD** (`CustomerRiskService`), tính theo số điện thoại đã chuẩn hóa trên mọi đơn (cả
thành viên):

- Đơn COD **đang mở** (`pending_confirm`, `confirmed`, `packing`, `ready_to_ship`, `shipping`) đạt
  `Ordering:MaxOpenCodOrdersPerPhone` → không đặt thêm COD (`TOO_MANY_OPEN_ORDERS`).
- Đơn COD **giao thất bại** (từng chuyển `shipping → returning`) đạt `Ordering:CodFailedDeliveryLimit` → số đó chỉ
  được chuyển khoản (`COD_NOT_ALLOWED`). Đổi trả sau khi đã nhận (`return_requested → returned`) không tính.
- Chuyển khoản luôn đặt được. `POST /checkout/preview` có `phone` thì trả `codAllowed` để FE ẩn COD trước khi khách
  bấm đặt hàng; không có `phone` thì `codAllowed = true`.
- Admin xem `customerRisk` trong chi tiết đơn (tổng đơn, đã nhận, đã hủy, giao thất bại, COD đang mở, có bị khóa
  COD không) để CSKH cân nhắc khi gọi xác nhận đơn `pending_confirm`.

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
| POST | `/orders/lookup` | Không | `{orderCode, phone}` (giới hạn tần suất); `phone` dạng `0…` hay `+84…` đều khớp |
| POST | `/orders/{code}/cancel` | Bearer | `{reason?}` |
| GET | `/orders/guest/{token}` | Không | Khách vãng lai xem đơn qua link (giới hạn tần suất) → `OrderResponse` |
| POST | `/orders/guest/{token}/cancel` | Không | `{reason?}` → `OrderResponse` (giới hạn tần suất) |
| POST | `/shipping/quote` | Không | `{subtotal, province?, items?: [{skuId, quantity}]}` → `{shippingFee, freeShippingThreshold, amountToFreeShipping, eta: {minDays, maxDays}?, zoneName?}`. Chỉ gửi `subtotal` (không `province`) thì dùng phí cố định như trước. `freeShippingThreshold` là `null` khi Admin tắt miễn ship theo ngưỡng |
| GET | `/admin/orders?status=&paymentMethod=&from=&to=&q=` | orders_read | Lọc theo trạng thái, phương thức, ngày (giờ VN), mã đơn/SĐT |
| GET | `/admin/orders/{code}` | orders_read | `{order, userId, nextStatuses[], costOfGoods, customerRisk}` |
| POST | `/admin/orders/{code}/status` | orders_manage | `{status, note?}` |

`OrderResponse`: mã đơn, trạng thái, phương thức/tình trạng thanh toán, thông tin khách, địa chỉ, `items[]`,
`subtotal, discount, shippingFee, shippingDiscount, total`, `appliedPromotions[]` (snapshot tên, mã, số tiền),
hóa đơn, `paymentExpiresAt`, `canCancel`, `history[]`, `paymentInstructions` (tài khoản, nội dung, QR cho đơn
chuyển khoản còn `pending_payment`, ngược lại `null`; chỉ trả ở response đơn khách, không có trong `order` của Admin). Mỗi dòng đơn có thêm `discountAmount`.
`shippingFee` là phí khách phải trả theo biểu giá sau miễn phí theo ngưỡng; khách thực trả
`shippingFee − shippingDiscount`. `guestAccessToken` chỉ có giá trị ở response tạo đơn của khách vãng lai, còn lại
`null`.

Sách được combo phủ trọn hiện và lưu theo **giá niêm yết** (`items[].unitPrice`, `subtotal`, `order_items.unit_price`), tiền
giảm combo cũng tính theo giá niêm yết nên số khách phải trả không đổi; phí ship và ngưỡng miễn phí vẫn tính trên tạm
tính theo giá hiện hành (xem `docs/features/promotion.md`, mục Combo).

`appliedPromotions[].kind` là `order_discount`, `free_shipping` hoặc `combo`. Với `combo`, `promotionId` là **id combo**
(không phải chương trình khuyến mãi), `couponCode` luôn `null`, `amount` là tổng giảm của các bộ combo đó; giá combo
chỉ thể hiện qua `discount` và `discountAmount` từng dòng, dòng đơn vẫn là các SKU lẻ (xem `docs/features/promotion.md`,
mục Combo).

`CheckoutPreviewResponse` có thêm `codAllowed`. `customerRisk` (Admin, `POST /admin/orders/{code}/status` cũng trả):
`{totalOrders, deliveredCount, cancelledCount, failedCodCount, openCodCount, codBlocked, openCodLimitReached,
codAllowed}`.

Mã lỗi: `CART_EMPTY` (422, chỉ đặt từ giỏ), `SKU_NOT_AVAILABLE` (422), `MAX_QTY_EXCEEDED` (422), `OUT_OF_STOCK` (409),
`PAYMENT_METHOD_NOT_SUPPORTED` (422), `IDEMPOTENCY_KEY_REUSED` (409), `ORDER_NOT_CANCELLABLE` (422),
`ORDER_INVALID_TRANSITION` (409), `ORDER_NOT_PAID` (422), `NOT_FOUND` (404), `CHECKOUT_TOTAL_CHANGED` (409),
`ADDRESS_NOT_SUPPORTED` (422, tỉnh nằm ngoài vùng giao hàng), `TOO_MANY_OPEN_ORDERS` (422),
`COD_NOT_ALLOWED` (422), cùng các mã mã giảm giá `COUPON_INVALID`,
`COUPON_EXPIRED`, `COUPON_MIN_ORDER`, `COUPON_NOT_APPLICABLE`, `COUPON_LOGIN_REQUIRED`, `PROMOTION_UNAVAILABLE`
(xem `docs/features/promotion.md`).

## Cấu hình cần thiết

| Key | Mặc định |
| --- | --- |
| `Ordering:PaymentTimeoutMinutes` | 30 |
| `Ordering:MaxQuantityPerSku` | 20 |
| `Ordering:EnableExpiredPaymentSweep`, `Ordering:ExpiredPaymentSweepSeconds` | true, 60 |
| `Ordering:MaxOpenCodOrdersPerPhone` | 3 (0 = không giới hạn) |
| `Ordering:CodFailedDeliveryLimit` | 2 (0 = không khóa COD) |
| `RateLimiting:AuthPermitPerMinute` | 10 (dùng chung cho `/orders/lookup` và `/orders/guest/*`) |
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

`AddGuestAccessTokenAndNormalizePhone`: `orders` thêm `guest_access_token` (`varchar(32)`, unique khi khác null),
index `ix_orders_phone`; câu `UPDATE` đổi `phone` dạng `+84xxxxxxxxx` của đơn cũ về `0xxxxxxxxx` (Down không đổi
ngược). Đơn vãng lai tạo trước migration không có token, khách vẫn tra cứu bằng mã đơn + SĐT. Production cần áp
migration ở bước deploy như trên.

## Hạn chế / việc còn lại

- Mua ngay chỉ đặt một SKU mỗi lần.
- Hỗ trợ COD và chuyển khoản đối soát tự động qua SePay (`docs/features/sepay-payment.md`); VNPAY/MoMo/ZaloPay
  (FR-35) chưa làm.
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
- Chống bom hàng tính theo số điện thoại: khách đổi số là lách được. Hai request COD song song có thể cùng vượt
  qua kiểm tra số đơn đang mở (không khóa theo số điện thoại); CSKH vẫn gọi xác nhận đơn `pending_confirm`.
  Chưa có danh sách đen thủ công trên Admin.
- Token nằm trong URL nên có thể lọt vào lịch sử trình duyệt hoặc log proxy; ai có link xem và hủy được đơn.
  Chưa thu hồi hoặc cấp lại token; chưa gửi link qua email/ZNS (khách cần lưu link ở trang đặt hàng thành công).
