-- Seed 15 đầu sách pháp luật (PL249..PL263) vào schema catalog: books -> editions -> skus.
-- Chạy lại nhiều lần an toàn: sách đã có (theo slug / mã SKU) sẽ được bỏ qua.
--
-- LƯU Ý trước khi dùng thật:
--  * isbn và publishing_decision_no là NOT NULL nhưng tài liệu nguồn không có. Query dùng GIÁ TRỊ GIỮ CHỖ:
--      - isbn: 978000000 + số SKU + số kiểm tra (hợp lệ ISBN-13, dễ nhận ra là giả). Thay bằng ISBN thật
--        qua PUT /admin/editions/{id}.
--      - publishing_decision_no: 'CHUA-CAP-NHAT'.
--  * Nguồn không ghi loại bìa -> gán 'paperback'; không có giá ưu đãi -> sale_price = giá bìa.
--  * Ảnh bìa (cover_image_url) để trống, bổ sung sau qua Admin.
--  * Sách đang để active / on_sale để hiện trên website; muốn ẩn thì đổi thành 'draft' ở hai chỗ có chú thích.

BEGIN;

CREATE TEMP TABLE seed_books
(
    sku_code            varchar(50)  NOT NULL,
    slug                varchar(255) NOT NULL,
    title               varchar(255) NOT NULL,
    description         text         NOT NULL,
    target_audiences    text[]       NOT NULL,
    isbn                char(13)     NOT NULL,
    pages               integer      NOT NULL,
    list_price          numeric(12, 0) NOT NULL,
    table_of_contents   text         NOT NULL
) ON COMMIT DROP;

INSERT INTO seed_books (sku_code, slug, title, description, target_audiences, isbn, pages, list_price, table_of_contents)
VALUES
('PL249',
 'luat-ke-toan-va-he-thong-cac-van-ban-quy-dinh-chi-tiet-thi-hanh',
 'LUẬT KẾ TOÁN VÀ HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến kế toán, chế độ kế toán và xử phạt vi phạm hành chính trong lĩnh vực kế toán. Phù hợp với người làm kế toán, kế toán trưởng, kiểm toán viên, doanh nghiệp, cơ quan quản lý tài chính và người học ngành kế toán.',
 ARRAY['student', 'business'], '9780000002495', 458, 415000,
 'Phần 1: Luật Kế toán
Phần 2: Hệ thống các nghị định quy định chi tiết thi hành
Phần 3: Hệ thống các thông tư hướng dẫn chi tiết thi hành
Phần 4: Quy định xử phạt vi phạm hành chính trong lĩnh vực kế toán'),

('PL250',
 'luat-quan-ly-no-cong-he-thong-cac-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh',
 'LUẬT QUẢN LÝ NỢ CÔNG – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến nợ công, vay và trả nợ, kế toán khoản vay và công bố thông tin nợ công. Phù hợp với cán bộ tài chính công, cơ quan quản lý nợ, kế toán khu vực công và người nghiên cứu tài chính công.',
 ARRAY['citizen'], '9780000002501', 470, 445000,
 'Phần 1: Luật Quản lý nợ công
Phần 2: Hệ thống các nghị định quy định chi tiết thi hành
Phần 3: Hướng dẫn chế độ kế toán đối với các khoản vay, trả nợ và mẫu biểu công bố thông tin về nợ công'),

('PL251',
 'luat-nha-giao-he-thong-cac-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh',
 'LUẬT NHÀ GIÁO – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến nhà giáo, nghề nghiệp giáo viên, tuyển dụng, tiền lương và phụ cấp ưu đãi. Phù hợp với giáo viên, nhà giáo, cán bộ quản lý trường học, nhân sự hỗ trợ giáo dục và cơ quan quản lý giáo dục.',
 ARRAY['citizen'], '9780000002518', 434, 395000,
 'Phần 1: Luật Nhà giáo
Phần 2: Hệ thống các văn bản quy định, hướng dẫn chi tiết thi hành
Phần 3: Chế độ phụ cấp ưu đãi theo nghề đối với nhà giáo, cán bộ quản lý cơ sở giáo dục và nhân sự hỗ trợ giáo dục công tác trong cơ sở giáo dục công lập'),

('PL252',
 'luat-phong-chong-ma-tuy-he-thong-cac-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh',
 'LUẬT PHÒNG, CHỐNG MA TÚY – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến phòng, chống ma túy, tình trạng nghiện, hồ sơ và thủ tục. Phù hợp với cán bộ thực thi pháp luật, cơ quan phòng, chống ma túy, người làm công tác xã hội và nghiên cứu pháp luật.',
 ARRAY['citizen'], '9780000002525', 436, 395000,
 'Phần 1: Luật Phòng, chống ma túy
Phần 2: Hệ thống văn bản quy định chi tiết, hướng dẫn thi hành'),

('PL253',
 'luat-ngan-sach-nha-nuoc-he-thong-cac-van-ban-quy-dinh-chi-tiet-thi-hanh',
 'LUẬT NGÂN SÁCH NHÀ NƯỚC – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến ngân sách nhà nước, lập dự toán, quản lý và quyết toán chi, mục lục ngân sách. Phù hợp với cán bộ tài chính - kế toán công, đơn vị dự toán, cơ quan nhà nước, Kho bạc Nhà nước và người nghiên cứu ngân sách.',
 ARRAY['citizen'], '9780000002532', 498, 450000,
 'Phần 1: Luật Ngân sách nhà nước
Phần 2: Hệ thống các nghị định quy định chi tiết thi hành Luật Ngân sách nhà nước
Phần 3: Hệ thống các thông tư quy định, hướng dẫn chi tiết thi hành'),

('PL254',
 'bo-luat-hinh-su-he-thong-cac-van-ban-huong-dan-thi-hanh',
 'BỘ LUẬT HÌNH SỰ – HỆ THỐNG CÁC VĂN BẢN HƯỚNG DẪN THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến Bộ luật Hình sự: nghị quyết của Hội đồng Thẩm phán, nghị quyết của Quốc hội, nghị định và thông tư liên tịch. Phù hợp với luật sư, thẩm phán, kiểm sát viên, điều tra viên, sinh viên luật và người làm công tác pháp luật hình sự.',
 ARRAY['student', 'lawyer'], '9780000002549', 632, 535000,
 'Phần 1: Bộ luật Hình sự
Phần 2: Hệ thống các nghị quyết của Hội đồng Thẩm phán Tòa án nhân dân tối cao
Phần 3: Hệ thống các nghị quyết của Quốc hội, Ủy ban Thường vụ Quốc hội
Phần 4: Hệ thống các nghị định quy định chi tiết thi hành
Phần 5: Hệ thống các thông tư liên tịch quy định, hướng dẫn thi hành'),

('PL255',
 'luat-giao-duc-he-thong-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh',
 'LUẬT GIÁO DỤC – HỆ THỐNG VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến giáo dục, quản lý trường học, điều lệ trường phổ thông và sách giáo khoa điện tử. Phù hợp với nhà quản lý giáo dục, giáo viên, trường học, cán bộ ngành giáo dục và người nghiên cứu chính sách giáo dục.',
 ARRAY['citizen'], '9780000002556', 568, 565000,
 'Phần 1: Luật Giáo dục
Phần 2: Hệ thống các nghị định quy định chi tiết thi hành
Phần 3: Hệ thống các thông tư quy định, hướng dẫn chi tiết thi hành'),

('PL256',
 'he-thong-90-an-le-viet-nam-hien-hanh',
 'HỆ THỐNG 90 ÁN LỆ VIỆT NAM HIỆN HÀNH',
 'Tập hợp, hệ thống 90 án lệ Việt Nam hiện hành phục vụ nghiên cứu và áp dụng trong xét xử. Phù hợp với thẩm phán, kiểm sát viên, luật sư, giảng viên, sinh viên luật, nhà nghiên cứu và người hành nghề pháp luật.',
 ARRAY['student', 'lawyer'], '9780000002563', 612, 495000,
 'Tập hợp, hệ thống 90 án lệ Việt Nam hiện hành'),

('PL257',
 'luat-tu-phap-nguoi-chua-thanh-nien-he-thong-cac-van-ban-quy-dinh-huong-dan-chi-tiet-thi-hanh',
 'LUẬT TƯ PHÁP NGƯỜI CHƯA THÀNH NIÊN – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến tư pháp người chưa thành niên, tố tụng hình sự, biện pháp giáo dục và tái hòa nhập cộng đồng. Phù hợp với người làm tư pháp, luật sư, cán bộ bảo vệ trẻ em, cơ quan tiến hành tố tụng và người nghiên cứu quyền trẻ em.',
 ARRAY['student', 'lawyer'], '9780000002570', 474, 450000,
 'Phần 1: Luật Tư pháp người chưa thành niên
Phần 2: Hệ thống các văn bản quy định, hướng dẫn chi tiết thi hành
Phần 3: Hệ thống các văn bản về thủ tục tố tụng hình sự đối với người chưa thành niên'),

('PL258',
 'luat-xay-dung-va-cac-quy-dinh-chi-tiet-ve-hoat-dong-xay-dung-tap-1',
 'LUẬT XÂY DỰNG VÀ CÁC QUY ĐỊNH CHI TIẾT VỀ HOẠT ĐỘNG XÂY DỰNG (TẬP 1)',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến xây dựng: quản lý dự án, giấy phép, trật tự xây dựng, năng lực hoạt động, vật liệu, quyết toán và chất lượng công trình. Phù hợp với chủ đầu tư, ban quản lý dự án, nhà thầu, tư vấn xây dựng, kỹ sư, kiến trúc sư và cán bộ quản lý xây dựng.',
 ARRAY['business'], '9780000002587', 646, 550000,
 'Phần 1: Luật Xây dựng
Phần 2: Quy định chi tiết về quản lý dự án đầu tư xây dựng, giấy phép xây dựng, quản lý trật tự xây dựng, xây dựng công trình đặc thù
Phần 3: Quy định chi tiết về cấp công trình xây dựng và áp dụng cấp công trình
Phần 4: Điều kiện năng lực hoạt động xây dựng, hệ thống thông tin và cơ sở dữ liệu quốc gia về hoạt động xây dựng
Phần 5: Quy định chi tiết về quản lý vật liệu xây dựng
Phần 6: Quy định chi tiết và hướng dẫn thi hành một số điều về quyết toán vốn đầu tư dự án
Phần 7: Quy định chi tiết và hướng dẫn thi hành về quản lý chất lượng, thi công xây dựng và bảo trì công trình xây dựng'),

('PL259',
 'luat-xay-dung-quan-ly-chi-phi-dau-tu-xay-dung-hop-dong-xay-dung-tap-2',
 'LUẬT XÂY DỰNG – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH, HƯỚNG DẪN CHI TIẾT THI HÀNH VỀ QUẢN LÝ CHI PHÍ ĐẦU TƯ XÂY DỰNG, HỢP ĐỒNG XÂY DỰNG (TẬP 2)',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến quản lý chi phí đầu tư xây dựng và hợp đồng xây dựng. Phù hợp với chủ đầu tư, ban quản lý dự án, nhà thầu, đơn vị tư vấn, cán bộ lập dự toán và quản lý hợp đồng xây dựng.',
 ARRAY['business'], '9780000002594', 646, 550000,
 'Phần 1: Luật Xây dựng
Phần 2: Các quy định chi tiết về quản lý chi phí đầu tư xây dựng
Phần 3: Các quy định chi tiết và hướng dẫn sử dụng, vận dụng hợp đồng xây dựng'),

('PL260',
 'luat-xu-ly-vi-pham-hanh-chinh-he-thong-van-ban-quy-dinh-chi-tiet-va-bien-phap-thi-hanh',
 'LUẬT XỬ LÝ VI PHẠM HÀNH CHÍNH – HỆ THỐNG VĂN BẢN QUY ĐỊNH CHI TIẾT VÀ BIỆN PHÁP THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến xử lý vi phạm hành chính, thẩm quyền xử phạt, biện pháp thi hành và thủ tục hành chính. Phù hợp với cán bộ quản lý nhà nước, người có thẩm quyền xử phạt, luật sư, doanh nghiệp và người nghiên cứu pháp luật hành chính.',
 ARRAY['lawyer', 'business'], '9780000002600', 546, 465000,
 'Phần 1: Luật Xử lý vi phạm hành chính
Phần 2: Hệ thống văn bản quy định chi tiết và biện pháp thi hành'),

('PL261',
 'luat-quan-ly-thue-he-thong-cac-van-ban-quy-dinh-chi-tiet-thi-hanh',
 'LUẬT QUẢN LÝ THUẾ – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến quản lý thuế: đăng ký thuế, hóa đơn điện tử, chứng từ điện tử, hộ kinh doanh, giao dịch liên kết và quản lý rủi ro thuế. Phù hợp với kế toán, kế toán thuế, doanh nghiệp, hộ kinh doanh, đại lý thuế, cán bộ thuế và tư vấn tài chính.',
 ARRAY['business'], '9780000002617', 684, 585000,
 'Phần 1: Luật Quản lý thuế
Phần 2: Hệ thống các nghị định quy định chi tiết thi hành
Phần 3: Hệ thống các thông tư quy định chi tiết thi hành'),

('PL262',
 'luat-can-bo-cong-chuc-luat-vien-chuc-he-thong-cac-van-ban-quy-dinh-chi-tiet-thi-hanh',
 'LUẬT CÁN BỘ, CÔNG CHỨC; LUẬT VIÊN CHỨC – HỆ THỐNG CÁC VĂN BẢN QUY ĐỊNH CHI TIẾT THI HÀNH',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến cán bộ, công chức, viên chức: tuyển dụng, sử dụng và quản lý nhân sự khu vực công. Phù hợp với cán bộ, công chức, viên chức, bộ phận tổ chức - cán bộ, đơn vị sự nghiệp công lập và người ôn tập, nghiên cứu pháp luật công vụ.',
 ARRAY['citizen'], '9780000002624', 566, 480000,
 'Phần 1: Luật Cán bộ, công chức và các văn bản quy định chi tiết thi hành
Phần 2: Luật Viên chức và hệ thống các văn bản quy định chi tiết thi hành'),

('PL263',
 'luat-xay-dung-quan-ly-chi-phi-chat-luong-thi-cong-bao-tri-cong-trinh-xay-dung',
 'LUẬT XÂY DỰNG – QUY ĐỊNH VỀ QUẢN LÝ CHI PHÍ ĐẦU TƯ XÂY DỰNG; QUẢN LÝ CHẤT LƯỢNG, THI CÔNG XÂY DỰNG VÀ BẢO TRÌ CÔNG TRÌNH XÂY DỰNG',
 'Sách hệ thống các quy định pháp luật và văn bản hướng dẫn liên quan đến quản lý chi phí đầu tư xây dựng, quản lý chất lượng, thi công và bảo trì công trình xây dựng. Phù hợp với chủ đầu tư, nhà thầu, đơn vị tư vấn, kỹ sư xây dựng, cán bộ quản lý chi phí, chất lượng và bảo trì công trình.',
 ARRAY['business'], '9780000002631', 506, 430000,
 'Phần 1: Luật Xây dựng
Phần 2: Quy định chi tiết thi hành về quản lý chi phí đầu tư xây dựng
Phần 3: Quy định chi tiết thi hành về quản lý chất lượng, thi công xây dựng và bảo trì công trình xây dựng');

-- 1) Đầu sách
INSERT INTO catalog.books
    (title, slug, description, book_type, target_audiences, status, created_at, updated_at)
SELECT s.title,
       s.slug,
       s.description,
       'consolidated',
       s.target_audiences::varchar(30)[],
       'active', -- đổi 'draft' nếu chưa muốn hiện trên website
       now(),
       now()
FROM seed_books s
WHERE NOT EXISTS (SELECT 1 FROM catalog.skus k WHERE k.sku_code = s.sku_code)
ON CONFLICT (slug) DO NOTHING;

-- 2) Ấn bản (mỗi sách một ấn bản, là ấn bản mới nhất)
INSERT INTO catalog.editions
    (book_id, isbn, publisher, publishing_decision_no, pages, size, table_of_contents,
     is_latest, status, created_at, updated_at)
SELECT b.id,
       s.isbn,
       'Nhà xuất bản Hồng Đức',
       'CHUA-CAP-NHAT',
       s.pages,
       '19 x 27 cm',
       s.table_of_contents,
       true,
       'on_sale', -- đổi 'draft' nếu chưa muốn bán
       now(),
       now()
FROM seed_books s
JOIN catalog.books b ON b.slug = s.slug
WHERE NOT EXISTS (SELECT 1 FROM catalog.editions e WHERE e.book_id = b.id);

-- 3) SKU (bìa mềm, giá bán = giá bìa)
INSERT INTO catalog.skus
    (edition_id, sku_code, cover_type, list_price, sale_price, allow_preorder, created_at, updated_at)
SELECT e.id,
       s.sku_code,
       'paperback',
       s.list_price,
       s.list_price,
       false,
       now(),
       now()
FROM seed_books s
JOIN catalog.books b ON b.slug = s.slug
JOIN catalog.editions e ON e.book_id = b.id AND e.isbn = s.isbn
WHERE NOT EXISTS (SELECT 1 FROM catalog.skus k WHERE k.sku_code = s.sku_code);

-- Kiểm tra: phải ra 15 dòng
SELECT k.sku_code, b.title, e.isbn, e.pages, k.list_price, k.sale_price, b.status, e.status AS edition_status
FROM catalog.skus k
JOIN catalog.editions e ON e.id = k.edition_id
JOIN catalog.books b ON b.id = e.book_id
WHERE k.sku_code BETWEEN 'PL249' AND 'PL263'
ORDER BY k.sku_code;

COMMIT;
