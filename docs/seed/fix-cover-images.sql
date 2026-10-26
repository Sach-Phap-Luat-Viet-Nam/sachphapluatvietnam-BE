-- Sửa ảnh bìa (editions.cover_image_url) của 15 đầu sách seed PL249..PL263 cho đúng sách.
--
-- Lỗi trước đó (đã xem từng ảnh trên Cloudinary): tên file ảnh đúng mã SKU nhưng 7 ấn bản trỏ nhầm file:
--   PL249 -> 261.jpg (bìa Quản lý thuế), PL258 -> 250.jpg (bìa Quản lý nợ công), PL259 -> 263.jpg (bìa Xây dựng chi phí/chất lượng),
--   PL260 -> 258.jpg (bìa Xây dựng tập 1), PL261 -> 260.jpg (bìa Xử lý vi phạm hành chính),
--   PL262 -> 253.jpg (bìa Ngân sách nhà nước), PL263 -> 256.jpg (bìa 90 án lệ).
-- Các file 249.jpg, 259.jpg, 262.jpg đã được upload (bìa Luật Kế toán, Luật Xây dựng tập 2, Luật Cán bộ công chức/Viên chức).
--
-- Khớp theo ISBN (duy nhất, không đổi giữa các môi trường) chứ không theo id. Chạy lại nhiều lần an toàn:
-- chỉ ghi dòng đang khác. Thiếu ấn bản nào trong 15 ISBN thì báo lỗi và không ghi gì (chưa chạy catalog-books.sql).
--
-- LƯU Ý:
--  * 3 URL không có số phiên bản (249, 259, 262) vì chưa rõ version Cloudinary đã cấp; Cloudinary vẫn phục vụ được.
--    Thay bằng URL đầy đủ (có /v.../) ở trang Admin hoặc ở đây nếu muốn khóa phiên bản.
--  * Ghi thẳng vào bảng nên không qua kiểm tra link Cloudinary của API; các URL đều bắt đầu bằng
--    https://res.cloudinary.com/dxm8oe8w1/image/upload/ như PUT /admin/editions/{id} yêu cầu.

BEGIN;

CREATE TEMP TABLE fix_covers
(
    isbn            char(13) PRIMARY KEY,
    sku_code        varchar(50) NOT NULL,
    cover_image_url text        NOT NULL
) ON COMMIT DROP;

INSERT INTO fix_covers (isbn, sku_code, cover_image_url)
VALUES
('9780000002495', 'PL249', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/249.jpg'),
('9780000002501', 'PL250', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994563/250.jpg'),
('9780000002518', 'PL251', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994563/251.jpg'),
('9780000002525', 'PL252', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994563/252.jpg'),
('9780000002532', 'PL253', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994563/253.jpg'),
('9780000002549', 'PL254', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/254.jpg'),
('9780000002556', 'PL255', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/255.jpg'),
('9780000002563', 'PL256', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/256.jpg'),
('9780000002570', 'PL257', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/257.jpg'),
('9780000002587', 'PL258', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/258.jpg'),
('9780000002594', 'PL259', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/259.jpg'),
('9780000002600', 'PL260', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994564/260.jpg'),
('9780000002617', 'PL261', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994565/261.jpg'),
('9780000002624', 'PL262', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/262.jpg'),
('9780000002631', 'PL263', 'https://res.cloudinary.com/dxm8oe8w1/image/upload/v1792994563/263.jpg');

DO $$
DECLARE
    v_missing text;
BEGIN
    SELECT string_agg(f.sku_code, ', ' ORDER BY f.sku_code)
    INTO v_missing
    FROM fix_covers f
    LEFT JOIN catalog.editions e ON e.isbn = f.isbn
    WHERE e.id IS NULL;

    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'Chưa có ấn bản (theo ISBN) của: %. Chạy docs/seed/catalog-books.sql trước.', v_missing;
    END IF;
END
$$;

UPDATE catalog.editions e
SET cover_image_url = f.cover_image_url,
    updated_at      = now()
FROM fix_covers f
WHERE e.isbn = f.isbn
  AND e.cover_image_url IS DISTINCT FROM f.cover_image_url;

COMMIT;

-- Kiểm tra sau khi chạy: mỗi dòng phải có số trong tên file = số trong sku_code.
-- SELECT k.sku_code, e.id AS edition_id, e.cover_image_url
-- FROM catalog.skus k
-- JOIN catalog.editions e ON e.id = k.edition_id
-- WHERE k.sku_code ~ '^PL2(49|5[0-9]|6[0-3])$'
-- ORDER BY k.sku_code;
