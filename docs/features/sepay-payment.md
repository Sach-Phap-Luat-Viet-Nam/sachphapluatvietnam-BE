# Thanh toán chuyển khoản qua SePay

## Mục đích

Đơn chọn `bank_transfer` được **tự động chuyển sang `paid`** khi khách chuyển đúng số tiền, thay cho đối soát tay
(FR-35, mục 11.4). SePay theo dõi biến động số dư tài khoản ngân hàng đã liên kết và gọi webhook về backend. Code
nằm trong module `Ordering` (không thêm module mới), schema `ordering`.

## Luồng xử lý

1. Khách đặt đơn `bank_transfer` → đơn `pending_payment`, hạn 30 phút. Response đơn có `paymentInstructions`
   (ngân hàng, số tài khoản, tên chủ tài khoản, số tiền, nội dung chuyển khoản **= mã đơn**, `qrUrl` là ảnh VietQR
   dựng sẵn số tiền và nội dung). Chỉ có khi đơn còn `pending_payment` và đã cấu hình `SePay:AccountNumber`/`BankName`.
2. Khách chuyển khoản. SePay gọi `POST /api/v1/payments/sepay/webhook`.
3. Backend kiểm tra header `Authorization: Apikey {SePay:ApiKey}` (so sánh thời gian cố định). Sai hoặc chưa cấu hình
   key → 401 `SEPAY_UNAUTHORIZED`.
4. Chỉ xử lý giao dịch tiền vào (`transferType = in`); tiền ra bị bỏ qua.
5. Ghi giao dịch vào `sepay_transactions` (unique theo `id` SePay) trước khi xử lý. SePay gửi lại cùng giao dịch →
   bỏ qua, nên không trả tiền hai lần. Giao dịch kẹt ở `pending` (lỗi giữa chừng) sẽ được xử lý tiếp khi SePay gửi lại.
6. Tách mã đơn (`SLVN` + ≥ 10 chữ số, không phân biệt hoa/thường) từ `code` rồi `content`, sau đó đối chiếu:

| Tình huống | `result` | Hành động |
| --- | --- | --- |
| Không thấy mã đơn | `unmatched` | Chỉ ghi nhận |
| Mã đơn không tồn tại, hoặc không phải đơn chuyển khoản | `unmatched` | Chỉ ghi nhận |
| Đơn không còn `pending_payment` (đã hủy, quá hạn, đã thanh toán) | `order_not_payable` | Chỉ ghi nhận |
| Số tiền khác `total` của đơn (thiếu hoặc thừa) | `amount_mismatch` | Chỉ ghi nhận |
| Đúng đơn, đúng tiền, đang `pending_payment` | `matched` | Đơn → `paid` (`OrderStatusService`, ghi lịch sử) |

7. Trả HTTP 200 `{"success": true}` cho mọi giao dịch đã nhận (kể cả không khớp đơn), vì đã lưu để đối soát; SePay
   không cần gửi lại. Lỗi hệ thống (500) thì SePay gửi lại theo cơ chế của họ.

Các trường hợp không tự khớp **không** đổi trạng thái đơn (kể cả tiền về sau khi đơn bị tự hủy do quá hạn): Admin xem
danh sách, liên hệ khách và hoàn tiền hoặc xử lý tay. Mỗi giao dịch cần xem xét còn được ghi log mức `Warning`.

## API (method, route, request, response, mã lỗi)

| Method | Route | Xác thực | Mô tả |
| --- | --- | --- | --- |
| POST | `/api/v1/payments/sepay/webhook` | Header `Authorization: Apikey ...` | Payload SePay: `{id, gateway, transactionDate, accountNumber, code, content, transferType, transferAmount, referenceCode, ...}` → 200 `{"success": true}` |
| GET | `/api/v1/admin/payments/sepay-transactions?result=&page=&limit=` | `orders_read` | Giao dịch SePay đã nhận, mới nhất trước; `result` là `pending`, `matched`, `unmatched`, `amount_mismatch`, `order_not_payable` |

`transactionDate` là giờ Việt Nam dạng `yyyy-MM-dd HH:mm:ss`, lưu UTC. Trường không dùng trong payload bị bỏ qua.
Mã lỗi: `SEPAY_UNAUTHORIZED` (401), `VALIDATION_ERROR` (400, `result` không hợp lệ ở API Admin).

`OrderResponse.paymentInstructions` (xem `docs/features/ordering.md`):
`{bankName, accountNumber, accountName, amount, transferContent, qrUrl}`, `null` khi không áp dụng.

## Cấu hình cần thiết

| Key | Ý nghĩa |
| --- | --- |
| `SePay:ApiKey` | Khóa tự đặt khi tạo webhook trên SePay (kiểu xác thực API Key); để trống thì webhook từ chối tất cả |
| `SePay:AccountNumber`, `SePay:BankName` | Tài khoản nhận tiền đã liên kết SePay; `BankName` theo tên ngân hàng SePay dùng để dựng QR (ví dụ `MBBank`). Thiếu một trong hai thì không trả `paymentInstructions` |
| `SePay:AccountName` | Tên chủ tài khoản, chỉ để hiển thị |
| `SePay:QrBaseUrl` | Mặc định `https://qr.sepay.vn/img` |

Secret (`ApiKey`) đặt qua `dotnet user-secrets` hoặc biến môi trường (`SePay__ApiKey`), không commit. Trên SePay:
liên kết tài khoản ngân hàng, tạo webhook trỏ tới `https://<host-api>/api/v1/payments/sepay/webhook` với xác thực API
Key trùng `SePay:ApiKey`, chỉ gửi giao dịch tiền vào.

## Thay đổi CSDL (migration liên quan)

`AddSepayTransactions` (schema `ordering`): bảng `sepay_transactions` (`sepay_id` unique, `result`, `order_code`,
`note`, `amount`, `received_at`, ...), index `(result, received_at)`. Không có khóa ngoại sang `orders`. Production tắt
`Database:ApplyMigrationsOnStartup`, cần áp migration ở bước deploy.

## Hạn chế / việc còn lại

- Route là `/api/v1/payments/sepay/webhook`, khác `POST /webhooks/bank-transfer` ở mục 11.4 đặc tả để theo tiền tố
  `api/v1` của repo.
- Đã nhận tiền nhưng thiếu/thừa/đơn hết hạn thì chưa có thao tác Admin để ghép đơn hoặc ghi nhận hoàn tiền; Admin
  chỉ xem danh sách rồi xử lý tay (đổi `paid` bằng `POST /admin/orders/{code}/status` khi phù hợp).
- Đơn đã bị hủy (tồn đã hoàn) mà tiền vẫn về không được mở lại tự động (bảng 16.1 không có `cancelled → paid`).
- Hai lần gửi cùng một giao dịch chạy song song: lần đến sau bỏ qua; không có khóa riêng theo giao dịch.
- Webhook chưa có giới hạn tần suất riêng và chưa lọc theo số tài khoản nhận (`accountNumber`).
- Chưa có thao tác Admin xem hướng dẫn thanh toán, chưa gửi email/ZNS nhắc thanh toán.
- Tích hợp dựa trên hiểu biết về định dạng webhook SePay; cần thử với giao dịch thật/sandbox của SePay.
