# Inventory: tồn kho, phiếu nhập, lô in

## Mục đích

Quản lý tồn kho theo lô in (FR-80, FR-81, BR-50): nhập kho qua phiếu có duyệt (US-05), tạm giữ khi đặt hàng,
trừ tồn khi đóng gói theo FIFO (BR-11, BR-13), thẻ kho từng SKU, cảnh báo tồn thấp.
Module `SachLuat.Modules.Inventory`, schema `inventory`.

## Luồng xử lý

- **Bất biến**: `available + reserved` = tổng `remaining` các lô in của SKU. DB có check constraint
  `available >= 0`, `reserved >= 0`, `0 <= remaining <= quantity`.
- **Phiếu nhập** (US-05):
  1. Thủ kho lập phiếu nháp: nhiều dòng (SKU, mã lô, xưởng in, số lượng, giá vốn/cuốn, ngày nhập).
  2. Super Admin duyệt (**được duyệt cả phiếu do chính mình lập**) → tạo lô in, cộng `available`, ghi thẻ kho
     `import` với reference `GR-{id}`. Hoặc từ chối kèm lý do.
  3. Duyệt có khóa dòng phiếu: hai người bấm duyệt cùng lúc không nhập kho hai lần.
  4. **Phiếu nháp sửa và xóa được** (`PUT`, `DELETE /receipts/{id}`) bởi người lập hoặc Super Admin: sửa là thay
     ghi chú và toàn bộ dòng (kiểm tra SKU, mã lô như lúc lập); xóa là xóa hẳn phiếu và các dòng vì chưa ảnh hưởng
     tồn kho. Phiếu đã duyệt hoặc từ chối giữ lại làm chứng từ, không sửa hoặc xóa (`RECEIPT_NOT_DRAFT`).
- **Tìm SKU để lập phiếu** (`GET /sku-search?q=`): thủ kho và Super Admin tìm theo một phần tên sách, mã SKU hoặc ISBN
  (không phân biệt hoa thường), tối đa `limit` (mặc định 8, tối đa 20) kết quả kèm tồn khả dụng. Đi qua
  `ICatalogSkuReader.SearchSkusAsync` nên không cần cấp quyền đọc Catalog cho thủ kho.
- **Tên hiển thị**: phiếu nhập, dòng phiếu và thẻ kho trả thêm tên sách, ấn bản, loại bìa và tên người
  lập/duyệt/thao tác (lấy qua `IUserNameReader` của Auth; chưa có họ tên thì dùng email).
- **Đơn hàng** (gọi qua `IInventoryService`, idempotent theo mã đơn):
  - Tạo đơn → `ReserveAsync`: câu `UPDATE ... WHERE available >= qty` cho từng SKU trong một transaction;
    thiếu bất kỳ SKU nào thì không giữ gì (`OUT_OF_STOCK`).
  - Hủy trước đóng gói → `ReleaseAsync`: trả `reserved` về `available`.
  - Đóng gói → `DeductAsync`: khóa các lô còn hàng (`FOR UPDATE`), xuất lô cũ nhất trước, ghi thẻ kho `sale`
    theo từng lô, trả giá vốn bình quân/cuốn cho đơn.
  - Hoàn hàng về kho → `RestockAsync`: trả đúng về các lô đã xuất, ghi thẻ kho `return`.
- **Xuất thủ công** (biếu tặng `gift`, hủy hàng lỗi `damage`): bắt buộc lý do, xuất FIFO.
- **Tồn thấp**: SKU có `available <= low_stock_threshold` (mặc định 50).

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1/admin/inventory`, policy `inventory_admin` (trừ duyệt/từ chối: `inventory_approve`).

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/receipts?status=draft&page=&limit=` | Danh sách phiếu nhập |
| GET | `/receipts/{id}` | Chi tiết phiếu |
| POST | `/receipts` | `{note?, lines: [{skuId, batchCode, printerName?, quantity, unitCost, receivedAt}]}` → 201 |
| PUT | `/receipts/{id}` | Sửa phiếu nháp, cùng body với `POST /receipts` (người lập hoặc Super Admin) |
| DELETE | `/receipts/{id}` | Xóa phiếu nháp → 204 (người lập hoặc Super Admin) |
| GET | `/sku-search?q=&limit=` | Tìm SKU: `[{skuId, skuCode, bookTitle, editionName, coverType, isbn, isLatestEdition, available}]` |
| POST | `/receipts/{id}/approve` | Duyệt (Super Admin) |
| POST | `/receipts/{id}/reject` | `{reason}` |
| GET | `/skus/{skuId}` | Thẻ kho: `skuCode, bookTitle, editionName, coverType, available, reserved, onHand, lowStockThreshold, batches[], recentTransactions[]` (mỗi giao dịch có `createdByName`) |
| PUT | `/skus/{skuId}/threshold` | `{lowStockThreshold}` |
| GET | `/low-stock` | SKU tồn thấp (phân trang) |
| POST | `/issues` | `{skuId, quantity, type: damage\|gift, note}` |

Phiếu trả thêm `createdByName`, `decidedByName`; mỗi dòng có `bookTitle`, `editionName`, `coverType`.

Mã lỗi: `OUT_OF_STOCK` (409), `RECEIPT_NOT_DRAFT` (409), `RECEIPT_FORBIDDEN` (403, sửa/xóa phiếu của người khác),
`BATCH_CODE_DUPLICATE` (409), `RECEIPT_EMPTY` (422), `REFERENCE_NOT_FOUND` (422, SKU không tồn tại).

## Cấu hình cần thiết

Không có cấu hình riêng (dùng `ConnectionStrings:Default`).

## Thay đổi CSDL (migration liên quan)

`CreateInventorySchema` (schema `inventory`): `stock_items` (khóa chính `sku_id`), `print_batches`
(unique `sku_id + batch_code`), `goods_receipts`, `goods_receipt_lines`, `inventory_transactions` (thẻ kho,
chỉ thêm), `stock_reservations` (unique `reference + sku_id`). Không đặt khóa ngoại sang `catalog.skus`.

## Hạn chế / việc còn lại

- Chưa có kiểm kê định kỳ và phiếu điều chỉnh có duyệt (BR-50), điều chỉnh tăng tồn.
- Cảnh báo tồn thấp mới có API danh sách, chưa gửi email (chờ module Notification).
- Route khác bản phác thảo mục 11.3 (`/print-batches`, `/inventory/transactions`): lô in và thẻ kho xem qua
  `/inventory/skus/{skuId}`.
