# Promotion: chương trình khuyến mãi và mã giảm giá

## Mục đích

Marketing tạo và quản lý khuyến mãi (FR-90): giảm trên tổng hóa đơn, giảm hoặc miễn phí ship, tự áp hoặc dùng mã,
có giới hạn thời gian, giá trị đơn, phạm vi sách, số lượt dùng. Khách xem ưu đãi đang chạy (FR-32) và thử mã ở bước
thanh toán. Module cũng quản lý **combo bộ sách** giá cố định (FR-16, mục Combo bên dưới). Module
`SachLuat.Modules.Promotion`, schema `promotion`; module Ordering gọi qua `SachLuat.Modules.Promotion.Contracts`
(`IPromotionService`).

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
  `+84901234567`, `84901234567` là một; dùng chung `PhoneNumbers.Normalize` ở SharedKernel với Ordering). Đổi số
  điện thoại là lách được giới hạn; chấp nhận ở giai đoạn này.

**Mã giảm giá** (`coupons`): thuộc chương trình `mode = coupon`. Mã lưu chữ in hoa, 4–40 ký tự `A–Z 0–9 _ -`, duy nhất
toàn hệ thống, khách nhập không phân biệt hoa thường. Một mã dùng chung (ví dụ `MUATHI2026`, `usageLimit` trống) hoặc
sinh hàng loạt mã ngẫu nhiên dùng một lần (bỏ ký tự dễ nhầm `0 O 1 I L`) để gửi riêng cho từng khách. Tắt mã bằng
`isActive = false`. Mã tắt, không tồn tại, chương trình đã xóa hoặc không phải loại dùng mã đều báo `COUPON_INVALID`.

**Trần giảm giá**: `Promotion:MaxDiscountPercent` (mặc định 50) chặn chương trình giảm trên đơn theo % vượt trần
khi tạo/sửa. Nghị định về khuyến mại giới hạn mức giảm giá hàng hóa và có thủ tục thông báo khuyến mại với Sở Công
Thương; Em ghi theo hiểu biết hiện tại và **chưa kiểm tra bản sửa đổi mới nhất, cần pháp chế xác nhận** trước khi
ra mắt.

**Combo bộ sách** (`Combo`, bảng `combos` và `combo_items`, FR-16): bộ sách **cố định** (2–10 SKU, mỗi SKU kèm số
cuốn trong một bộ) bán với **giá combo** thấp hơn tổng giá lẻ. Ví dụ sách AB gồm tập 1 (A) và tập 2 (B), mua lẻ mỗi
cuốn 50.000đ, mua chung chỉ 70.000đ. Combo nằm ở Promotion vì không có SKU hay tồn kho riêng: bản chất là ưu đãi giá
trên các SKU có sẵn nên Cart, Inventory và dòng đơn **không đổi**.

| Nhóm | Thuộc tính |
| --- | --- |
| Hiển thị | `name`, `slug` (trang `/combo/{slug}`, duy nhất giữa các combo chưa xóa), `description`, `imageUrl` (https) |
| Giá | `comboPrice` (VNĐ nguyên, một bộ), `items[]` = `{skuId, quantity}` |
| Thời gian | `startsAt`, `endsAt` (UTC, `endsAt` là mốc loại trừ, trống = chạy đến khi tắt), `isActive` |

- **Tự nhận diện trong giỏ**: khách thêm lẻ A và B (hoặc bấm "Mua combo", FE chỉ việc thêm lần lượt từng SKU vào giỏ)
  thì giỏ đủ bộ tự được giá combo ở `POST /checkout/preview` và khi tạo đơn. Số bộ =
  `min(floor(số lượng SKU trong giỏ / số cuốn trong một bộ))`; phần dư không được giảm.
  Ví dụ A×2 + B×1 là một bộ, A×2 + B×2 là hai bộ.
- **Điều kiện áp**: `comboPrice ≤ tổng giá hiện hành` của một bộ (giá hiện hành đã gồm lịch giá, xem `catalog.md`): khách
  không bao giờ trả nhiều hơn mua lẻ. Sách đang sale mạnh khiến giá lẻ thấp hơn giá combo thì combo không áp.
- **Mức giảm một bộ hiển thị** = `tổng giá niêm yết (giá bìa) − comboPrice` (ví dụ hai tập 550.000đ, combo 575.000đ:
  giảm 525.000đ). Trong đó `tổng giá hiện hành − comboPrice` là giảm **thật** (ví dụ 580.000 − 575.000 = 5.000đ), phần còn
  lại là **chênh lệch niêm yết** (`PromotionEvaluation.ListMarkups`, theo SKU) = `giá niêm yết − giá hiện hành`. Phần
  chênh lệch chỉ tính cho cuốn được các bộ **phủ trọn** (không còn cuốn lẻ dư của SKU đó); cuốn dư lẻ giữ giá hiện hành.
  Ordering cộng chênh lệch vào tiền dòng và tạm tính (dòng, `order_items.unit_price` theo giá niêm yết) và nó cũng nằm
  trong `orderDiscount`/`lineDiscounts`, nên **số khách phải trả không đổi** so với tính theo giá hiện hành
  (`1.100.000 − 525.000 − 25.000 = 580.000 − 5.000 − 25.000`). Không truyền `ListPrice` (null) thì không có chênh lệch
  (hành vi cũ).
- **Cộng dồn với chương trình giảm trên đơn**: combo tính trước, rồi chương trình giảm trên đơn (tự áp hoặc mã, vẫn
  chọn một chương trình giảm nhiều nhất) tính tiếp trên **số tiền còn lại của từng dòng sau khi trừ phần giảm của
  combo** (phần dòng thuộc phạm vi chương trình). `minOrderAmount` vẫn xét trên tạm tính trước giảm giá. Tổng giảm
  (`orderDiscount`) = giảm combo + giảm chương trình; phần chương trình phân bổ về dòng nên không dòng nào bị giảm quá
  tiền của dòng. Mã giảm giá hợp lệ vẫn dùng được cùng combo. Giảm phí ship áp độc lập như thường.
- **Trần tổng mức giảm** `Promotion:MaxTotalDiscountPercent` (mặc định 50): với dòng thuộc combo, tổng giảm (combo +
  chương trình) không vượt phần trăm này của **giá hiện hành** (`PromotionLine.UnitPrice`, đã áp lịch giá). Phần giảm
  combo trong trần là giảm **thật** theo giá hiện hành (không gồm chênh lệch niêm yết). Chỉ chặn phần giảm của chương
  trình (combo không bị cắt, vì combo đã có trần riêng lúc tạo/sửa); dòng không có combo không bị chặn nên chương trình
  hoạt động như cũ. Hết chỗ giảm thì chương trình tự áp bị bỏ qua êm, còn mã khách nhập báo `COUPON_NOT_APPLICABLE`.
  Ví dụ hai tập hiện hành 290.000đ/cuốn, combo 575.000đ (giảm thật 5.000đ), mỗi cuốn còn được giảm thêm tối đa 142.500đ.
  Trần này cùng lưu ý pháp lý như `MaxDiscountPercent`: cần pháp chế xác nhận.
- Nhiều combo chung SKU: xét **tham lam**, combo giảm nhiều mỗi bộ lấy sách trước, hòa thì combo tạo sớm hơn.
- Tiền giảm phân bổ về từng dòng bằng cùng hàm `Allocate` của chương trình, tính trên phần sách thuộc từng combo.
- Kết quả: `AppliedPromotion` có `kind = combo`, `promotionId` là **id combo**, `couponId` null (mỗi combo một phần tử).
  Combo **không** ghi `promotion_redemptions`: `RedeemAsync` bỏ qua, `ReleaseAsync` không cần làm gì.
- Khi tạo/sửa, `ComboAdminService` kiểm tra qua `ICatalogSkuReader` (Contracts của Catalog): SKU phải tồn tại
  (`REFERENCE_NOT_FOUND`), `comboPrice` **không được cao hơn tổng giá hiện hành** (sau giảm, đã áp lịch giá) của các
  sách và không giảm quá `Promotion:MaxDiscountPercent` so với giá bán thường (cùng trần và cùng lưu ý pháp lý với
  chương trình giảm trên đơn). Admin response có thêm `currentTotal` (mức tối đa của giá combo) và `currentPrice` từng dòng.
- Trang khách: danh sách ẩn combo có sách ngừng bán; trang chi tiết vẫn mở kèm `isSellable = false` để FE tắt nút mua.
  Sách trong bộ không còn ở Catalog thì combo coi như không tồn tại.
- Xóa là **xóa mềm** (`deleted_at`); slug của combo đã xóa dùng lại được.

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

**Combo** (admin cùng policy `pricing_admin`):

| Method | Route | Xác thực | Request → Response |
| --- | --- | --- | --- |
| GET | `/combos?skuId=` | Không | → `ComboResponse[]` (đang chạy, mới nhất trước, tối đa 50, không phân trang; `skuId` chỉ lấy combo có SKU đó; ẩn combo có sách ngừng bán) |
| GET | `/combos/{slug}` | Không | → `ComboResponse` (404 `NOT_FOUND` khi combo không chạy/không tồn tại) |
| GET | `/admin/combos?q=&page=&limit=` | pricing_admin | → `PagedResponse<AdminComboResponse>` (`q` khớp tên hoặc slug) |
| GET | `/admin/combos/{id}` | pricing_admin | → `AdminComboResponse` |
| POST | `/admin/combos` | pricing_admin | `UpsertComboRequest` → 201 `AdminComboResponse` |
| PUT | `/admin/combos/{id}` | pricing_admin | `UpsertComboRequest` → `AdminComboResponse` |
| DELETE | `/admin/combos/{id}` | pricing_admin | → 204 (xóa mềm) |

`ComboResponse` (khách): `{id, name, slug, description, imageUrl, comboPrice, retailPrice, listPrice, savings, items[],
isSellable, startsAt, endsAt}`; `retailPrice` là tổng giá **hiện hành** (đã áp lịch giá) của một bộ nếu mua lẻ,
`listPrice` là tổng **giá bìa** (giá gốc, không đổi khi có lịch giá) để FE hiện giá cũ gạch ngang, `savings = max(0,
retailPrice − comboPrice)` (0 nghĩa là combo không còn lợi nên không được áp, xem Quy tắc giá ở trên); mỗi item: `{skuId, skuCode, bookTitle, bookSlug, editionName, coverType, coverImageUrl, quantity,
unitPrice, listPrice, isSellable}` (`unitPrice` là giá hiện hành của một cuốn, `listPrice` là giá bìa/giá gốc của một cuốn). `AdminComboResponse`: các trường của request kèm `regularTotal` (tổng giá bán thường một bộ,
`null` nếu có SKU không còn ở Catalog), `status` (`inactive`, `scheduled`, `running`, `ended`), `items[]` =
`{skuId, quantity, skuCode, bookTitle, regularSalePrice}` và `createdAt`, `updatedAt`.

Payload mẫu cho ví dụ sách AB (SKU 11 là tập 1, SKU 12 là tập 2, mỗi cuốn bán thường 50.000đ):

```json
{
  "name": "Combo trọn bộ sách AB",
  "slug": "combo-tron-bo-sach-ab",
  "description": "Trọn bộ hai tập, tiết kiệm 30.000đ so với mua lẻ.",
  "imageUrl": null,
  "comboPrice": 70000,
  "items": [
    { "skuId": 11, "quantity": 1 },
    { "skuId": 12, "quantity": 1 }
  ],
  "startsAt": "2026-11-01T00:00:00+07:00",
  "endsAt": null,
  "isActive": true
}
```

Mua combo trên FE: gọi `POST /cart/items` lần lượt cho từng item (số lượng = `quantity` của item). Giá combo hiện ở
`POST /checkout/preview` dưới dạng một phần tử `appliedPromotions` có `kind = combo`.

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
tự áp), `PROMOTION_UNAVAILABLE` (422, chương trình tự áp hết lượt/hết hạn giữa lúc đặt hàng), `COMBO_SLUG_DUPLICATE` (409),
`REFERENCE_NOT_FOUND` (422, SKU trong combo không tồn tại). Giá combo không thấp hơn tổng giá bán thường hoặc giảm quá
trần `Promotion:MaxDiscountPercent` trả `VALIDATION_ERROR` (400) kèm lý do.

## Cấu hình cần thiết

| Key | Mặc định |
| --- | --- |
| `Promotion:MaxDiscountPercent` | 50 (trần % cho chương trình giảm trên đơn và cho mức giảm của combo so với giá bán thường, xem lưu ý pháp lý ở trên) |
| `Promotion:MaxTotalDiscountPercent` | 50 (trần tổng giảm combo + chương trình trên giá bán thường của dòng có combo, xem mục Combo; cần pháp chế xác nhận) |
| `Promotion:MaxCouponsPerBatch` | 1000 (số mã tối đa mỗi lần sinh) |

## Thay đổi CSDL (migration liên quan)

Migration `CreatePromotionSchema` (schema `promotion`):

| Bảng | Ghi chú |
| --- | --- |
| `promotions` | enum dạng chuỗi snake_case; tiền `numeric(12,0)`; check `discount_value > 0`, giới hạn > 0, `ends_at > starts_at`; xóa mềm |
| `promotion_targets` | id sách/SKU/danh mục theo `scope_type`; unique `(promotion_id, target_id)`; **không** khóa ngoại sang Catalog |
| `coupons` | unique `code`; khóa ngoại về `promotions` (cùng schema) |
| `promotion_redemptions` | unique `(reference, promotion_id)` để idempotent; index `(promotion_id, customer_key)` lọc `status = 'redeemed'`; `reference` là mã đơn, không khóa ngoại sang Ordering |

Migration `AddCombos` (chỉ thêm bảng, không đổi dữ liệu cũ):

| Bảng | Ghi chú |
| --- | --- |
| `combos` | `combo_price numeric(12,0)`; check `combo_price > 0`, `ends_at > starts_at`; unique `slug` (index `ux_combos_slug`, chỉ với dòng chưa xóa mềm); xóa mềm |
| `combo_items` | unique `(combo_id, sku_id)`, check `quantity > 0`, index `sku_id`; khóa ngoại về `combos` (cascade); **không** khóa ngoại sang `catalog.skus` |

Catalog không đổi schema nhưng `ICatalogSkuReader` trả thêm `RegularSalePrice` và `CategoryIds` (gồm danh mục cha).
Production tắt `Database:ApplyMigrationsOnStartup`, cần áp migration này ở bước deploy.

## Hạn chế / việc còn lại

- Chưa có đối tượng "đơn đầu tiên" và nhóm khách hàng (FR-85), flash sale, tặng quà.
- Combo: chưa có giới hạn lượt dùng, đối tượng khách hay thống kê riêng (không ghi `promotion_redemptions`); chỉ có
  bộ sách cố định, chưa có kiểu "mua N cuốn bất kỳ trong nhóm"; `imageUrl` chỉ kiểm tra là link https, chưa ràng buộc
  link Cloudinary như ảnh bìa (xem `catalog.md`) và chưa có API ký upload riêng cho ảnh combo.
- Giỏ hàng chưa hiện giá combo: khách chỉ thấy ở bước xem trước thanh toán (`POST /checkout/preview`); thêm combo vào
  giỏ là nhiều lệnh `POST /cart/items` không nguyên tử (một SKU hết hàng thì các SKU khác đã vào giỏ).
- Nhiều combo chung SKU tính theo tham lam, chưa tìm phương án tối ưu tuyệt đối (hiếm gặp).
- Đơn đã áp combo không hiển thị riêng giá combo trên dòng đơn, chỉ có `discountAmount` đã phân bổ về từng dòng.
- Chưa có chương trình "Đổi sách cũ – Lấy bản mới" tự nhận diện khách đủ điều kiện (BR-30): hiện chỉ có sinh mã riêng
  hàng loạt để gửi tay; chưa có tự động gửi mã (chờ module Notification).
- `targetIds` không được kiểm tra tồn tại vì Catalog là module khác; id sai thì chương trình không áp dụng cho sách nào.
- Một đơn chỉ nhận một chương trình giảm trên đơn và một chương trình giảm ship (không cộng dồn các chương trình cùng
  loại); riêng combo thì cộng dồn với chương trình giảm trên đơn (xem mục Combo).
- Báo cáo hiệu quả (FR-95) mới có `stats` theo chương trình (số lượt, tổng tiền giảm); chưa có doanh thu mang lại,
  chưa xuất Excel.
- Chưa ghi nhật ký thao tác (FR-98) cho tạo/sửa/xóa chương trình, hiện chỉ ghi log ứng dụng.
- Lượt dùng chỉ được trả khi hủy đơn, chưa trả khi hoàn hàng/hoàn tiền.
- Mã dùng chung có thể bị lan truyền công khai; muốn chặn thì đặt `usageLimit`/`perCustomerLimit`.
