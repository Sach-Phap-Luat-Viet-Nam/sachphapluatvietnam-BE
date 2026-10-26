-- Seed tồn kho ban đầu: mỗi SKU PL249..PL263 nhập 100 cuốn.
-- Đi đúng luồng "duyệt phiếu nhập" của InventoryAdminService.ApproveReceiptAsync để giữ bất biến
-- available + reserved = tổng remaining các lô in: 1 phiếu nhập đã duyệt -> lô in -> thẻ kho 'import' -> stock_items.
--
-- Chạy lại an toàn: SKU đã có bất kỳ lô in nào sẽ bị bỏ qua (không nhập đôi).
-- Cần chạy SAU docs/seed/catalog-books.sql (cần có SKU) và đã có tài khoản super_admin.
--
-- LƯU Ý:
--  * Giá vốn (unit_cost) nguồn không có -> để 0. Cập nhật lại nếu cần báo cáo lãi gộp.
--  * Người lập và người duyệt phiếu đều là một tài khoản super_admin (seed trực tiếp, không qua kiểm soát chéo).
--  * Mã lô 'SEED-100', xưởng in để trống.

BEGIN;

CREATE TEMP TABLE seed_stock ON COMMIT DROP AS
SELECT k.id AS sku_id, k.sku_code
FROM catalog.skus k
WHERE k.sku_code ~ '^PL2(49|5[0-9]|6[0-3])$'
  AND NOT EXISTS (SELECT 1 FROM inventory.print_batches b WHERE b.sku_id = k.id);

DO $$
DECLARE
    v_qty     CONSTANT integer := 100;
    v_user    bigint;
    v_receipt bigint;
    v_today   date := (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM seed_stock) THEN
        RAISE NOTICE 'Không có SKU nào cần seed tồn kho (chưa có SKU hoặc đã nhập rồi).';
        RETURN;
    END IF;

    SELECT ur.user_id
    INTO v_user
    FROM auth.user_roles ur
    JOIN auth.roles r ON r.id = ur.role_id
    WHERE r.name = 'super_admin'
    ORDER BY ur.user_id
    LIMIT 1;

    IF v_user IS NULL THEN
        RAISE EXCEPTION 'Không tìm thấy tài khoản super_admin để ghi người lập/duyệt phiếu nhập.';
    END IF;

    -- Phiếu nhập đã duyệt
    INSERT INTO inventory.goods_receipts
        (status, note, created_by, decided_by, decided_at, created_at, updated_at)
    VALUES
        ('approved', 'Seed tồn kho ban đầu: 100 cuốn/SKU', v_user, v_user, now(), now(), now())
    RETURNING id INTO v_receipt;

    -- Lô in (remaining = quantity)
    INSERT INTO inventory.print_batches
        (sku_id, batch_code, printer_name, quantity, unit_cost, received_at, remaining, created_at, updated_at)
    SELECT s.sku_id, 'SEED-100', NULL, v_qty, 0, v_today, v_qty, now(), now()
    FROM seed_stock s;

    -- Dòng phiếu nhập, gắn với lô in vừa tạo
    INSERT INTO inventory.goods_receipt_lines
        (goods_receipt_id, sku_id, batch_code, printer_name, quantity, unit_cost, received_at, print_batch_id)
    SELECT v_receipt, b.sku_id, b.batch_code, NULL, b.quantity, b.unit_cost, b.received_at, b.id
    FROM inventory.print_batches b
    JOIN seed_stock s ON s.sku_id = b.sku_id
    WHERE b.batch_code = 'SEED-100';

    -- Thẻ kho
    INSERT INTO inventory.inventory_transactions
        (sku_id, print_batch_id, type, quantity, reference, created_by, note, created_at, updated_at)
    SELECT b.sku_id, b.id, 'import', b.quantity, 'GR-' || v_receipt, v_user, NULL, now(), now()
    FROM inventory.print_batches b
    JOIN seed_stock s ON s.sku_id = b.sku_id
    WHERE b.batch_code = 'SEED-100';

    -- Tồn khả dụng (ngưỡng cảnh báo mặc định 50 theo StockItem.DefaultLowStockThreshold)
    INSERT INTO inventory.stock_items
        (sku_id, available, reserved, low_stock_threshold, created_at, updated_at)
    SELECT s.sku_id, v_qty, 0, 50, now(), now()
    FROM seed_stock s
    ON CONFLICT (sku_id) DO UPDATE
        SET available = inventory.stock_items.available + EXCLUDED.available,
            updated_at = now();

    RAISE NOTICE 'Đã nhập % cuốn cho % SKU (phiếu GR-%).', v_qty, (SELECT count(*) FROM seed_stock), v_receipt;
END
$$;

-- Kiểm tra: available + reserved phải bằng tổng remaining các lô
SELECT k.sku_code,
       si.available,
       si.reserved,
       COALESCE(SUM(pb.remaining), 0) AS batch_remaining
FROM catalog.skus k
LEFT JOIN inventory.stock_items si ON si.sku_id = k.id
LEFT JOIN inventory.print_batches pb ON pb.sku_id = k.id
WHERE k.sku_code ~ '^PL2(49|5[0-9]|6[0-3])$'
GROUP BY k.sku_code, si.available, si.reserved
ORDER BY k.sku_code;

COMMIT;
