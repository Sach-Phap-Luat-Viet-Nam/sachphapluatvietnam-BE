# Shipping: vùng giao hàng và phí vận chuyển

## Mục đích

Tính và quản lý phí vận chuyển (FR-34, FR-99, BR-14, mục 12.2): phí theo tỉnh/thành và trọng lượng đơn, thời gian
giao dự kiến, miễn phí ship từ một mức tạm tính. Admin tự sửa biểu phí và ngưỡng miễn ship trong CSDL, không cần
deploy lại. Module `SachLuat.Modules.Shipping`, schema `shipping`; module Ordering gọi qua
`SachLuat.Modules.Shipping.Contracts` (`IShippingQuoteService`).

## Luồng xử lý

1. **Trọng lượng đơn** = Σ (`weight_gram` của ấn bản × số lượng) + `packagingWeightGram` (mặc định 100 g, mục 12.2).
   Ấn bản chưa nhập trọng lượng dùng `defaultBookWeightGram` (mặc định 400 g).
2. **Vùng giao hàng** (`shipping_zones`): nhóm tỉnh/thành dùng chung một biểu phí. Mỗi tỉnh chỉ thuộc **một** vùng.
   Tên tỉnh so khớp không phân biệt hoa thường, dấu và tiền tố hành chính ("Thành phố Hà Nội", "TP. Hà Nội",
   "ha noi" cùng khớp; `ProvinceKey`).
3. **Biểu phí** của vùng: `baseFee` cho `baseWeightGram` gram đầu, mỗi bước `stepWeightGram` gram tiếp theo (làm tròn
   lên, vượt 1 gram vẫn tính trọn một bước) cộng `stepFee`; kèm `etaMinDays`–`etaMaxDays` ngày giao dự kiến.
   Ví dụ `baseFee = 20.000` cho 1.000 g đầu, `stepFee = 5.000` mỗi 500 g: đơn 1.900 g trả 30.000đ.
4. **Miễn phí theo ngưỡng** (BR-14): tạm tính ≥ `freeShippingThreshold` thì phí khách trả = 0. Ngưỡng so với tạm tính
   **trước** giảm giá, nên chương trình giảm giá không làm khách mất freeship. `freeShippingThreshold = null` tắt
   tính năng. Báo giá luôn trả `baseFee` (theo biểu giá) để biết phần doanh nghiệp bù, và `amountToFreeShipping` (còn
   thiếu bao nhiêu để được miễn ship) cho FE gợi ý "mua thêm … để được freeship".
5. **Chưa chọn tỉnh** hoặc **chưa cấu hình vùng nào**: dùng phí cố định `flatFee` (hành vi cũ, để hệ thống chạy được
   ngay sau khi nâng cấp). **Đã có vùng đang bật** mà tỉnh không thuộc vùng nào: 422 `ADDRESS_NOT_SUPPORTED`.
6. **Cấu hình** (`shipping_settings`, đúng một dòng): khi Admin chưa lưu lần nào thì dùng giá trị mặc định từ cấu hình
   ứng dụng (`Shipping:*`); `GET /admin/shipping/settings` trả `isDefault = true`. Từ lần lưu đầu, CSDL là nguồn duy nhất.

Báo giá được Ordering dùng ở `POST /shipping/quote`, `POST /checkout/preview` và lúc tạo đơn; phí lưu vào đơn
(`shipping_fee`, `shipping_base_fee`).

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1/admin/shipping`, policy `shipping_admin` (`super_admin`, `order_manager`, Super Admin bắt buộc 2FA).
Phía khách dùng `POST /shipping/quote` (xem `docs/features/ordering.md`).

| Method | Route | Request → Response |
| --- | --- | --- |
| GET | `/zones` | → `ZoneResponse[]` theo `sortOrder` |
| POST | `/zones` | `UpsertZoneRequest` → 201 `ZoneResponse` |
| PUT | `/zones/{id}` | `UpsertZoneRequest` → `ZoneResponse` |
| DELETE | `/zones/{id}` | → 204 (xóa luôn danh sách tỉnh của vùng) |
| GET | `/settings` | → `{freeShippingThreshold, packagingWeightGram, defaultBookWeightGram, flatFee, isDefault}` |
| PUT | `/settings` | `{freeShippingThreshold?, packagingWeightGram, defaultBookWeightGram, flatFee}` → như trên |

```json
{
  "name": "Nội thành HN/HCM",
  "isActive": true,
  "sortOrder": 1,
  "provinces": ["Hà Nội", "Hồ Chí Minh"],
  "baseWeightGram": 1000,
  "baseFee": 20000,
  "stepWeightGram": 500,
  "stepFee": 5000,
  "etaMinDays": 1,
  "etaMaxDays": 2
}
```

Tiền là số nguyên VNĐ. Mã lỗi: `VALIDATION_ERROR` (400), `NOT_FOUND` (404), `SHIPPING_PROVINCE_DUPLICATE` (409, tỉnh
đã thuộc vùng khác), `ADDRESS_NOT_SUPPORTED` (422, ở API báo giá phía khách).

## Cấu hình cần thiết

Giá trị mặc định khi Admin chưa lưu cấu hình (xem `appsettings.Example.json`):

| Key | Mặc định |
| --- | --- |
| `Shipping:DefaultFlatFee` | 30000 (thay `Ordering:ShippingFlatFee`) |
| `Shipping:DefaultFreeShippingThreshold` | 300000 (thay `Ordering:FreeShippingThreshold`, BR-14 giả định) |
| `Shipping:DefaultPackagingWeightGram` | 100 |
| `Shipping:DefaultBookWeightGram` | 400 |

## Thay đổi CSDL (migration liên quan)

Migration `CreateShippingSchema` (schema `shipping`): `shipping_zones` (check trọng lượng/phí/ETA hợp lệ),
`shipping_zone_provinces` (khóa ngoại về vùng, xóa theo; unique `province_key` toàn hệ thống),
`shipping_settings` (check `id = 1`). Không có dữ liệu khởi tạo; hệ thống chạy bằng giá trị mặc định đến khi Admin lưu.
Production tắt `Database:ApplyMigrationsOnStartup`, cần áp migration này ở bước deploy.

## Hạn chế / việc còn lại

- Chưa gọi API đơn vị vận chuyển (GHN/GHTK/Viettel Post) để lấy giá thật; chưa tạo vận đơn (FR-71), chưa có bảng
  `shipments`, nên chưa có phí thực trả để đối soát với phí thu của khách (FR-74) và chưa cho khách chọn đơn vị vận chuyển.
- Bảng giá mới theo tỉnh/thành (địa chỉ hiện chỉ có tỉnh + phường/xã), chưa theo quận/huyện. Tỉnh nhập tay sai chính
  tả (không phải khác dấu/hoa thường) sẽ không khớp vùng; FE nên chọn tỉnh từ danh sách chuẩn.
- Mức phí từng vùng và ngưỡng miễn ship thật cần chủ dự án chốt (câu hỏi mở mục 16.4).
- Trọng lượng nằm ở ấn bản (`editions.weight_gram`); cần nhập cho toàn bộ ấn bản hiện có, nếu không phí tính theo
  trọng lượng mặc định 400 g mỗi cuốn.
- Chưa có nhật ký thay đổi biểu phí (FR-98), hiện chỉ ghi log ứng dụng.
