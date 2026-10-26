-- Seed 1 combo bộ sách: "Luật Xây dựng - Hệ thống các văn bản quy định, hướng dẫn chi tiết thi hành" (PL258 + PL259).
-- Mua lẻ 2 tập = 550.000đ + 550.000đ = 1.100.000đ, mua combo 625.000đ (giảm ~43,2%, dưới trần Promotion:MaxDiscountPercent = 50).
--
-- Chạy lại an toàn: đã có combo cùng slug (chưa xóa) thì bỏ qua, không tạo trùng.
-- Cần chạy SAU migration AddCombos (schema promotion có bảng combos, combo_items) và SAU docs/seed/catalog-books.sql
-- (cần có SKU PL258, PL259). Thiếu một trong hai SKU thì báo lỗi và không ghi gì.
--
-- LƯU Ý:
--  * Query ghi thẳng vào bảng nên KHÔNG qua kiểm tra của ComboAdminService (SKU tồn tại, giá combo thấp hơn tổng giá bán
--    thường, trần % giảm). Các điều kiện đó đã được kiểm bằng tay ở trên; đổi giá thì kiểm lại.
--  * Giá combo là giá so với GIÁ HIỆN HÀNH: nếu một tập đang chạy lịch giá khiến tổng lẻ thấp hơn hoặc bằng 625.000đ thì
--    combo tự không áp (khách đã có giá tốt hơn).
--  * Bắt đầu chạy ngay (starts_at = now()), không có ngày kết thúc. Tắt bằng is_active = false hoặc PUT /admin/combos/{id}.
--  * image_url để trống: trang combo tự dựng từ ảnh bìa hai tập. Ảnh banner 1920x1080 là ảnh quảng cáo (popup), không
--    hợp làm ảnh combo vì trang combo hiện ảnh tỷ lệ 4:3 sẽ cắt mất chữ trên banner.

BEGIN;

DO $$
DECLARE
    v_slug  CONSTANT text := 'combo-luat-xay-dung-he-thong-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh';
    v_sku1  bigint;
    v_sku2  bigint;
    v_combo bigint;
BEGIN
    SELECT id INTO v_sku1 FROM catalog.skus WHERE sku_code = 'PL258';
    SELECT id INTO v_sku2 FROM catalog.skus WHERE sku_code = 'PL259';

    IF v_sku1 IS NULL OR v_sku2 IS NULL THEN
        RAISE EXCEPTION 'Thiếu SKU: PL258 -> %, PL259 -> %. Chạy docs/seed/catalog-books.sql trước.', v_sku1, v_sku2;
    END IF;

    IF EXISTS (SELECT 1 FROM promotion.combos WHERE slug = v_slug AND deleted_at IS NULL) THEN
        RAISE NOTICE 'Combo "%" đã tồn tại, bỏ qua.', v_slug;
        RETURN;
    END IF;

    INSERT INTO promotion.combos
        (name, slug, description, image_url, combo_price, is_active, starts_at, ends_at, deleted_at, created_at, updated_at)
    VALUES
        ('Combo Luật Xây dựng - Hệ thống các văn bản quy định, hướng dẫn chi tiết thi hành (trọn bộ 2 tập)',
         v_slug,
         'Trọn bộ 2 tập Luật Xây dựng và hệ thống văn bản quy định, hướng dẫn chi tiết thi hành: Tập 1 về hoạt động xây dựng, Tập 2 về quản lý chi phí đầu tư xây dựng, hợp đồng xây dựng. Mua cả bộ chỉ 625.000đ thay vì 1.100.000đ khi mua lẻ.',
         NULL,
         625000,
         true,
         now(),
         NULL,
         NULL,
         now(),
         now())
    RETURNING id INTO v_combo;

    INSERT INTO promotion.combo_items (combo_id, sku_id, quantity)
    VALUES (v_combo, v_sku1, 1),
           (v_combo, v_sku2, 1);

    RAISE NOTICE 'Đã tạo combo id = % (PL258 + PL259, 625.000đ).', v_combo;
END
$$;

COMMIT;

-- Kiểm tra nhanh sau khi chạy:
-- SELECT c.id, c.slug, c.combo_price, k.sku_code, i.quantity
-- FROM promotion.combos c
-- JOIN promotion.combo_items i ON i.combo_id = c.id
-- JOIN catalog.skus k ON k.id = i.sku_id
-- WHERE c.deleted_at IS NULL ORDER BY c.id, k.sku_code;
