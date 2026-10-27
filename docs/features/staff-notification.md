# Thông báo đơn hàng cho nhân viên qua Zalo

## Mục đích

Có sự kiện đơn hàng quan trọng thì gửi tin vào **group Zalo nội bộ** để nhân viên xử lý kịp, không phải mở trang
Admin để canh đơn mới. Tin đi qua **Zalo Bot API** chính thức (Zalo Bot Platform).

- Module `SachLuat.Modules.Notification`: kênh gửi, chưa có CSDL. Module khác chỉ dùng `IStaffNotifier` trong
  `SachLuat.Modules.Notification.Contracts`. Sau này đổi sang kênh khác (Zalo OA, Telegram) chỉ cần thay
  implementation.
- Module Ordering quyết định **khi nào** báo và **nội dung** tin, qua outbox `ordering.order_notifications`.

## Luồng xử lý

1. **Ghi outbox cùng transaction với đơn.** Kênh gửi đã bật (`Zalo:Enabled` và đủ token/chat id) thì ghi một dòng
   `order_notifications` với trạng thái `pending`:

   | Sự kiện | `kind` | Chỗ ghi |
   | --- | --- | --- |
   | Đơn mới **không** chờ thanh toán (COD) | `new_order` | `OrderService.PlaceAsync` (đặt từ giỏ và mua ngay) |
   | SePay xác nhận đủ tiền, đơn sang `paid` | `paid` | `SepayPaymentService` → `OrderStatusService` |
   | Khách tự hủy (thành viên hoặc vãng lai) | `cancelled_by_customer` | `OrderService.CancelAsync` → `OrderStatusService` |

   Các trường hợp **không** báo:
   - Đơn chuyển khoản lúc mới tạo (`pending_payment`), để tránh spam đơn khách bỏ dở. Đơn này chỉ báo khi tiền về.
   - Job tự hủy đơn quá hạn thanh toán.
   - Admin đổi trạng thái.

   Kênh gửi chưa bật thì không ghi outbox. Như vậy khi bật lên sau này, hệ thống không gửi dồn một loạt tin cũ.
2. **Gửi nền.** `OrderNotificationDispatcher` (`BackgroundService`) chạy mỗi `StaffNotificationPollSeconds` giây:
   - Giữ chỗ tối đa 10 tin đến hạn bằng `FOR UPDATE SKIP LOCKED`, đẩy `next_attempt_at` thêm 5 phút rồi commit
     ngay. Không giữ transaction trong lúc gọi Zalo. Nhiều instance chạy cùng lúc không gửi trùng. App chết giữa
     chừng thì hết 5 phút tin được gửi lại.
   - Gửi từng tin, lưu kết quả ngay sau mỗi tin.
3. **Thử lại.** Zalo lỗi (mạng, timeout, `ok = false`, HTTP lỗi) thì lùi lần gửi sau theo cấp số nhân: 30 giây,
   1 phút, 2 phút, ... tối đa 1 giờ/lần, kèm log `Warning`. Hết `StaffNotificationMaxAttempts` lần thì chuyển sang
   `failed`, log `Error` và không tự gửi lại nữa.
4. **Nội dung tin.** Tin đi qua nền tảng bên ngoài nên chỉ gửi đủ để nhận biết và xử lý đơn. Chi tiết xem qua link
   Admin.

   ```text
   🛒 ĐƠN MỚI #SLVN2610270002
   💵 550.000đ · COD · 2 cuốn
   ━━━━━━━━━━━━
   👤 Tăng T. Bình · 09xxxxx425
   📍 Tây Thạnh, TP. Hồ Chí Minh
   📚 Sách:
   • [SLVN-BLDS-2026-BM] Bộ luật Dân sự 2026 ×1
   • [SLVN-LDD-2024-BM] Luật Đất đai ×1
   ━━━━━━━━━━━━
   ➡️ Gọi khách xác nhận đơn
   🔗 Xem đơn: https://admin.example.com/don-hang/SLVN2610270002
   ```

   - **Dòng đầu** có loại sự kiện và mã đơn, vì Zalo chỉ hiện dòng này trong thông báo đẩy:
     `🛒 ĐƠN MỚI`, `💰 ĐÃ THANH TOÁN`, `❌ KHÁCH HỦY ĐƠN`.
   - **Thông tin cá nhân**: tên rút gọn tên đệm, SĐT chỉ giữ 2 số đầu và 3 số cuối, không có số nhà/đường.
   - **Danh sách sách**: mỗi dòng có mã SKU (đứng đầu để kho dò mã khi soạn hàng), tên sách, ấn bản (nếu có) và
     số lượng. Đơn quá 5 đầu sách thì hiện 4 dòng đầu và
     `… và N sách khác`. Tên sách dài quá 60 ký tự thì cắt.
   - **Tin hủy** kèm `📝 Lý do: ...` (cắt ở 200 ký tự) khi khách có nhập lý do.
   - **Dòng việc cần làm**:

     | Sự kiện | Dòng việc cần làm |
     | --- | --- |
     | Đơn mới | `Gọi khách xác nhận đơn` |
     | Đã thanh toán | `Xác nhận và đóng gói đơn` |
     | Khách hủy, đơn đã chuyển khoản (hủy sau khi `confirmed`) | `⚠️ Khách đã chuyển khoản: cần hoàn tiền` |
     | Khách hủy, các trường hợp còn lại | `Tồn kho đã tự hoàn lại, không cần xử lý thêm` |

   - Đơn sỉ hoặc đặt trước có thêm dòng `🏷️ ĐƠN SỈ` / `⏳ ĐƠN ĐẶT TRƯỚC`. Hiện mọi đơn đều là đơn lẻ.
   - Không cấu hình `Ordering:AdminBaseUrl` thì bỏ dòng link.
   - Tin dài quá 2000 ký tự (giới hạn của `sendMessage`) bị cắt bớt.
   - Tin là văn bản thường, chưa dùng `parse_mode` (in đậm, ...). Em chưa kiểm chứng được Zalo hiển thị định dạng
     trong group thế nào, và nếu dùng thì phải escape tên/lý do do khách nhập.

## API (method, route, request, response, mã lỗi)

Không thêm API mới. BE gọi ra ngoài:

- `POST {Zalo:BaseUrl}/bot{BotToken}/sendMessage`, body `{"chat_id": "...", "text": "..."}`. Thành công khi HTTP
  2xx và `ok = true`.
- Token nằm trong URL theo quy định của Zalo. Vì vậy logger mặc định của `HttpClient` đã bị tắt
  (`RemoveAllLoggers`), và log của module không bao giờ ghi URL.

## Cấu hình cần thiết

| Key | Mặc định | Ghi chú |
| --- | --- | --- |
| `Zalo:Enabled` | `false` | Bật thì bắt buộc có `BotToken` và `ChatId`, thiếu thì app không khởi động |
| `Zalo:BotToken` | trống | **Secret**: đặt bằng `dotnet user-secrets` / biến môi trường `Zalo__BotToken` |
| `Zalo:ChatId` | trống | Id group nhận tin (cách lấy ở dưới) |
| `Zalo:BaseUrl` | `https://bot-api.zaloplatforms.com` | |
| `Zalo:TimeoutSeconds` | 10 | Timeout mỗi lần gọi Zalo |
| `Ordering:AdminBaseUrl` | trống | Ví dụ `https://admin.sachluat.vn`; link tin nhắn là `{AdminBaseUrl}/don-hang/{mã đơn}` |
| `Ordering:EnableStaffNotificationDispatch` | `true` | Tắt job gửi (test e2e tắt để gọi tay) |
| `Ordering:StaffNotificationPollSeconds` | 10 | |
| `Ordering:StaffNotificationMaxAttempts` | 10 | |

**Cài đặt bot:**

1. Tạo bot trên Zalo Bot Platform (`bot.zaloplatforms.com`) và lấy token dạng `123456789:abc...`.
2. Thêm bot vào group nội bộ và nhắn một tin trong group (ví dụ tag bot).
3. Lấy id group: chạy `curl -X POST https://bot-api.zaloplatforms.com/bot<TOKEN>/getUpdates`, đọc
   `result.message.chat.id` của tin vừa nhắn trong group (`chat.chat_type` là group).
   - `getUpdates` không chạy khi bot đang đặt webhook. Gặp trường hợp đó thì gọi `deleteWebhook` trước.
4. Cấu hình secret rồi khởi động lại API:

   ```bash
   dotnet user-secrets set "Zalo:Enabled" "true" --project src/SachLuat.Api
   dotnet user-secrets set "Zalo:BotToken" "<TOKEN>" --project src/SachLuat.Api
   dotnet user-secrets set "Zalo:ChatId" "<GROUP_CHAT_ID>" --project src/SachLuat.Api
   dotnet user-secrets set "Ordering:AdminBaseUrl" "https://admin.example.com" --project src/SachLuat.Api
   ```

   Trên server, đặt các biến môi trường tương ứng: `Zalo__Enabled`, `Zalo__BotToken`, `Zalo__ChatId`,
   `Ordering__AdminBaseUrl`.

## Thay đổi CSDL (migration liên quan)

`AddOrderNotificationOutbox` (schema `ordering`) tạo bảng `order_notifications` với các cột:

- `order_id`: khóa ngoại sang `orders`, cùng schema.
- `kind`, `status` (`pending`/`sent`/`failed`), `attempts`, `next_attempt_at`, `last_error` (≤ 500 ký tự),
  `created_at`, `sent_at`.

Index lọc `status = 'pending'` theo `next_attempt_at`. Production cần áp migration ở bước deploy.

## Hạn chế / việc còn lại

- **Chưa kiểm chứng được bot có vào group hay không.** Tài liệu chính thức của Zalo Bot chưa mô tả rõ việc gửi
  vào group, và có nguồn bên ngoài báo một số bot không thêm được vào group. Nếu bot không vào được group:
  - Tạm thời đặt `Zalo:ChatId` là id chat 1-1 của một nhân viên với bot.
  - Hoặc đổi kênh bằng cách viết implementation khác của `IStaffNotifier`.
- Tin `failed` (hết số lần thử) chưa có màn hình Admin để xem hoặc gửi lại. Hiện xem bằng log `Error` hoặc truy vấn
  bảng `ordering.order_notifications`.
- Chưa dọn bảng outbox định kỳ. Mỗi đơn có vài dòng, nên chưa cần.
- Chưa có tin nhắn cho sự kiện khác như cảnh báo tồn thấp hay giao thất bại. Muốn thêm thì dùng lại `IStaffNotifier`.
