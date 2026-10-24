# Promotion: chương trình khuyến mãi và mã giảm giá

## Mục đích

Marketing tạo và quản lý khuyến mãi (FR-90): giảm trên tổng hóa đơn, giảm hoặc miễn phí ship, tự áp hoặc dùng mã,
có giới hạn thời gian, giá trị đơn, phạm vi sách, số lượt dùng. Khách xem ưu đãi đang chạy (FR-32) và thử mã ở bước
thanh toán. Module `SachLuat.Modules.Promotion`, schema `promotion`; module Ordering gọi qua
`SachLuat.Modules.Promotion.Contracts` (`IPromotionService`).

Ví dụ "Giảm ngay 20% trên tổng hóa đơn cho tất cả các đầu sách pháp luật chính hãng, áp dụng đến hết 31/12/2026"
là **một chương trình** giảm trên đơn, tự áp, 20%, mọi sách (payload mẫu ở mục API). Các câu quảng cáo như "Giao hàng
nhanh toàn quốc" chỉ là nội dung mô tả; hiển thị bằng popup/banner (xem `docs/features/popup.md`) trỏ về
`/khuyen-mai`. Nội dung quảng cáo phải khớp thời gian giao thực tế.

## Luồng xử lý

**Một chương trình** (`PromotionCampaign`, bảng `promotions`):

| Nhóm | Thuộc tính |
| --- | --- |
| Hiển thị | `name` (nội bộ), `title`, `description` (cho khách), `isPublic` (có hiện ở trang khuyến mãi không) |
| Loại | `kind`: `order_discount` (giảm trên tổng đơn) hoặc `free_shipping` (giảm phí ship) |
| Cách áp | `mode`: `automatic` (tự áp) hoặc `coupon` (khách nhập mã) |
| Mức giảm | `discountType`: `percent` (1–100) hoặc `fixed_amount` (VNĐ); `maxDiscountAmount` là trần số tiền khi giảm theo % |
| Điều kiện | `minOrderAmount` (tạm tính cả đơn, trước giảm giá); `scopeType`: `all`, `books`, `skus`, `categories` kèm `targetIds`; `excludeOnSaleItems`; `audience`: `everyone`, `members_only` |
| Giới hạn | `usageLimit` (tổng lượt), `perCustomerLimit` (lượt mỗi khách); `usedCount` do hệ thống tính |
| Thời gian | `startsAt`, `endsAt` (UTC, `endsAt` là mốc loại trừ, trống = chạy đến khi tắt), `isActive`, `priority` |

- `status` tính lúc trả về, không lưu: `inactive`, `scheduled`, `running`, `ended`, `exhausted`.
- Phạm vi `categories` gồm cả sách thuộc danh mục con (Catalog trả kèm danh mục cha của sách).
- `excludeOnSaleItems`: bỏ qua sách **đang chạy lịch giá** (giá hiện hành thấp hơn giá bán thường, xem
  `docs/features/catalog.md`), tránh giảm chồng lên giảm. Sách chỉ có giá bán thấp hơn giá bìa vẫn được tính.
- `free_shipping` luôn áp cho cả đơn: phạm vi phải là `all` và `excludeOnSaleItems = false`.
- Xóa là **xóa mềm** (`deleted_at`); chương trình đã xóa không áp dụng, lượt dùng cũ vẫn được trả khi hủy đơn.

**Chọn ưu đãi khi tính tiền** (`PromotionEvaluator`, hàm thuần, có test):

1. Ứng viên: mọi chương trình `automatic` đang chạy, cộng chương trình của mã khách nhập (nếu có).
2. Mỗi ứng viên được kiểm tra: đã bắt đầu, chưa hết hạn/hết lượt, đối tượng, giới hạn mỗi khách, đơn tối thiểu, có
   sách thuộc phạm vi. Chương trình **tự áp** không đủ điều kiện thì bị bỏ qua êm; **mã khách nhập** không dùng được
   thì báo lỗi rõ lý do.
3. Chọn **tối đa một chương trình giảm trên đơn và một chương trình giảm phí ship**, mỗi loại lấy chương trình giảm
   nhiều nhất cho khách. Hòa thì ưu tiên mã khách đã nhập, rồi `priority` cao hơn, rồi chương trình tạo sớm hơn.
   Không cộng dồn nhiều chương trình cùng loại.
4. Số tiền: `percent` = `floor(cơ sở × %)` (đồng nguyên, làm tròn xuống), không vượt `maxDiscountAmount`;
   `fixed_amount` = số tiền; luôn không vượt cơ sở (phần tạm tính thuộc phạm vi, hoặc phí ship).
5. Mức giảm trên đơn được **phân bổ về từng dòng** theo tỷ lệ tiền, làm tròn xuống, phần dư (vài đồng) cộng cho dòng
   có tiền lớn nhất để tổng khớp đúng.
6. Mã hợp lệ nhưng có chương trình tự áp lợi hơn: không lỗi, mã **không** được dùng, `couponApplied = false`
   (không trừ lượt của mã).

**Ghi nhận và trả lượt dùng** (`IPromotionService`, idempotent theo mã đơn):

- `EvaluateAsync` chỉ tính, không ghi gì (dùng cho xem trước và tạo đơn).
- `RedeemAsync` khi tạo đơn: trong một transaction, khóa dòng chương trình (`FOR UPDATE`), kiểm tra lại đang chạy,
  còn lượt, mã còn lượt, giới hạn mỗi khách rồi ghi `promotion_redemptions` và tăng bộ đếm. Hai đơn tranh lượt cuối
  cùng thì chỉ một đơn thắng, đơn còn lại nhận `COUPON_EXPIRED` (mã) hoặc `PROMOTION_UNAVAILABLE` (tự áp) và
  Ordering tự trả tồn đã giữ. Gọi lại cùng mã đơn không tính trùng.
- `ReleaseAsync` khi hủy đơn (khách hủy hoặc job tự hủy quá hạn thanh toán) hoặc khi lưu đơn lỗi: đổi lượt sang
  `released`, giảm bộ đếm; gọi lại không giảm hai lần. Lượt đã trả dùng lại được cho đơn khác.
- Giới hạn mỗi khách: thành viên tính theo tài khoản, khách vãng lai theo số điện thoại đã chuẩn hóa (`0901234567`,
  `+84901234567`, `84901234567` là một). Đổi số điện thoại là lách được giới hạn; chấp nhận ở giai đoạn này.

**Mã giảm giá** (`coupons`): thuộc chương trình `mode = coupon`. Mã lưu chữ in hoa, 4–40 ký tự `A–Z 0–9 _ -`, duy nhất
toàn hệ thống, khách nhập không phân biệt hoa thường. Một mã dùng chung (ví dụ `MUATHI2026`, `usageLimit` trống) hoặc
sinh hàng loạt mã ngẫu nhiên dùng một lần (bỏ ký tự dễ nhầm `0 O 1 I L`) để gửi riêng cho từng khách. Tắt mã bằng
`isActive = false`. Mã tắt, không tồn tại, chương trình đã xóa hoặc không phải loại dùng mã đều báo `COUPON_INVALID`.

**Trần giảm giá**: `Promotion:MaxDiscountPercent` (mặc định 50) chặn chương trình giảm trên đơn theo % vượt trần
khi tạo/sửa. Nghị định về khuyến mại giới hạn mức giảm giá hàng hóa và có thủ tục thông báo khuyến mại với Sở Công
Thương; Em ghi theo hiểu biết hiện tại và **chưa kiểm tra bản sửa đổi mới nhất, cần pháp chế xác nhận** trước khi
ra mắt.

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1`. Admin dùng policy `pricing_admin` (`super_admin`, `product_manager`, `marketing`, bắt buộc 2FA).
Enum trong JSON dạng snake_case.

| Method | Route | Xác thực | Request → Response |
| --- | --- | --- | --- |
| GET | `/promotions/active` | Không | → `PublicPromotionResponse[]` (đang chạy và `isPublic`, tối đa 20, không có mã giảm giá) |
| GET | `/admin/promotions?q=&page=&limit=` | pricing_admin | → `PagedResponse<AdminPromotionResponse>` (kèm `targetIds`, `couponCount`, `status`) |
| GET | `/admin/promotions/{id}` | pricing_admin | → `AdminPromotionResponse` |
| POST | `/admin/promotions` | pricing_admin | `UpsertPromotionRequest` → 201 |
| PUT | `/admin/promotions/{id}` | pricing_admin | `UpsertPromotionRequest` → `AdminPromotionResponse` |
| DELETE | `/admin/promotions/{id}` | pricing_admin | → 204 (xóa mềm) |
| GET | `/admin/promotions/{id}/stats` | pricing_admin | → `{promotionId, usedCount, releasedCount, totalOrderDiscount, totalShippingDiscount, uniqueCustomers}` |
| GET | `/admin/promotions/{id}/coupons?page=&limit=` | pricing_admin | → `PagedResponse<CouponResponse>` |
| POST | `/admin/promotions/{id}/coupons` | pricing_admin | `{code, usageLimit?}` → 201 `CouponResponse` |
| POST | `/admin/promotions/{id}/coupons/generate` | pricing_admin | `{count, prefix?, usageLimit?}` (`usageLimit` trống = 1) → 201 `CouponResponse[]` |
| PUT | `/admin/coupons/{id}` | pricing_admin | `{isActive, usageLimit?}` → `CouponResponse` |

Phía khách dùng `POST /checkout/preview` và `couponCode` ở `POST /orders` (xem `docs/features/ordering.md`).

Payload mẫu cho ví dụ giảm 20% (giờ Việt Nam, kết thúc đúng 0h ngày 01/01/2027 tức hết ngày 31/12/2026):

```json
{
  "name": "Sale cuối năm 2026",
  "title": "Giảm ngay 20% trên tổng hóa đơn",
  "description": "Cho tất cả các đầu sách pháp luật chính hãng. Chương trình áp dụng đến hết ngày 31/12/2026.",
  "kind": "order_discount",
  "mode": "automatic",
  "discountType": "percent",
  "discountValue": 20,
  "maxDiscountAmount": null,
  "minOrderAmount": 0,
  "scopeType": "all",
  "targetIds": [],
  "excludeOnSaleItems": false,
  "audience": "everyone",
  "usageLimit": null,
  "perCustomerLimit": null,
  "startsAt": "2026-11-01T00:00:00+07:00",
  "endsAt": "2027-01-01T00:00:00+07:00",
  "isActive": true,
  "priority": 10,
  "isPublic": true
}
```

Mã lỗi: `VALIDATION_ERROR` (400), `NOT_FOUND` (404), `COUPON_INVALID`, `COUPON_EXPIRED` (hết hạn, hết lượt, hoặc khách đã
dùng hết lượt của mã), `COUPON_MIN_ORDER`, `COUPON_NOT_APPLICABLE` (giỏ không có sách thuộc phạm vi),
`COUPON_LOGIN_REQUIRED` (đều 422), `COUPON_CODE_DUPLICATE` (409), `PROMOTION_NOT_COUPON` (422, thêm mã cho chương trình
tự áp), `PROMOTION_UNAVAILABLE` (422, chương trình tự áp hết lượt/hết hạn giữa lúc đặt hàng).

## Cấu hình cần thiết

| Key | Mặc định |
| --- | --- |
| `Promotion:MaxDiscountPercent` | 50 (trần % cho chương trình giảm trên đơn, xem lưu ý pháp lý ở trên) |
| `Promotion:MaxCouponsPerBatch` | 1000 (số mã tối đa mỗi lần sinh) |

## Thay đổi CSDL (migration liên quan)

Migration `CreatePromotionSchema` (schema `promotion`):

| Bảng | Ghi chú |
| --- | --- |
| `promotions` | enum dạng chuỗi snake_case; tiền `numeric(12,0)`; check `discount_value > 0`, giới hạn > 0, `ends_at > starts_at`; xóa mềm |
| `promotion_targets` | id sách/SKU/danh mục theo `scope_type`; unique `(promotion_id, target_id)`; **không** khóa ngoại sang Catalog |
| `coupons` | unique `code`; khóa ngoại về `promotions` (cùng schema) |
| `promotion_redemptions` | unique `(reference, promotion_id)` để idempotent; index `(promotion_id, customer_key)` lọc `status = 'redeemed'`; `reference` là mã đơn, không khóa ngoại sang Ordering |

Catalog không đổi schema nhưng `ICatalogSkuReader` trả thêm `RegularSalePrice` và `CategoryIds` (gồm danh mục cha).
Production tắt `Database:ApplyMigrationsOnStartup`, cần áp migration này ở bước deploy.

## Hạn chế / việc còn lại

- Chưa có đối tượng "đơn đầu tiên" và nhóm khách hàng (FR-85), flash sale, combo (FR-16), tặng quà.
- Chưa có chương trình "Đổi sách cũ – Lấy bản mới" tự nhận diện khách đủ điều kiện (BR-30): hiện chỉ có sinh mã riêng
  hàng loạt để gửi tay; chưa có tự động gửi mã (chờ module Notification).
- `targetIds` không được kiểm tra tồn tại vì Catalog là module khác; id sai thì chương trình không áp dụng cho sách nào.
- Một đơn chỉ nhận một chương trình giảm trên đơn và một chương trình giảm ship, chưa có cờ cộng dồn.
- Báo cáo hiệu quả (FR-95) mới có `stats` theo chương trình (số lượt, tổng tiền giảm); chưa có doanh thu mang lại,
  chưa xuất Excel.
- Chưa ghi nhật ký thao tác (FR-98) cho tạo/sửa/xóa chương trình, hiện chỉ ghi log ứng dụng.
- Lượt dùng chỉ được trả khi hủy đơn, chưa trả khi hoàn hàng/hoàn tiền.
- Mã dùng chung có thể bị lan truyền công khai; muốn chặn thì đặt `usageLimit`/`perCustomerLimit`.
