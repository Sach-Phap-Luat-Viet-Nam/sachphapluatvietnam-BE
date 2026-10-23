# Tài liệu dự án Website Sách Luật Việt Nam

Oct 23, 2026 · @Software DGP

Tài liệu này là nguồn tham chiếu duy nhất (single source of truth) cho việc xây dựng website thương mại điện tử của thương hiệu **Sách Luật Việt Nam** — nơi bán sách pháp luật do chính doanh nghiệp biên soạn, in ấn và phân phối.

## 1. Thông tin tài liệu

| Mục | Nội dung |
| --- | --- |
| Tên dự án | Website thương mại điện tử Sách Luật Việt Nam |
| Mã dự án | SLVN-WEB |
| Loại tài liệu | Tài liệu đặc tả dự án tổng hợp (Project Specification: BRD + SRS + thiết kế kỹ thuật) |
| Phiên bản | 1.0 (bản nháp đầu tiên) |
| Trạng thái | Nháp — chờ chủ dự án duyệt |
| Chủ sở hữu tài liệu | Chủ dự án / Product Owner |
| Đối tượng đọc | Ban giám đốc, Product Owner, BA, thiết kế UI/UX, lập trình viên, QA/tester, vận hành kho, chăm sóc khách hàng, bộ phận biên tập |
| Ngôn ngữ | Tiếng Việt |

**Lịch sử thay đổi**

| Phiên bản | Ngày | Người sửa | Nội dung thay đổi |
| --- | --- | --- | --- |
| 1.0 | 23/10/2026 | Chủ dự án | Khởi tạo tài liệu |

**Quy ước trong tài liệu**

- Mức ưu tiên theo MoSCoW: **Must** (bắt buộc ở bản ra mắt), **Should** (nên có), **Could** (có thể có), **Won't** (không làm ở giai đoạn này).
- Mã yêu cầu: `BR-xx` (nghiệp vụ), `FR-xx` (chức năng), `NFR-xx` (phi chức năng).
- Tiền tệ: VNĐ, đã gồm VAT trừ khi ghi khác. Thời gian: giờ Việt Nam (UTC+7).
- Mọi con số mục tiêu trong tài liệu là **giả định đề xuất**, cần chủ dự án xác nhận.

## 2. Tổng quan dự án

Website là kênh bán trực tiếp (D2C) toàn bộ đầu sách pháp luật do Sách Luật Việt Nam tự biên soạn và in, giúp bỏ qua nhà phân phối trung gian, giữ biên lợi nhuận và cập nhật sách nhanh khi luật thay đổi.

### 2.1. Bối cảnh

- Hệ thống pháp luật Việt Nam thay đổi liên tục: luật, nghị định, thông tư mới được ban hành và sửa đổi hằng năm. Người dùng cần sách **đúng bản hiện hành**.
- Kênh bán hiện tại (giả định): nhà sách truyền thống, sàn TMĐT (Shopee, Tiki, Lazada), bán qua Zalo/Facebook. Kênh này bị chiết khấu cao (25–40%) và khó kiểm soát tồn kho, giá.
- Doanh nghiệp đã có năng lực biên soạn và liên kết xuất bản, nên có lợi thế tự quyết định nội dung, giá và thời điểm phát hành.

### 2.2. Mô hình kinh doanh: Tự sản xuất – Tự bán

| Khâu | Ai thực hiện | Vai trò của website |
| --- | --- | --- |
| Biên soạn nội dung (hệ thống hóa văn bản, chú giải, hỏi–đáp) | Ban biên tập nội bộ, cộng tác viên luật sư | Quản lý thông tin đầu sách, ấn bản, ngày cập nhật văn bản |
| Xuất bản (cấp ISBN, quyết định phát hành) | Liên kết với nhà xuất bản có giấy phép | Lưu số ISBN, NXB, số quyết định xuất bản, số đăng ký KHXB |
| In ấn | Xưởng in đối tác theo đơn đặt in | Theo dõi lô in, số lượng nhập kho |
| Lưu kho | Kho của doanh nghiệp | Quản lý tồn kho theo lô/ấn bản |
| Bán hàng | Website (kênh chính), bán sỉ cho tổ chức | Toàn bộ trải nghiệm mua hàng, khuyến mãi, hóa đơn |
| Giao hàng | Đơn vị vận chuyển (GHN, GHTK, Viettel Post…) | Tạo vận đơn, theo dõi trạng thái |
| Hậu mãi | Chăm sóc khách hàng nội bộ | Đổi trả, thông báo ấn bản mới, đổi sách cũ lấy sách mới |

**Lợi thế cạnh tranh cần thể hiện trên website**

1. Giá tốt hơn kênh trung gian vì không qua phân phối.
2. Cập nhật nhanh: hiển thị rõ "cập nhật đến văn bản nào, ngày nào".
3. Nguồn gốc rõ ràng: đầy đủ ISBN, NXB, giấy phép — chống sách lậu, sách in lậu.
4. Dịch vụ riêng: đặt trước ấn bản mới, đổi sách cũ lấy sách mới có trợ giá, in theo yêu cầu số lượng lớn cho cơ quan.

### 2.3. Tầm nhìn

Trở thành địa chỉ trực tuyến tin cậy số 1 để mua sách pháp luật bản hiện hành tại Việt Nam.

### 2.4. Mục tiêu và KPI (12 tháng sau ra mắt — giả định)

| Mục tiêu | Chỉ số đo (KPI) | Chỉ tiêu đề xuất |
| --- | --- | --- |
| Tăng doanh thu kênh trực tiếp | Tỷ trọng doanh thu từ website / tổng doanh thu | ≥ 40% |
| Bán được nhiều | Số đơn hoàn tất mỗi tháng | ≥ 3.000 đơn |
| Chuyển đổi tốt | Tỷ lệ chuyển đổi (đơn / phiên truy cập) | ≥ 1,5% |
| Giá trị đơn cao | Giá trị đơn hàng trung bình (AOV) | ≥ 350.000đ |
| Khách quay lại | Tỷ lệ khách mua lại trong 6 tháng | ≥ 25% |
| Tìm kiếm tự nhiên | Lượt truy cập từ Google (organic) mỗi tháng | ≥ 50.000 phiên |
| Vận hành tốt | Tỷ lệ đơn giao thành công | ≥ 95% |
| Hài lòng | Điểm đánh giá trung bình | ≥ 4,5/5 |

## 3. Phạm vi dự án

Giai đoạn 1 (MVP) làm website bán sách giấy hoàn chỉnh cho khách lẻ và trang quản trị; sách điện tử, ứng dụng di động và đa nhà bán để ở các giai đoạn sau.

### 3.1. Trong phạm vi

- Website khách hàng (responsive desktop/tablet/mobile): trang chủ, danh mục, tìm kiếm, chi tiết sách, giỏ hàng, thanh toán, tài khoản, theo dõi đơn, blog/tin pháp luật.
- Trang quản trị (Admin CMS): sách, ấn bản, danh mục, tồn kho, đơn hàng, khách hàng, khuyến mãi, nội dung, báo cáo, phân quyền.
- Tích hợp: cổng thanh toán (VNPAY/MoMo/ZaloPay/chuyển khoản VietQR), COD, đơn vị vận chuyển, hóa đơn điện tử, email/SMS/Zalo ZNS, Google Analytics 4.
- Bán sỉ cơ bản: form báo giá cho cơ quan, doanh nghiệp, thư viện.
- Đặt trước (pre-order) ấn bản sắp phát hành.

### 3.2. Ngoài phạm vi (giai đoạn 1)

- Sách điện tử (e-book) và đọc trực tuyến có DRM — giai đoạn 2.
- Ứng dụng di động iOS/Android — giai đoạn 3.
- Marketplace cho nhà xuất bản khác bán trên website.
- Tư vấn pháp lý trực tuyến có thu phí.
- Bán hàng quốc tế, đa tiền tệ.
- Đồng bộ tự động với sàn TMĐT (Shopee, Tiki) — có thể cân nhắc ở giai đoạn 2.

### 3.3. Giả định

- Doanh nghiệp đã có pháp nhân, mã số thuế, tài khoản ngân hàng doanh nghiệp.
- Mọi đầu sách bán đều đã được NXB cấp phép và có ISBN.
- Nội dung (ảnh bìa, mục lục, đọc thử, mô tả) do ban biên tập cung cấp đầy đủ trước khi nhập lên web.
- Có sẵn kho và nhân sự đóng gói; đơn vị vận chuyển lấy hàng tại kho.

### 3.4. Ràng buộc

- Tuân thủ pháp luật Việt Nam về xuất bản, thương mại điện tử, bảo vệ dữ liệu cá nhân, hóa đơn điện tử (chi tiết ở mục 12).
- Ngân sách và thời gian ra mắt do chủ dự án chốt (đề xuất ở mục 14).
- Máy chủ đặt tại Việt Nam hoặc khu vực gần (Singapore) để đảm bảo tốc độ.

### 3.5. Thuật ngữ

| Thuật ngữ | Giải thích |
| --- | --- |
| Đầu sách (Title) | Một cuốn sách xét về nội dung, ví dụ "Bộ luật Dân sự" |
| Ấn bản (Edition) | Một lần phát hành của đầu sách, có năm, lần tái bản, ISBN riêng, cập nhật đến văn bản cụ thể |
| SKU | Mã hàng tồn kho, gắn với một ấn bản và một quy cách (bìa mềm/bìa cứng) |
| ISBN | Mã số sách tiêu chuẩn quốc tế, 13 chữ số |
| KHXB | Kế hoạch xuất bản; số xác nhận đăng ký xuất bản do Cục Xuất bản cấp |
| Lô in | Một đợt in của một ấn bản, có số lượng, ngày nhập kho |
| COD | Thanh toán khi nhận hàng |
| Pre-order | Đặt trước ấn bản chưa phát hành |
| Văn bản hợp nhất | Văn bản tổng hợp văn bản gốc và các văn bản sửa đổi, bổ sung |
| MVP | Phiên bản tối thiểu có thể ra mắt |

## 4. Các bên liên quan và chân dung khách hàng

Hệ thống phục vụ 4 nhóm khách hàng chính và 7 vai trò nội bộ; mỗi vai trò có quyền riêng trong trang quản trị.

### 4.1. Các bên liên quan

| Bên liên quan | Vai trò trong dự án | Mối quan tâm chính |
| --- | --- | --- |
| Ban giám đốc | Duyệt ngân sách, mục tiêu | Doanh thu, lợi nhuận, thương hiệu |
| Product Owner | Chốt yêu cầu, ưu tiên | Đúng nhu cầu, đúng tiến độ |
| Ban biên tập | Cung cấp nội dung sách | Thông tin sách chính xác, cập nhật |
| Kho – vận hành | Đóng gói, giao hàng | Đơn rõ ràng, in phiếu nhanh, tồn kho đúng |
| Chăm sóc khách hàng | Hỗ trợ, đổi trả | Tra cứu đơn nhanh, lịch sử khách |
| Kế toán | Đối soát, hóa đơn | Số liệu khớp, hóa đơn điện tử đúng |
| Marketing | Khuyến mãi, nội dung, SEO | Công cụ mã giảm giá, landing page, báo cáo |
| Đội phát triển | Xây dựng hệ thống | Yêu cầu rõ, tiêu chí nghiệm thu rõ |

### 4.2. Vai trò người dùng và quyền

| Vai trò | Mô tả | Quyền chính |
| --- | --- | --- |
| Khách vãng lai | Chưa đăng nhập | Xem, tìm sách, thêm giỏ, đặt hàng không cần tài khoản |
| Khách hàng thành viên | Đã đăng ký | Thêm: lịch sử đơn, sổ địa chỉ, yêu thích, đánh giá, tích điểm, nhận thông báo ấn bản mới |
| Khách hàng tổ chức | Cơ quan, doanh nghiệp, thư viện | Thêm: giá sỉ theo bậc, xuất hóa đơn công ty, thanh toán chuyển khoản sau |
| Super Admin | Chủ hệ thống | Toàn quyền, cấu hình, phân quyền |
| Quản lý sản phẩm | Ban biên tập | Thêm/sửa đầu sách, ấn bản, danh mục, nội dung |
| Quản lý đơn hàng | Vận hành | Xử lý đơn, vận đơn, đổi trả |
| Thủ kho | Kho | Nhập/xuất kho, kiểm kê |
| CSKH | Chăm sóc khách hàng | Xem đơn, khách hàng, trả lời đánh giá, tạo phiếu hỗ trợ |
| Marketing | Tiếp thị | Khuyến mãi, banner, bài viết, SEO |
| Kế toán | Tài chính | Đối soát thanh toán, COD, hóa đơn, báo cáo doanh thu |

### 4.3. Chân dung khách hàng (Personas)

| Persona | Mô tả | Nhu cầu | Nỗi đau | Website cần đáp ứng |
| --- | --- | --- | --- | --- |
| Sinh viên luật (18–24 tuổi) | Học tại các trường luật, ngân sách hạn chế | Giáo trình, bộ luật, sách ôn thi giá rẻ | Không biết bản nào mới nhất, sợ mua sách lậu | Lọc theo môn học, combo ôn thi, giá sinh viên, mua trên điện thoại |
| Luật sư, người làm pháp chế (25–50 tuổi) | Bận rộn, cần chính xác | Bộ luật mới nhất, sách chuyên khảo, chú giải | Mua phải bản cũ, mất thời gian đối chiếu | Ghi rõ "cập nhật đến", đăng ký thông báo ấn bản mới, giao nhanh |
| Kế toán, nhân sự doanh nghiệp | Cần tra cứu luật thuế, lao động, BHXH | Sách hướng dẫn thực hành, mẫu biểu | Văn bản thay đổi liên tục | Danh mục theo lĩnh vực nghề nghiệp, sách kèm mẫu biểu |
| Cơ quan nhà nước, thư viện, trường học | Mua số lượng lớn theo kế hoạch | Báo giá, hóa đơn, hợp đồng | Thủ tục mua sắm phức tạp | Form báo giá, giá sỉ, xuất hóa đơn, hồ sơ năng lực |
| Người dân | Cần giải quyết việc cụ thể (đất đai, hôn nhân, thừa kế) | Sách dễ hiểu, hỏi–đáp | Ngôn ngữ luật khó | Phân loại theo tình huống đời sống, đọc thử, mô tả dễ hiểu |

## 5. Quy trình nghiệp vụ

Mô hình tự sản xuất – tự bán gồm hai chuỗi nối nhau: chuỗi **sản xuất** (biên soạn → xuất bản → in → nhập kho) và chuỗi **bán hàng** (đặt hàng → xử lý → giao → hậu mãi); website quản lý từ lúc ấn bản có ISBN trở đi.

### 5.1. Quy trình phát hành ấn bản mới (BR-01)

1. Ban biên tập theo dõi văn bản pháp luật mới và quyết định làm ấn bản mới hoặc tái bản.
2. Biên soạn, hiệu đính; luật sư cộng tác soát nội dung.
3. Đăng ký KHXB và nhận quyết định xuất bản, ISBN từ NXB liên kết.
4. Quản lý sản phẩm tạo **ấn bản** trên Admin ở trạng thái `Sắp phát hành`, nhập: ISBN, NXB, năm, lần tái bản, "cập nhật đến" (tên + số hiệu + ngày văn bản mới nhất), ảnh bìa, mục lục, file đọc thử.
5. Bật **pre-order** nếu muốn nhận đặt trước; nhập ngày dự kiến phát hành.
6. Đặt in với xưởng in; nhập **lô in** dự kiến.
7. Hàng về kho: thủ kho kiểm đếm, nhập kho theo lô; trạng thái ấn bản chuyển `Đang bán`.
8. Hệ thống tự động: chuyển các đơn pre-order sang `Chờ xử lý`, gửi thông báo cho khách đã đăng ký nhận tin của đầu sách.
9. Ấn bản cũ chuyển `Ngừng bán` hoặc `Thanh lý` (giảm giá, gắn nhãn "Bản cũ — cập nhật đến …").

**Quy tắc nghiệp vụ**

- BR-02: Mỗi đầu sách tại một thời điểm có tối đa **một ấn bản ở trạng thái "Bản mới nhất"**; trang chi tiết luôn hiển thị bản mới nhất trước.
- BR-03: Ấn bản cũ còn tồn phải có cảnh báo nổi bật "Đã có ấn bản mới" kèm liên kết.
- BR-04: Không được bán ấn bản chưa có ISBN và số quyết định xuất bản.

### 5.2. Quy trình đặt hàng và xử lý đơn (BR-10)

1. Khách chọn sách, thêm vào giỏ, áp mã giảm giá (nếu có).
2. Nhập thông tin nhận hàng, chọn phương thức giao và thanh toán; hệ thống tính phí ship qua API đơn vị vận chuyển.
3. Khách xác nhận đặt hàng → đơn ở `Chờ thanh toán` (online) hoặc `Chờ xác nhận` (COD).
4. Thanh toán online thành công (nhận IPN từ cổng) → `Đã thanh toán`; quá 30 phút không thanh toán → `Đã hủy`, hoàn tồn kho tạm giữ.
5. Đơn COD giá trị lớn (≥ 2.000.000đ, giả định) hoặc khách mới: CSKH gọi xác nhận.
6. Kho in phiếu nhặt hàng, đóng gói, dán vận đơn → `Đang đóng gói` → `Chờ lấy hàng`.
7. Đơn vị vận chuyển lấy hàng → `Đang giao` (cập nhật qua webhook).
8. Giao thành công → `Đã giao`; sau 7 ngày không khiếu nại → `Hoàn tất`, cộng điểm thưởng.
9. Giao thất bại → `Hoàn hàng`, kho nhận lại và nhập kho.
10. Hóa đơn điện tử phát hành khi đơn `Đã giao` (hoặc ngay khi thanh toán, theo cấu hình kế toán).

**Quy tắc nghiệp vụ**

- BR-11: Tồn kho được **tạm giữ** khi đơn được tạo, trừ thật khi đóng gói, hoàn lại khi hủy.
- BR-12: Khách được tự hủy đơn khi đơn chưa sang `Đang đóng gói`.
- BR-13: Xuất kho theo nguyên tắc FIFO theo lô in.
- BR-14: Miễn phí vận chuyển cho đơn ≥ 300.000đ (giả định, cấu hình được).

### 5.3. Quy trình đổi trả (BR-20)

1. Khách gửi yêu cầu trong vòng **7 ngày** kể từ khi nhận hàng, chọn lý do, đính kèm ảnh.
2. CSKH duyệt trong 24 giờ làm việc.
3. Lý do được chấp nhận: sách lỗi in, thiếu trang, rách, giao sai sách/sai ấn bản. Không áp dụng cho "đổi ý" khi sách đã bóc màng co (cấu hình được).
4. Khách gửi sách về (doanh nghiệp chịu phí nếu lỗi do doanh nghiệp).
5. Kho kiểm tra → đổi sách mới hoặc hoàn tiền (về phương thức thanh toán gốc hoặc chuyển khoản trong 5–7 ngày làm việc).
6. Sách lỗi nhập kho "hàng lỗi", không bán lại.

### 5.4. Chương trình "Đổi sách cũ – Lấy bản mới" (BR-30, Should)

1. Khi có ấn bản mới, khách đã mua ấn bản cũ trên website nhận mã giảm giá riêng (ví dụ 20%).
2. Mã chỉ dùng cho ấn bản mới của chính đầu sách đó, hạn 60 ngày.
3. Hệ thống tự nhận diện khách đủ điều kiện qua lịch sử đơn.

### 5.5. Quy trình bán sỉ cho tổ chức (BR-40)

1. Tổ chức gửi form báo giá: danh sách sách, số lượng, thông tin xuất hóa đơn.
2. Bộ phận kinh doanh gửi báo giá (PDF tạo từ hệ thống) trong 1 ngày làm việc.
3. Tổ chức đồng ý → tạo đơn sỉ trên Admin, áp giá bậc thang (ví dụ: 10–49 cuốn −10%, 50–199 cuốn −15%, ≥ 200 cuốn −20%).
4. Thanh toán chuyển khoản trước hoặc công nợ theo hợp đồng; xuất hóa đơn công ty.
5. Số lượng lớn hơn tồn kho → kích hoạt đặt in bổ sung.

### 5.6. Quy trình nhập – xuất – kiểm kê kho (BR-50)

- Nhập kho: theo lô in (mã lô, ngày nhập, số lượng, giá vốn/cuốn).
- Xuất kho: tự động theo đơn bán; xuất thủ công cho biếu tặng, hủy hàng lỗi (bắt buộc ghi lý do).
- Cảnh báo tồn thấp: khi tồn ≤ ngưỡng tối thiểu từng SKU (mặc định 50 cuốn) → gửi email cho quản lý để cân nhắc in thêm.
- Kiểm kê định kỳ hằng tháng; chênh lệch được ghi nhận bằng phiếu điều chỉnh có người duyệt.

## 6. Yêu cầu chức năng

Hệ thống gồm 2 phân hệ: **Website khách hàng** (FR-01 → FR-59) và **Trang quản trị** (FR-60 → FR-99); mỗi yêu cầu có mức ưu tiên và tiêu chí chấp nhận để QA nghiệm thu.

### 6.1. Phân hệ Website khách hàng

**A. Tài khoản**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-01 | Đăng ký | Bằng email hoặc số điện thoại + mật khẩu; xác thực qua OTP (SMS/Zalo) hoặc link email | Must |
| FR-02 | Đăng nhập mạng xã hội | Google, Facebook, Zalo | Should |
| FR-03 | Đăng nhập / đăng xuất | Ghi nhớ đăng nhập 30 ngày; khóa 15 phút sau 5 lần sai | Must |
| FR-04 | Quên mật khẩu | Đặt lại qua OTP hoặc email, link hết hạn sau 15 phút | Must |
| FR-05 | Hồ sơ cá nhân | Họ tên, ngày sinh, nghề nghiệp (sinh viên/luật sư/kế toán/khác) để gợi ý sách | Must |
| FR-06 | Sổ địa chỉ | Nhiều địa chỉ, chọn mặc định; tỉnh/thành theo danh mục hành chính mới nhất | Must |
| FR-07 | Thông tin xuất hóa đơn | Lưu tên công ty, mã số thuế, địa chỉ, email nhận hóa đơn | Must |
| FR-08 | Xóa tài khoản | Khách tự yêu cầu xóa dữ liệu cá nhân (tuân thủ Luật Bảo vệ dữ liệu cá nhân) | Must |
| FR-09 | Tài khoản tổ chức | Đăng ký kèm giấy tờ; admin duyệt mới hưởng giá sỉ | Should |

**B. Duyệt và tìm kiếm sách**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-10 | Danh mục đa cấp | Theo lĩnh vực luật (Dân sự, Hình sự, Đất đai, Doanh nghiệp, Lao động, Thuế, Hành chính…), theo loại sách (Bộ luật, Văn bản hợp nhất, Chú giải, Hỏi–đáp, Giáo trình, Sách chuyên khảo), theo đối tượng (sinh viên, luật sư, doanh nghiệp, người dân) | Must |
| FR-11 | Tìm kiếm toàn văn | Theo tên sách, tác giả, ISBN, số hiệu văn bản (ví dụ "91/2015/QH13"); gợi ý khi gõ; không phân biệt dấu ("bo luat dan su" vẫn ra kết quả) | Must |
| FR-12 | Bộ lọc | Lĩnh vực, loại sách, năm phát hành, khoảng giá, bìa mềm/cứng, còn hàng, đang giảm giá, chỉ hiện bản mới nhất | Must |
| FR-13 | Sắp xếp | Mới nhất, bán chạy, giá tăng/giảm, đánh giá cao | Must |
| FR-14 | Tra cứu theo văn bản | Nhập số hiệu văn bản → danh sách sách đã cập nhật văn bản đó | Should |
| FR-15 | Gợi ý sách | "Thường mua cùng", "Cùng lĩnh vực", "Bạn đã xem" | Should |
| FR-16 | Combo / bộ sách | Ví dụ "Combo ôn thi luật sư", giá combo thấp hơn mua lẻ | Should |

**C. Trang chi tiết sách**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-20 | Thông tin cơ bản | Tên, ảnh bìa (nhiều ảnh, phóng to), giá bán, giá gốc, % giảm, tình trạng kho | Must |
| FR-21 | Thông tin xuất bản | Tác giả/nhóm biên soạn, NXB, ISBN, năm, lần tái bản, số trang, khổ sách, trọng lượng, loại bìa, số QĐXB | Must |
| FR-22 | Nhãn "Cập nhật đến" | Hiển thị nổi bật: "Cập nhật đến Nghị định xx/2026/NĐ-CP ngày dd/mm/2026" | Must |
| FR-23 | Chọn ấn bản | Chuyển giữa các ấn bản; ấn bản cũ có cảnh báo (BR-03) | Must |
| FR-24 | Mục lục và đọc thử | Mục lục dạng văn bản; đọc thử 10–20 trang PDF có watermark, không cho tải | Must |
| FR-25 | Mô tả | Giới thiệu, đối tượng phù hợp, điểm mới so với ấn bản trước | Must |
| FR-26 | Đánh giá | Sao 1–5 + bình luận; chỉ khách đã mua mới được đánh giá (nhãn "Đã mua hàng") | Should |
| FR-27 | Đăng ký nhận tin ấn bản mới | Nút "Báo tôi khi có bản mới" | Must |
| FR-28 | Hết hàng | Nút "Báo khi có hàng" thay cho "Thêm vào giỏ" | Should |
| FR-29 | Chia sẻ | Copy link, Facebook, Zalo | Could |

**D. Giỏ hàng và thanh toán**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-30 | Giỏ hàng | Thêm/xóa/sửa số lượng; lưu giỏ 30 ngày cho thành viên; gộp giỏ khi đăng nhập | Must |
| FR-31 | Giới hạn số lượng | Tối đa 20 cuốn/SKU/đơn lẻ; vượt mức gợi ý chuyển sang báo giá sỉ | Should |
| FR-32 | Mã giảm giá | Nhập mã; kiểm tra điều kiện; hiển thị số tiền giảm | Must |
| FR-33 | Thanh toán khách vãng lai | Không bắt buộc đăng ký | Must |
| FR-34 | Phí vận chuyển | Tính theo trọng lượng + địa chỉ qua API; hiển thị thời gian giao dự kiến | Must |
| FR-35 | Phương thức thanh toán | COD, VNPAY (thẻ ATM/Visa/Master/QR), MoMo, ZaloPay, chuyển khoản VietQR tự động đối soát | Must |
| FR-36 | Xuất hóa đơn | Tích "Xuất hóa đơn công ty" và nhập thông tin | Must |
| FR-37 | Xác nhận đơn | Trang cảm ơn + email/Zalo ZNS kèm mã đơn | Must |
| FR-38 | Pre-order | Đặt trước ấn bản sắp phát hành; bắt buộc thanh toán trước; tách đơn nếu giỏ có cả sách sẵn hàng | Should |

**E. Sau mua hàng**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-40 | Theo dõi đơn | Danh sách đơn + chi tiết trạng thái theo dòng thời gian; tra cứu đơn không cần đăng nhập bằng mã đơn + SĐT | Must |
| FR-41 | Hủy đơn | Theo BR-12 | Must |
| FR-42 | Yêu cầu đổi trả | Theo quy trình 5.3 | Must |
| FR-43 | Tải hóa đơn | Tải PDF/XML hóa đơn điện tử | Must |
| FR-44 | Tích điểm thành viên | 1 điểm / 10.000đ; 1 điểm = 100đ khi dùng; hạng Bạc/Vàng/Kim cương | Could |
| FR-45 | Danh sách yêu thích | Lưu sách để mua sau | Should |

**F. Nội dung và hỗ trợ**

| Mã | Chức năng | Mô tả chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-50 | Tin pháp luật / blog | Bài viết về văn bản mới, gắn link tới sách liên quan (phục vụ SEO) | Should |
| FR-51 | Trang tĩnh | Giới thiệu, liên hệ, chính sách đổi trả, vận chuyển, bảo mật, điều khoản, hướng dẫn mua hàng, FAQ | Must |
| FR-52 | Chat hỗ trợ | Nút Zalo OA / Messenger, hoặc chat trực tuyến | Must |
| FR-53 | Đăng ký nhận bản tin | Email nhận thông tin sách mới, khuyến mãi | Should |
| FR-54 | Form báo giá sỉ | Theo quy trình 5.5 | Must |
| FR-55 | Kiểm tra sách chính hãng | Nhập ISBN/mã tem → xác nhận sách do Sách Luật Việt Nam phát hành | Could |

### 6.2. Phân hệ Trang quản trị (Admin)

| Mã | Module | Chức năng chi tiết | Ưu tiên |
| --- | --- | --- | --- |
| FR-60 | Dashboard | Doanh thu hôm nay/tuần/tháng, số đơn theo trạng thái, top sách bán chạy, cảnh báo tồn thấp, đơn cần xử lý | Must |
| FR-61 | Quản lý đầu sách | CRUD đầu sách; slug SEO; meta title/description; gắn danh mục, tác giả, tag | Must |
| FR-62 | Quản lý ấn bản | CRUD ấn bản; ISBN (kiểm tra hợp lệ 13 số), QĐXB, "cập nhật đến", trạng thái (Nháp / Sắp phát hành / Đang bán / Ngừng bán / Thanh lý), upload đọc thử | Must |
| FR-63 | Quản lý SKU và giá | Giá gốc, giá bán, giá sỉ theo bậc, lịch giá (giảm giá có thời hạn) | Must |
| FR-64 | Danh mục, tác giả, tag | CRUD; sắp xếp kéo thả | Must |
| FR-65 | Nhập hàng loạt | Import/export Excel cho sách và giá | Should |
| FR-70 | Đơn hàng | Danh sách lọc theo trạng thái/ngày/phương thức; chi tiết; đổi trạng thái; ghi chú nội bộ; lịch sử thao tác | Must |
| FR-71 | Tạo vận đơn | Đẩy đơn sang đơn vị vận chuyển 1 click; in vận đơn + phiếu đóng gói hàng loạt | Must |
| FR-72 | Tạo đơn thủ công | Cho đơn điện thoại, đơn sỉ | Must |
| FR-73 | Đổi trả / hoàn tiền | Duyệt, từ chối, ghi nhận hoàn tiền | Must |
| FR-74 | Đối soát | COD từ đơn vị vận chuyển, giao dịch từ cổng thanh toán; đánh dấu chênh lệch | Must |
| FR-80 | Kho | Nhập kho theo lô in, xuất kho, điều chỉnh, kiểm kê, thẻ kho từng SKU, cảnh báo tồn thấp | Must |
| FR-81 | Lô in | Ghi nhận lô in: xưởng in, số lượng, giá vốn, ngày | Should |
| FR-85 | Khách hàng | Danh sách, chi tiết, lịch sử mua, phân nhóm (sinh viên, tổ chức…), duyệt tài khoản tổ chức | Must |
| FR-86 | Đánh giá | Duyệt/ẩn/trả lời đánh giá | Should |
| FR-90 | Khuyến mãi | Mã giảm giá (% hoặc số tiền, giới hạn lượt, đơn tối thiểu, theo danh mục/sách, theo nhóm khách), flash sale, combo, miễn phí ship | Must |
| FR-91 | Nội dung | Banner trang chủ, bài viết, trang tĩnh, menu (trình soạn thảo WYSIWYG) | Must |
| FR-92 | Thông báo | Mẫu email/SMS/ZNS; gửi thông báo ấn bản mới cho danh sách đăng ký | Must |
| FR-95 | Báo cáo | Doanh thu theo ngày/kênh/danh mục/đầu sách; lợi nhuận gộp (giá bán − giá vốn lô); tồn kho; khách hàng mới/quay lại; hiệu quả mã giảm giá; xuất Excel | Must |
| FR-97 | Người dùng nội bộ và phân quyền | Tạo tài khoản nhân viên, gán vai trò (RBAC) theo bảng 4.2; bắt buộc 2FA cho admin | Must |
| FR-98 | Nhật ký hệ thống | Log mọi thao tác sửa giá, tồn kho, đơn hàng, quyền: ai, lúc nào, giá trị cũ → mới | Must |
| FR-99 | Cấu hình | Thông tin công ty, phí ship, ngưỡng miễn ship, thời hạn đổi trả, kết nối cổng thanh toán/vận chuyển/hóa đơn | Must |

### 6.3. User stories và tiêu chí chấp nhận (mẫu cho các chức năng cốt lõi)

**US-01 — Tìm sách theo số hiệu văn bản (FR-11, FR-14)**

> Là một luật sư, tôi muốn gõ số hiệu văn bản để tìm sách đã cập nhật văn bản đó, nhằm chắc chắn mua đúng bản mới.

- Cho trước sách A có "cập nhật đến" chứa "45/2019/QH14", khi tôi tìm "45/2019/QH14" thì sách A nằm trong 3 kết quả đầu.
- Khi tôi gõ "bo luat lao dong" (không dấu) thì kết quả giống khi gõ có dấu.
- Thời gian trả kết quả ≤ 500 ms với 95% truy vấn.

**US-02 — Cảnh báo ấn bản cũ (FR-23, BR-03)**

> Là một sinh viên, tôi muốn biết sách tôi xem có phải bản mới nhất không, để không mua nhầm.

- Khi mở ấn bản không phải bản mới nhất, hiện thanh cảnh báo màu vàng phía trên giá, kèm nút "Xem ấn bản mới".
- Thêm ấn bản cũ vào giỏ thì hiện hộp xác nhận một lần.

**US-03 — Thanh toán VNPAY (FR-35)**

> Là khách hàng, tôi muốn thanh toán bằng QR ngân hàng để không phải trả tiền mặt.

- Sau khi thanh toán thành công, trạng thái đơn đổi sang `Đã thanh toán` trong ≤ 10 giây nhờ IPN, kể cả khi tôi đóng trình duyệt.
- Thanh toán thất bại: đơn giữ `Chờ thanh toán`, tôi được thanh toán lại trong 30 phút.
- Không bao giờ ghi nhận thanh toán chỉ dựa vào URL trả về (return URL); phải kiểm tra chữ ký IPN.

**US-04 — Đặt trước ấn bản mới (FR-38)**

> Là kế toán doanh nghiệp, tôi muốn đặt trước sách luật thuế mới để nhận ngay khi phát hành.

- Nút hiển thị "Đặt trước — dự kiến giao từ dd/mm/yyyy".
- Khi ấn bản nhập kho, đơn chuyển `Chờ xử lý` và tôi nhận thông báo trong ≤ 1 giờ.
- Nếu trễ phát hành quá 30 ngày so với dự kiến, tôi được hủy và hoàn tiền 100%.

**US-05 — Nhập kho theo lô (FR-80)**

> Là thủ kho, tôi muốn nhập kho theo lô in để biết giá vốn và xuất theo FIFO.

- Phiếu nhập bắt buộc: SKU, mã lô, số lượng, giá vốn, ngày.
- Sau khi duyệt phiếu, tồn khả dụng trên website tăng ngay.
- Thẻ kho hiển thị đầy đủ nhập/xuất/tồn theo từng lô.

## 7. Yêu cầu phi chức năng

Website phải tải trang dưới 2,5 giây trên 4G, chạy ổn định 99,5% thời gian và bảo vệ dữ liệu khách theo chuẩn OWASP; các chỉ tiêu cụ thể dưới đây là căn cứ nghiệm thu.

| Mã | Nhóm | Yêu cầu | Chỉ tiêu đo |
| --- | --- | --- | --- |
| NFR-01 | Hiệu năng | Thời gian tải trang (LCP) trên mobile 4G | ≤ 2,5 giây |
| NFR-02 | Hiệu năng | Core Web Vitals: INP, CLS | INP ≤ 200 ms; CLS ≤ 0,1 |
| NFR-03 | Hiệu năng | Thời gian phản hồi API (p95) | ≤ 300 ms (đọc), ≤ 800 ms (đặt hàng) |
| NFR-04 | Tải | Chịu tải đồng thời | 1.000 người dùng đồng thời; 50 đơn/phút giờ cao điểm (mùa thi, flash sale) |
| NFR-05 | Sẵn sàng | Uptime hằng tháng | ≥ 99,5% |
| NFR-06 | Mở rộng | Kiến trúc mở rộng ngang | Thêm máy chủ ứng dụng không cần sửa code |
| NFR-10 | Bảo mật | HTTPS toàn bộ, HSTS | TLS 1.2+ |
| NFR-11 | Bảo mật | Mật khẩu | Băm bcrypt/argon2; tối thiểu 8 ký tự |
| NFR-12 | Bảo mật | Chống tấn công phổ biến | Không có lỗi OWASP Top 10 mức cao khi pentest; chống SQL injection, XSS, CSRF |
| NFR-13 | Bảo mật | Giới hạn truy cập | Rate limit đăng nhập, OTP, tìm kiếm; reCAPTCHA/Turnstile ở form |
| NFR-14 | Bảo mật | Dữ liệu thẻ | Không lưu thông tin thẻ; mọi thanh toán thẻ qua cổng đạt PCI DSS |
| NFR-15 | Bảo mật | Quản trị | 2FA bắt buộc; whitelist IP tùy chọn; tự đăng xuất sau 30 phút không thao tác |
| NFR-16 | Bảo mật | File đọc thử | Watermark, chặn tải trực tiếp, link có hạn |
| NFR-20 | Dữ liệu | Sao lưu | DB tự động hằng ngày, giữ 30 ngày; lưu ở vùng khác; thử khôi phục mỗi quý |
| NFR-21 | Dữ liệu | Mục tiêu khôi phục | RPO ≤ 24 giờ; RTO ≤ 4 giờ |
| NFR-30 | SEO | URL thân thiện | `/sach/bo-luat-dan-su-2026` ; `/danh-muc/luat-dat-dai` |
| NFR-31 | SEO | Dữ liệu có cấu trúc | Schema.org `Book`, `Product`, `Offer`, `BreadcrumbList`, `Review` |
| NFR-32 | SEO | Kỹ thuật | Server-side rendering; sitemap.xml tự động; robots.txt; canonical cho ấn bản trùng; Open Graph |
| NFR-40 | Tương thích | Trình duyệt | 2 phiên bản mới nhất của Chrome, Safari, Edge, Firefox; Cốc Cốc |
| NFR-41 | Tương thích | Thiết bị | Mobile-first; màn hình từ 360 px |
| NFR-42 | Truy cập | Khả năng truy cập | WCAG 2.1 mức AA: tương phản, alt ảnh, điều hướng bàn phím, cỡ chữ tối thiểu 16 px |
| NFR-50 | Vận hành | Ghi log và giám sát | Log tập trung; cảnh báo khi lỗi 5xx > 1% hoặc cổng thanh toán lỗi |
| NFR-51 | Bảo trì | Chất lượng code | Unit test bao phủ ≥ 70% phần nghiệp vụ; code review bắt buộc; CI/CD |
| NFR-60 | Ngôn ngữ | Nội dung | Tiếng Việt; chuẩn bị cấu trúc đa ngôn ngữ (i18n) cho tương lai |

## 8. Sơ đồ trang và giao diện

Website có khoảng 20 loại trang; giao diện theo phong cách nghiêm túc, đáng tin cậy của ngành luật nhưng hiện đại, dễ đọc trên điện thoại.

### 8.1. Sơ đồ trang (sitemap)

- **Trang chủ** `/`
- **Danh mục** `/danh-muc/{slug}` — theo lĩnh vực, loại sách, đối tượng
- **Tìm kiếm** `/tim-kiem?q=`
- **Chi tiết sách** `/sach/{slug}` (tham số `?an-ban=` để chọn ấn bản)
- **Combo** `/combo/{slug}`
- **Sách sắp phát hành** `/sap-phat-hanh`
- **Khuyến mãi** `/khuyen-mai`
- **Giỏ hàng** `/gio-hang`
- **Thanh toán** `/thanh-toan` → **Đặt hàng thành công** `/dat-hang-thanh-cong/{ma-don}`
- **Tra cứu đơn** `/tra-cuu-don-hang`
- **Tài khoản** `/tai-khoan`
  - Hồ sơ, Sổ địa chỉ, Đơn hàng của tôi, Đổi trả, Yêu thích, Điểm thưởng, Thông báo ấn bản mới, Thông tin hóa đơn
- **Đăng nhập / Đăng ký / Quên mật khẩu**
- **Tin pháp luật** `/tin-phap-luat` và `/tin-phap-luat/{slug}`
- **Khách hàng tổ chức – Báo giá sỉ** `/bao-gia`
- **Trang tĩnh**: Giới thiệu, Liên hệ, Chính sách đổi trả, Chính sách vận chuyển, Chính sách thanh toán, Chính sách bảo mật, Điều khoản sử dụng, Hướng dẫn mua hàng, FAQ
- **Kiểm tra sách chính hãng** `/kiem-tra-chinh-hang`
- **404 / 500**

### 8.2. Mô tả giao diện các trang chính

**Header (mọi trang)**: logo Sách Luật Việt Nam; ô tìm kiếm lớn ở giữa (gợi ý khi gõ); menu danh mục dạng mega-menu (lĩnh vực / loại sách / đối tượng); biểu tượng tài khoản, giỏ hàng (số lượng); hotline. Mobile: header thu gọn, tìm kiếm cố định trên cùng, menu dạng ngăn kéo.

**Footer**: thông tin doanh nghiệp (tên, MST, địa chỉ, hotline, email), các chính sách, logo **"Đã thông báo Bộ Công Thương"**, phương thức thanh toán, mạng xã hội, đăng ký bản tin.

**Trang chủ** (từ trên xuống):

1. Banner chính (slider 3–5 ảnh): sách mới, khuyến mãi mùa thi.
2. Dải cam kết: "Bản mới nhất – Chính hãng – Giao nhanh 63 tỉnh – Đổi trả 7 ngày".
3. "Văn bản mới – Sách mới": sách vừa cập nhật theo luật mới có hiệu lực.
4. Danh mục nổi bật theo lĩnh vực (lưới biểu tượng).
5. Sách bán chạy.
6. Sắp phát hành (đặt trước).
7. Combo cho sinh viên / luật sư / doanh nghiệp.
8. Tin pháp luật mới.
9. Khối khách hàng tổ chức (CTA báo giá).

**Trang danh mục / tìm kiếm**: breadcrumb; tiêu đề + mô tả ngắn danh mục (SEO); bộ lọc bên trái (desktop) hoặc nút "Lọc" mở ngăn (mobile); lưới sách 4 cột desktop / 2 cột mobile; thẻ sách gồm ảnh bìa, tên, nhãn năm/ấn bản, giá, % giảm, nhãn "Mới nhất"; phân trang hoặc tải thêm.

**Trang chi tiết sách**: cột trái ảnh bìa + nút "Đọc thử"; cột phải tên sách, nhãn **"Cập nhật đến …"**, chọn ấn bản, chọn loại bìa, giá, số lượng, nút "Thêm vào giỏ" và "Mua ngay", phí ship dự kiến, cam kết; bên dưới các tab Mô tả / Mục lục / Thông tin xuất bản / Đánh giá; cuối trang sách liên quan. Mobile: thanh "Thêm vào giỏ" cố định dưới màn hình.

**Trang thanh toán**: 1 trang gồm 3 khối (Thông tin nhận hàng → Vận chuyển → Thanh toán) + tóm tắt đơn bên phải; không có menu điều hướng để giảm thoát trang; hiển thị rõ tổng tiền và phí trước khi bấm "Đặt hàng".

### 8.3. Hướng dẫn nhận diện (định hướng — chờ bộ nhận diện chính thức)

| Thành phần | Đề xuất |
| --- | --- |
| Màu chủ đạo | Xanh navy đậm (tin cậy, pháp lý) |
| Màu nhấn | Đỏ đô hoặc vàng đồng (gợi quốc huy, bìa sách luật) — dùng cho nút mua, giá |
| Màu nền | Trắng / xám rất nhạt |
| Phông chữ | Sans-serif hỗ trợ tiếng Việt tốt (Be Vietnam Pro, Inter); tiêu đề có thể dùng serif (Noto Serif) |
| Ảnh | Ảnh bìa nền trắng, tỷ lệ 3:4, tối thiểu 800×1067 px, WebP |
| Giọng văn | Chính xác, rõ ràng, không cường điệu; tránh lời hứa pháp lý thay luật sư |

Bàn giao thiết kế: wireframe → mockup Figma (desktop + mobile) → design system (màu, chữ, nút, thẻ sách, form) trước khi lập trình.

## 9. Kiến trúc hệ thống và công nghệ

Đề xuất kiến trúc **modular monolith** (một ứng dụng backend chia module rõ ràng) cho giai đoạn 1: đủ nhanh để ra mắt, dễ vận hành với đội nhỏ, có thể tách dịch vụ sau khi tải tăng.

&#91;embedded content: kiến trúc hệ thống · 6 lớp\]

Khách và nhân viên đều đi qua CDN; hai giao diện gọi chung một backend, việc chậm (email, hóa đơn, vận đơn) đẩy sang worker.

### 9.1. Các thành phần

| Lớp | Thành phần | Trách nhiệm |
| --- | --- | --- |
| Người dùng | Trình duyệt khách hàng, trình duyệt nhân viên | Truy cập website và trang quản trị |
| Biên | CDN + WAF (Cloudflare) | Cache ảnh/tĩnh, chống DDoS, chặn bot xấu |
| Giao diện | Storefront (Next.js, SSR) | Trang khách hàng, tối ưu SEO |
| Giao diện | Admin (Next.js hoặc React SPA) | Trang quản trị |
| Ứng dụng | Backend API (NestJS / Laravel) | Module: Auth, Catalog, Inventory, Cart, Order, Payment, Shipping, Promotion, Content, Notification, Report |
| Ứng dụng | Worker xử lý nền + hàng đợi | Gửi email/ZNS, đồng bộ vận đơn, phát hành hóa đơn, xuất báo cáo |
| Dữ liệu | PostgreSQL | Dữ liệu chính (giao dịch) |
| Dữ liệu | Redis | Cache, phiên, giỏ hàng, hàng đợi, rate limit |
| Dữ liệu | Meilisearch / Elasticsearch | Tìm kiếm không dấu, gợi ý |
| Dữ liệu | Object storage (S3-compatible) | Ảnh bìa, file đọc thử, hóa đơn PDF |
| Bên thứ ba | Cổng thanh toán, vận chuyển, hóa đơn điện tử, email/SMS/ZNS, GA4 | Xem mục 12 |

### 9.2. Công nghệ đề xuất

| Hạng mục | Lựa chọn chính | Phương án thay thế | Lý do |
| --- | --- | --- | --- |
| Frontend | Next.js (React, TypeScript) + Tailwind CSS | Nuxt (Vue) | SSR tốt cho SEO, cộng đồng lớn |
| Backend | NestJS (Node.js, TypeScript) | Laravel (PHP), Spring Boot | Chung ngôn ngữ với frontend, cấu trúc module rõ |
| CSDL | PostgreSQL 16 | MySQL 8 | Giao dịch chặt chẽ, JSONB, full-text |
| Tìm kiếm | Meilisearch | Elasticsearch | Nhẹ, hỗ trợ bỏ dấu, dễ vận hành |
| Cache/Queue | Redis + BullMQ | RabbitMQ | Đơn giản, đủ cho tải giai đoạn 1 |
| Hạ tầng | VPS/Cloud tại Việt Nam (Viettel IDC, VNG Cloud, FPT Cloud) hoặc AWS Singapore | Bizfly Cloud | Độ trễ thấp cho khách Việt Nam |
| Container | Docker + Docker Compose (giai đoạn 1); Kubernetes khi mở rộng | — | Triển khai nhất quán |
| CI/CD | GitHub Actions / GitLab CI | — | Tự động test, build, deploy |
| Giám sát | Sentry (lỗi), Grafana + Prometheus / Uptime Kuma | Datadog | Chi phí thấp |
| Phương án nhanh | WooCommerce hoặc Haravan/Sapo | — | Nếu ngân sách thấp, cần ra mắt trong 4–6 tuần; đổi lại khó tùy biến ấn bản, lô in |

### 9.3. Môi trường

| Môi trường | Mục đích | Dữ liệu |
| --- | --- | --- |
| Local | Lập trình viên phát triển | Dữ liệu mẫu |
| Staging | QA kiểm thử, khách hàng nghiệm thu (UAT) | Dữ liệu ẩn danh từ production; cổng thanh toán sandbox |
| Production | Chạy thật | Dữ liệu thật |

## 10. Thiết kế dữ liệu

Điểm khác biệt của mô hình dữ liệu là tách **Đầu sách → Ấn bản → SKU → Lô in**, để quản lý đúng việc sách luật được tái bản liên tục và tính giá vốn theo từng đợt in.

### 10.1. Quan hệ chính

- `books` (đầu sách) 1–n `editions` (ấn bản) 1–n `skus` (bìa mềm/cứng) 1–n `print_batches` (lô in).
- `editions` n–n `legal_documents` (văn bản pháp luật được cập nhật trong ấn bản) qua `edition_legal_documents`.
- `books` n–n `categories`, n–n `authors`.
- `users` 1–n `addresses`, 1–n `orders`; `orders` 1–n `order_items` (n–1 `skus`), 1–n `payments`, 1–n `shipments`, 1–n `order_status_logs`.
- `skus` 1–n `inventory_transactions` (thẻ kho).
- `promotions` 1–n `coupons`; `orders` n–n `coupons` qua `order_coupons`.

### 10.2. Bảng chi tiết (các bảng cốt lõi)

**books — Đầu sách**

| Trường | Kiểu | Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| id | BIGINT | PK |  |
| title | VARCHAR(255) | NOT NULL | Tên sách |
| slug | VARCHAR(255) | UNIQUE | URL |
| description | TEXT |  | Mô tả HTML |
| book\_type | ENUM | NOT NULL | code, consolidated, commentary, qna, textbook, monograph |
| target\_audience | VARCHAR\[\] |  | student, lawyer, business, citizen |
| meta\_title, meta\_description | VARCHAR |  | SEO |
| status | ENUM | NOT NULL | draft, active, hidden |
| created\_at, updated\_at | TIMESTAMP |  |  |

**editions — Ấn bản**

| Trường | Kiểu | Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| id | BIGINT | PK |  |
| book\_id | BIGINT | FK → books |  |
| edition\_name | VARCHAR(100) |  | Ví dụ "Tái bản lần 3 – 2026" |
| isbn | CHAR(13) | UNIQUE, NOT NULL | Kiểm tra checksum |
| publisher | VARCHAR(255) | NOT NULL | NXB liên kết |
| publishing\_decision\_no | VARCHAR(100) | NOT NULL | Số QĐXB |
| registration\_no | VARCHAR(100) |  | Số xác nhận ĐKXB |
| publish\_year | SMALLINT |  |  |
| updated\_to\_text | VARCHAR(500) |  | Nội dung nhãn "Cập nhật đến" |
| updated\_to\_date | DATE |  | Ngày văn bản mới nhất |
| pages, size, weight\_gram | INT / VARCHAR / INT |  | Số trang, khổ, trọng lượng (tính ship) |
| preview\_file\_url | VARCHAR |  | File đọc thử |
| release\_date | DATE |  | Ngày phát hành |
| is\_latest | BOOLEAN | Tối đa 1 bản/đầu sách | BR-02 |
| status | ENUM |  | draft, upcoming, on\_sale, discontinued, clearance |

**skus — Mã hàng**

| Trường | Kiểu | Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| id | BIGINT | PK |  |
| edition\_id | BIGINT | FK → editions |  |
| sku\_code | VARCHAR(50) | UNIQUE | Ví dụ SLVN-BLDS-2026-BM |
| cover\_type | ENUM |  | paperback, hardcover |
| list\_price | DECIMAL(12,0) | NOT NULL | Giá bìa |
| sale\_price | DECIMAL(12,0) | NOT NULL | Giá bán |
| stock\_available | INT | ≥ 0 | Tồn khả dụng |
| stock\_reserved | INT | ≥ 0 | Tạm giữ (BR-11) |
| low\_stock\_threshold | INT | Mặc định 50 |  |
| allow\_preorder | BOOLEAN |  |  |

**print\_batches — Lô in**

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| id | BIGINT | PK |
| sku\_id | BIGINT | FK → skus |
| batch\_code | VARCHAR(50) | Mã lô |
| printer\_name | VARCHAR(255) | Xưởng in |
| quantity | INT | Số lượng in |
| unit\_cost | DECIMAL(12,0) | Giá vốn/cuốn |
| received\_at | DATE | Ngày nhập kho |
| remaining | INT | Còn lại (FIFO) |

**legal\_documents — Văn bản pháp luật**

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| id | BIGINT | PK |
| doc\_number | VARCHAR(50) | Số hiệu, ví dụ 91/2015/QH13 (có chỉ mục tìm kiếm) |
| doc\_type | ENUM | Luật, Bộ luật, Nghị định, Thông tư… |
| title | VARCHAR(500) | Trích yếu |
| issued\_date, effective\_date | DATE | Ngày ban hành, hiệu lực |

**orders — Đơn hàng**

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| id | BIGINT | PK |
| order\_code | VARCHAR(20) | UNIQUE, ví dụ SLVN2610230001 |
| user\_id | BIGINT | FK → users, NULL nếu khách vãng lai |
| customer\_name, phone, email | VARCHAR | Snapshot lúc đặt |
| shipping\_address | JSONB | Snapshot địa chỉ |
| subtotal, discount, shipping\_fee, total | DECIMAL(12,0) |  |
| payment\_method | ENUM | cod, vnpay, momo, zalopay, bank\_transfer |
| payment\_status | ENUM | unpaid, paid, refunded, partial\_refund |
| status | ENUM | Xem phụ lục 16.1 |
| order\_type | ENUM | retail, wholesale, preorder |
| invoice\_required | BOOLEAN |  |
| invoice\_info | JSONB | Tên công ty, MST, địa chỉ, email |
| note | TEXT | Ghi chú khách |
| created\_at | TIMESTAMP |  |

**order\_items**: id, order\_id, sku\_id, snapshot tên sách + ấn bản + ISBN, unit\_price, quantity, line\_total, cost\_price (giá vốn theo lô đã xuất).

**payments**: id, order\_id, provider, transaction\_ref, amount, status, raw\_response (JSONB), paid\_at.

**shipments**: id, order\_id, carrier, tracking\_code, fee, cod\_amount, status, events (JSONB), delivered\_at.

**inventory\_transactions**: id, sku\_id, batch\_id, type (import, sale, return, adjust, damage, gift), quantity (+/−), reference (mã đơn/phiếu), created\_by, note, created\_at.

**Các bảng khác**: users, roles, permissions, user\_roles, addresses, categories, book\_categories, authors, book\_authors, reviews, wishlists, carts, cart\_items, promotions, coupons, order\_coupons, return\_requests, invoices, posts, pages, banners, subscriptions (đăng ký nhận tin ấn bản mới), loyalty\_points, audit\_logs, settings.

### 10.3. Nguyên tắc dữ liệu

- Đơn hàng lưu **snapshot** giá, tên sách, địa chỉ tại thời điểm đặt; sửa sách sau đó không làm đổi đơn cũ.
- Tiền lưu dạng số nguyên VNĐ (DECIMAL không phần lẻ), không dùng FLOAT.
- Không xóa cứng đơn hàng, giao dịch kho, thanh toán; chỉ xóa mềm (`deleted_at`) các danh mục nội dung.
- Cập nhật tồn kho dùng transaction + khóa dòng (row lock) để tránh bán âm khi nhiều người mua cùng lúc.

## 11. Thiết kế API

API theo chuẩn REST, JSON, tiền tố `/api/v1`; xác thực bằng JWT (access token 15 phút + refresh token 30 ngày trong cookie HttpOnly); tài liệu tự sinh bằng OpenAPI/Swagger.

### 11.1. Quy ước chung

- Phân trang: `?page=1&limit=24`; trả về `meta: {page, limit, total}`.
- Định dạng lỗi thống nhất: `{"error": {"code": "OUT_OF_STOCK", "message": "Sách đã hết hàng", "details": {...}}}`.
- Mã HTTP: 200/201 thành công; 400 dữ liệu sai; 401 chưa đăng nhập; 403 không có quyền; 404 không tìm thấy; 409 xung đột (hết hàng, trùng ISBN); 422 lỗi nghiệp vụ; 429 quá nhiều yêu cầu; 500 lỗi hệ thống.
- API tạo đơn và thanh toán nhận header `Idempotency-Key` để tránh tạo trùng đơn khi khách bấm nhiều lần.

### 11.2. API khách hàng (Storefront)

| Phương thức | Endpoint | Mô tả | Xác thực |
| --- | --- | --- | --- |
| POST | /auth/register | Đăng ký | Không |
| POST | /auth/login | Đăng nhập | Không |
| POST | /auth/otp/send, /auth/otp/verify | Gửi / xác minh OTP | Không |
| POST | /auth/refresh, /auth/logout | Làm mới token / đăng xuất | Có |
| GET | /categories | Cây danh mục | Không |
| GET | /books | Danh sách sách (lọc, sắp xếp, phân trang) | Không |
| GET | /books/{slug} | Chi tiết sách + các ấn bản + SKU | Không |
| GET | /search?q= | Tìm kiếm; /search/suggest gợi ý | Không |
| GET | /legal-documents/{number}/books | Sách theo số hiệu văn bản | Không |
| GET/POST/PATCH/DELETE | /cart, /cart/items | Giỏ hàng | Tùy chọn |
| POST | /cart/apply-coupon | Áp mã giảm giá | Tùy chọn |
| POST | /shipping/quote | Tính phí ship | Không |
| POST | /orders | Tạo đơn | Tùy chọn |
| GET | /orders, /orders/{code} | Đơn của tôi / chi tiết | Có |
| POST | /orders/lookup | Tra cứu đơn bằng mã + SĐT | Không |
| POST | /orders/{code}/cancel | Hủy đơn | Có |
| POST | /orders/{code}/returns | Yêu cầu đổi trả | Có |
| POST | /payments/{code}/init | Tạo link thanh toán | Tùy chọn |
| GET/POST | /me, /me/addresses, /me/wishlist, /me/subscriptions | Tài khoản | Có |
| POST | /books/{id}/reviews | Đánh giá | Có |
| POST | /wholesale-quotes | Gửi yêu cầu báo giá sỉ | Không |

### 11.3. API quản trị (Admin)

Tiền tố `/api/v1/admin`, mọi endpoint kiểm tra quyền theo vai trò: `/books`, `/editions`, `/skus`, `/print-batches`, `/inventory/transactions`, `/inventory/stocktakes`, `/orders` (+ `/orders/{id}/status`, `/orders/{id}/shipments`), `/returns`, `/customers`, `/reviews`, `/promotions`, `/coupons`, `/posts`, `/banners`, `/reports/{type}`, `/users`, `/roles`, `/audit-logs`, `/settings`, `/imports` (Excel).

### 11.4. Webhook nhận từ bên thứ ba

| Endpoint | Nguồn | Xử lý |
| --- | --- | --- |
| POST /webhooks/vnpay/ipn | VNPAY | Kiểm tra chữ ký (checksum), số tiền, trạng thái → cập nhật thanh toán; trả mã phản hồi đúng định dạng VNPAY |
| POST /webhooks/momo/ipn | MoMo | Tương tự |
| POST /webhooks/zalopay/callback | ZaloPay | Tương tự |
| POST /webhooks/bank-transfer | Dịch vụ đối soát chuyển khoản (Casso, SePay…) | Khớp nội dung chuyển khoản chứa mã đơn và số tiền |
| POST /webhooks/shipping/{carrier} | GHN, GHTK, Viettel Post | Cập nhật trạng thái vận đơn, ghi sự kiện |

Mọi webhook phải: kiểm tra chữ ký/secret, xử lý idempotent (nhận trùng không ghi trùng), ghi log thô, trả phản hồi trong ≤ 5 giây (việc nặng đẩy sang hàng đợi).

## 12. Thanh toán, vận chuyển và tích hợp bên thứ ba

Giai đoạn 1 cần 6 nhóm tích hợp; mỗi tích hợp đều phải có tài khoản sandbox để kiểm thử trước khi chạy thật. Phí dịch vụ dưới đây là mức tham khảo, cần lấy báo giá chính thức khi ký hợp đồng.

| Nhóm | Nhà cung cấp đề xuất | Mục đích | Ghi chú |
| --- | --- | --- | --- |
| Cổng thanh toán | VNPAY | Thẻ ATM nội địa, Visa/Master, VNPAY-QR | Cần hồ sơ doanh nghiệp + website đã thông báo Bộ Công Thương |
| Ví điện tử | MoMo, ZaloPay | Thanh toán ví | Should; tăng chuyển đổi nhóm sinh viên |
| Chuyển khoản tự động | VietQR + Casso/SePay | Mã QR có sẵn số tiền + nội dung mã đơn, tự đối soát | Phí thấp, phù hợp đơn sỉ |
| COD | Qua đơn vị vận chuyển | Thu hộ | Đối soát COD hằng tuần |
| Vận chuyển | GHN, GHTK, Viettel Post (chọn 1–2) | Tính phí, tạo vận đơn, webhook trạng thái | Admin chọn đơn vị theo tỉnh; sách nặng nên chú ý bảng giá theo kg |
| Hóa đơn điện tử | VNPT Invoice, Viettel S-Invoice, MISA meInvoice, EasyInvoice | Phát hành hóa đơn qua API | Theo quy định hóa đơn điện tử hiện hành |
| Email giao dịch | Amazon SES, SendGrid, Brevo | Xác nhận đơn, đặt lại mật khẩu | Cấu hình SPF, DKIM, DMARC cho tên miền |
| SMS / Zalo ZNS | eSMS, SpeedSMS, Zalo ZNS | OTP, thông báo đơn | ZNS rẻ hơn SMS, cần Zalo OA đã xác thực |
| Phân tích | Google Analytics 4, Google Search Console, Facebook Pixel / Conversions API | Đo lường, quảng cáo | Bật theo đồng ý cookie của khách |
| Chat | Zalo OA, Facebook Messenger | Hỗ trợ khách |  |
| Đăng nhập mạng xã hội | Google, Facebook, Zalo | Đăng nhập nhanh | Should |

### 12.1. Luồng thanh toán online (áp dụng chung)

1. Khách bấm "Đặt hàng" → backend tạo đơn `Chờ thanh toán`, tạm giữ tồn kho.
2. Backend tạo URL/mã QR thanh toán có chữ ký, hạn 15–30 phút → chuyển khách sang cổng.
3. Khách thanh toán; cổng gọi **IPN** về backend.
4. Backend kiểm tra chữ ký, mã đơn, số tiền khớp → đánh dấu `Đã thanh toán` (idempotent).
5. Khách được chuyển về trang kết quả; trang này **chỉ hiển thị** trạng thái lấy từ backend.
6. Hết hạn chưa thanh toán → job tự hủy đơn và trả tồn kho.
7. Hằng ngày: job đối soát tự động giữa giao dịch trong hệ thống và báo cáo của cổng.

### 12.2. Quy tắc vận chuyển

- Trọng lượng đơn = tổng `weight_gram` các sách + 100 g bao bì (cấu hình được).
- Đóng gói chống ẩm, chống móp góc (sách luật bìa cứng nặng, dễ gãy góc).
- Thời gian giao dự kiến: nội thành HN/HCM 1–2 ngày; tỉnh 2–5 ngày (theo báo giá đơn vị vận chuyển).
- Cho khách xem hàng trước khi nhận (cấu hình trên vận đơn).

## 13. Pháp lý và tuân thủ

Website là nền tảng TMĐT **kinh doanh trực tiếp** (người bán tự bán hàng của mình), phải tuân thủ Luật Thương mại điện tử 2025 (hiệu lực 01/7/2026) và Luật Bảo vệ dữ liệu cá nhân 2025 (hiệu lực 01/01/2026), cùng quy định về xuất bản và hóa đơn. Mục này là danh sách việc cần làm; doanh nghiệp nên nhờ luật sư rà soát trước khi ra mắt.

### 13.1. Căn cứ pháp lý chính

| Văn bản | Nội dung áp dụng cho dự án | Nguồn |
| --- | --- | --- |
| Luật Thương mại điện tử số 122/2025/QH15 — hiệu lực 01/7/2026 | Nghĩa vụ của nền tảng TMĐT kinh doanh trực tiếp: thông báo/đăng ký, công khai thông tin, chính sách, bảo vệ người tiêu dùng | [Thư viện Pháp luật](https://thuvienphapluat.vn/van-ban/Thuong-mai/Luat-Thuong-mai-dien-tu-2025-so-122-2025-QH15-662035.aspx?tab=3) |
| Nghị định 248/2026/NĐ-CP — hiệu lực 01/7/2026 | Quy định chi tiết Luật TMĐT (thủ tục, nội dung phải công khai) | [LuatVietnam](https://luatvietnam.vn/bai-viet-lien-quan/luat-122-2025-qh15-423356.html) |
| Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 — hiệu lực 01/01/2026 | Sự đồng ý, quyền của chủ thể dữ liệu, nghĩa vụ bên xử lý, thông báo vi phạm; cấm mua bán dữ liệu cá nhân | [Thư viện Pháp luật](https://thuvienphapluat.vn/van-ban/Bo-may-hanh-chinh/Law-91-2025-QH15-Personal-Data-Protection-665440.aspx), [Ngân hàng Nhà nước](https://sbv.gov.vn/en/w/tri%E1%BB%83n-khai-thi-h%C3%A0nh-lu%E1%BA%ADt-b%E1%BA%A3o-v%E1%BB%87-d%E1%BB%AF-li%E1%BB%87u-c%C3%A1-nh%C3%A2n-1) |
| Nghị định 356/2025/NĐ-CP — hiệu lực 01/01/2026 | Hướng dẫn Luật BVDLCN: nhân sự/bộ phận bảo vệ dữ liệu, điều kiện xử lý | [Thư viện Pháp luật](https://thuvienphapluat.vn/van-ban/Quyen-dan-su/Nghi-dinh-356-2025-ND-CP-huong-dan-Luat-Bao-ve-du-lieu-ca-nhan-687428.aspx) |
| Luật Xuất bản và văn bản hướng dẫn (cần kiểm tra bản hợp nhất mới nhất) | Sách phát hành phải có quyết định xuất bản của NXB; thông tin bắt buộc ghi trên sách; hoạt động phát hành xuất bản phẩm | Cần luật sư xác nhận |
| Quy định về hóa đơn điện tử (Nghị định 123/2020/NĐ-CP và văn bản sửa đổi — cần kiểm tra bản hiện hành) | Xuất hóa đơn điện tử cho từng đơn bán hàng | Cần kế toán xác nhận |
| Luật Bảo vệ quyền lợi người tiêu dùng 2023 | Thông tin minh bạch, điều kiện giao dịch chung, đổi trả, giải quyết khiếu nại | Cần luật sư xác nhận |
| Luật Sở hữu trí tuệ | Bản quyền nội dung biên soạn; bảo hộ nhãn hiệu "Sách Luật Việt Nam" | Nên nộp đơn đăng ký nhãn hiệu sớm |

### 13.2. Việc phải làm trước khi ra mắt

- [ ] Thực hiện thủ tục thông báo/đăng ký website TMĐT với Bộ Công Thương theo Luật TMĐT 2025 và Nghị định 248/2026/NĐ-CP; gắn logo xác nhận ở footer.
- [ ] Công khai trên website: tên doanh nghiệp, MST, địa chỉ trụ sở, số điện thoại, email; người đại diện.
- [ ] Công khai chính sách: điều kiện giao dịch chung, đổi trả – hoàn tiền, vận chuyển, thanh toán, bảo mật/xử lý dữ liệu cá nhân, giải quyết khiếu nại.
- [ ] Trang chi tiết sách hiển thị đủ thông tin xuất bản (NXB, ISBN, QĐXB) — trùng với trường bắt buộc FR-21.
- [ ] Giá hiển thị rõ đã gồm thuế hay chưa; phí vận chuyển hiển thị trước khi khách xác nhận đơn.
- [ ] Đăng ký nhãn hiệu "Sách Luật Việt Nam" tại Cục Sở hữu trí tuệ.
- [ ] Ký hợp đồng với cổng thanh toán, đơn vị vận chuyển, nhà cung cấp hóa đơn điện tử.
- [ ] Chỉ định nhân sự/bộ phận bảo vệ dữ liệu cá nhân theo Nghị định 356/2025/NĐ-CP.

### 13.3. Yêu cầu hệ thống để tuân thủ bảo vệ dữ liệu cá nhân

| Yêu cầu | Cách hệ thống đáp ứng |
| --- | --- |
| Thu thập có sự đồng ý | Checkbox đồng ý chính sách xử lý dữ liệu khi đăng ký/đặt hàng (không tích sẵn); lưu thời điểm, phiên bản chính sách |
| Đồng ý riêng cho marketing | Checkbox riêng cho email/ZNS quảng cáo; rút đồng ý 1 click |
| Cookie | Banner cho chọn cookie cần thiết / phân tích / quảng cáo; GA4 và Pixel chỉ chạy khi được đồng ý |
| Quyền của khách | Xem, sửa, tải dữ liệu, yêu cầu xóa tài khoản (FR-08); xử lý trong thời hạn luật định |
| Tối thiểu hóa | Chỉ thu thập dữ liệu cần cho giao hàng, hóa đơn; không thu CCCD của khách lẻ |
| Bảo mật | Mã hóa khi truyền (HTTPS) và khi lưu với dữ liệu nhạy cảm; phân quyền truy cập; log truy cập dữ liệu khách |
| Thời hạn lưu trữ | Dữ liệu đơn hàng lưu theo thời hạn kế toán – thuế; tài khoản không hoạt động quá thời hạn quy định thì ẩn danh hóa |
| Sự cố | Quy trình thông báo vi phạm dữ liệu cho cơ quan chức năng và khách hàng theo thời hạn luật định |
| Bên thứ ba | Hợp đồng xử lý dữ liệu với nhà cung cấp email, SMS, vận chuyển; không chia sẻ dữ liệu cho mục đích khác |

### 13.4. Lưu ý nội dung

- Sách và bài viết là tài liệu tham khảo, không thay thế tư vấn pháp lý; ghi miễn trừ trách nhiệm ở trang tin pháp luật.
- Không quảng cáo kiểu "duy nhất", "tốt nhất" nếu không chứng minh được.
- Đánh giá của khách phải thật; không tạo đánh giá giả.

## 14. Kiểm thử, triển khai và vận hành

Không ra mắt khi còn lỗi mức Nghiêm trọng hoặc Cao; mọi luồng đặt hàng – thanh toán – giao hàng phải chạy thông trên staging với tiền thật số nhỏ trước khi mở cho khách.

### 14.1. Các loại kiểm thử

| Loại | Phạm vi | Người thực hiện | Công cụ gợi ý |
| --- | --- | --- | --- |
| Unit test | Hàm nghiệp vụ: tính giá, khuyến mãi, tồn kho, phí ship | Lập trình viên | Jest / PHPUnit |
| Integration test | API + CSDL + webhook giả lập | Lập trình viên | Supertest, Postman/Newman |
| End-to-end (E2E) | Luồng khách: tìm → giỏ → thanh toán → theo dõi đơn | QA | Playwright / Cypress |
| Kiểm thử thủ công | Theo test case từ tiêu chí chấp nhận mục 6.3 | QA | Bảng test case |
| Hiệu năng / tải | Trang chủ, tìm kiếm, đặt hàng ở 1.000 người đồng thời | QA / DevOps | k6, JMeter |
| Bảo mật | OWASP Top 10, phân quyền admin | Bên thứ ba hoặc chuyên gia | OWASP ZAP, Burp Suite |
| Tương thích | Trình duyệt, thiết bị theo NFR-40/41 | QA | BrowserStack, máy thật |
| UAT | Nghiệp vụ thực tế: biên tập nhập sách, kho xử lý đơn, kế toán đối soát | Người dùng nội bộ | Kịch bản UAT |

**Mức độ lỗi**: Nghiêm trọng (mất tiền, sai đơn, sập hệ thống) — sửa ngay; Cao (chức năng chính không dùng được) — sửa trước ra mắt; Trung bình — sửa trong 1 tuần; Thấp (giao diện nhỏ) — xếp lịch.

**Test case bắt buộc (ví dụ)**

- Hai khách cùng mua cuốn cuối cùng: chỉ một đơn thành công, đơn kia báo hết hàng.
- Cổng thanh toán gửi IPN trùng 3 lần: đơn chỉ ghi nhận thanh toán 1 lần.
- Mã giảm giá hết lượt dùng giữa lúc khách đang thanh toán: báo lỗi rõ, không giảm sai.
- Khách đổi địa chỉ sang tỉnh khác: phí ship tính lại đúng.
- Ấn bản mới chuyển "Bản mới nhất": ấn bản cũ tự hiện cảnh báo.

### 14.2. Triển khai

1. Mã nguồn trên Git, nhánh `main` (production), `develop` (staging), nhánh tính năng.
2. Mỗi Pull Request: chạy lint + test tự động, ít nhất 1 người review.
3. Merge vào `develop` → tự động deploy staging.
4. Tạo bản release (tag phiên bản) → deploy production ngoài giờ cao điểm (sau 22:00).
5. Migration CSDL có kịch bản rollback; sao lưu trước mỗi lần deploy.
6. Có khả năng quay lui về phiên bản trước trong ≤ 15 phút.

### 14.3. Vận hành và giám sát

| Hạng mục | Cách làm | Ngưỡng cảnh báo |
| --- | --- | --- |
| Uptime | Ping 1 phút/lần | Website không phản hồi 2 lần liên tiếp |
| Lỗi ứng dụng | Sentry | Lỗi mới hoặc tăng đột biến |
| Tài nguyên máy chủ | CPU, RAM, ổ đĩa | CPU > 80% trong 10 phút; ổ đĩa > 85% |
| Nghiệp vụ | Số đơn mỗi giờ, tỷ lệ thanh toán thất bại | 0 đơn trong 3 giờ ban ngày; thất bại > 20% |
| Bảo mật | Log đăng nhập admin, WAF | Đăng nhập lạ, nhiều yêu cầu bất thường |
| Chứng chỉ SSL, tên miền | Theo dõi hạn | Còn < 30 ngày |

**Bảo trì và hỗ trợ sau ra mắt**: 3 tháng bảo hành sửa lỗi miễn phí (đề xuất trong hợp đồng); cam kết phản hồi sự cố Nghiêm trọng trong 1 giờ, Cao trong 4 giờ làm việc; cập nhật bảo mật thư viện hằng tháng.

**Tài liệu bàn giao**: mã nguồn, tài liệu API (Swagger), sơ đồ hạ tầng, hướng dẫn deploy, tài khoản hệ thống, hướng dẫn sử dụng Admin (có video) cho từng vai trò.

## 15. Kế hoạch dự án

Đề xuất ra mắt MVP sau khoảng **20 tuần** (5 tháng) với đội 8–9 người; các mốc, chi phí dưới đây là ước tính ban đầu để chủ dự án chốt.

### 15.1. Lộ trình

| Giai đoạn | Thời lượng | Đầu ra | Điều kiện qua giai đoạn |
| --- | --- | --- | --- |
| 0. Khởi động và phân tích | Tuần 1–3 | Tài liệu này được duyệt, backlog, danh sách dữ liệu sách | Chủ dự án ký duyệt phạm vi |
| 1. Thiết kế | Tuần 3–6 | Wireframe, mockup Figma, design system, thiết kế CSDL, API | Duyệt mockup trang chủ, chi tiết, thanh toán |
| 2. Phát triển lõi | Tuần 6–14 | Catalog, tìm kiếm, giỏ, đặt hàng, Admin sách/đơn/kho (sprint 2 tuần) | Demo cuối mỗi sprint |
| 3. Tích hợp | Tuần 12–16 | Thanh toán, vận chuyển, hóa đơn, email/ZNS | Giao dịch sandbox thông suốt |
| 4. Kiểm thử và UAT | Tuần 16–19 | Báo cáo test, sửa lỗi, nhập dữ liệu sách thật | Không còn lỗi Nghiêm trọng/Cao; UAT ký duyệt |
| 5. Ra mắt | Tuần 20 | Go-live, chạy thử mềm (soft launch) 1–2 tuần | Thủ tục Bộ Công Thương hoàn tất |
| Giai đoạn 2 | Sau ra mắt 3–6 tháng | E-book, tích điểm đầy đủ, đồng bộ sàn TMĐT, gợi ý thông minh | Theo KPI giai đoạn 1 |

### 15.2. Nhân sự

| Vai trò | Số lượng | Thời gian tham gia |
| --- | --- | --- |
| Project Manager / Scrum Master | 1 | Toàn dự án |
| Business Analyst | 1 | Tuần 1–8, hỗ trợ đến UAT |
| UI/UX Designer | 1 | Tuần 3–10 |
| Frontend Developer | 2 | Tuần 5–20 |
| Backend Developer | 2 | Tuần 5–20 |
| QA / Tester | 1 | Tuần 8–20 |
| DevOps (bán thời gian) | 1 | Tuần 5 trở đi |
| Phía doanh nghiệp: biên tập nhập liệu, kho, kế toán, CSKH | 3–5 | UAT và nhập dữ liệu |

### 15.3. Ngân sách ước tính (tham khảo, chưa có báo giá)

| Hạng mục | Ước tính | Ghi chú |
| --- | --- | --- |
| Phát triển (thuê ngoài hoặc đội nội bộ) | khoảng 600 triệu – 1,2 tỷ đồng | Tùy đơn vị, mức tùy biến; dùng nền tảng có sẵn (Haravan/WooCommerce) có thể dưới 150 triệu |
| Hạ tầng (máy chủ, CDN, lưu trữ) | khoảng 5–15 triệu đồng/tháng | Tăng theo lưu lượng |
| Dịch vụ bên thứ ba | Theo giao dịch | Phí cổng thanh toán theo %, SMS/ZNS theo tin, hóa đơn điện tử theo gói |
| Tên miền, SSL, email doanh nghiệp | khoảng 3–10 triệu đồng/năm | sachluatvietnam.vn / .com.vn (kiểm tra còn trống) |
| Pentest | khoảng 30–80 triệu đồng/lần | Trước ra mắt |
| Marketing ra mắt | Theo kế hoạch marketing | Ngoài phạm vi tài liệu |
| Dự phòng | 10–15% tổng |  |

### 15.4. Rủi ro và phương án

| Rủi ro | Khả năng | Ảnh hưởng | Phương án |
| --- | --- | --- | --- |
| Dữ liệu sách (ảnh, mô tả, "cập nhật đến") không sẵn sàng | Cao | Cao | Ban biên tập nhập dữ liệu song song từ tuần 6; mẫu Excel import |
| Thủ tục Bộ Công Thương, hợp đồng cổng thanh toán chậm | Trung bình | Cao | Nộp hồ sơ ngay khi có bản staging công khai; ra mắt với COD + chuyển khoản trước |
| Phạm vi phình to giữa chừng | Cao | Trung bình | Chốt MVP; yêu cầu mới vào backlog giai đoạn 2 qua quy trình thay đổi |
| Luật thay đổi làm sách tồn thành bản cũ | Cao | Trung bình | Chức năng thanh lý, đổi sách cũ lấy bản mới; in lô nhỏ, cảnh báo tồn |
| Quá tải mùa thi / flash sale | Trung bình | Cao | Test tải, CDN, cache, giới hạn số lượng |
| Sách lậu cạnh tranh giá | Cao | Trung bình | Tem chống giả, kiểm tra chính hãng, truyền thông về bản quyền |
| Phụ thuộc 1 lập trình viên chủ chốt | Trung bình | Cao | Code review chéo, tài liệu kỹ thuật, bàn giao đầy đủ |

## 16. Phụ lục

### 16.1. Trạng thái đơn hàng

| Mã trạng thái | Tên hiển thị | Ý nghĩa | Chuyển tiếp được sang |
| --- | --- | --- | --- |
| pending\_payment | Chờ thanh toán | Đơn online chưa trả tiền | paid, cancelled |
| pending\_confirm | Chờ xác nhận | Đơn COD chờ CSKH xác nhận | confirmed, cancelled |
| preorder\_waiting | Chờ phát hành | Đơn đặt trước, sách chưa nhập kho | confirmed, cancelled |
| paid | Đã thanh toán | Đã nhận tiền | confirmed |
| confirmed | Chờ xử lý | Sẵn sàng đóng gói | packing, cancelled |
| packing | Đang đóng gói | Kho đã nhặt hàng, trừ tồn thật | ready\_to\_ship |
| ready\_to\_ship | Chờ lấy hàng | Đã tạo vận đơn | shipping |
| shipping | Đang giao | Đơn vị vận chuyển đã lấy | delivered, returning |
| delivered | Đã giao | Khách đã nhận | completed, return\_requested |
| completed | Hoàn tất | Hết thời hạn khiếu nại | — |
| return\_requested | Yêu cầu đổi trả | Khách gửi yêu cầu | returned, delivered (từ chối) |
| returning | Đang hoàn hàng | Giao thất bại, đang trả về kho | returned |
| returned | Đã hoàn hàng | Kho đã nhận lại | refunded |
| refunded | Đã hoàn tiền | Đã trả tiền cho khách | — |
| cancelled | Đã hủy | Khách/hệ thống/CSKH hủy | — |

### 16.2. Mã lỗi nghiệp vụ API

| Mã | HTTP | Thông báo cho khách |
| --- | --- | --- |
| OUT\_OF\_STOCK | 409 | Sách đã hết hàng hoặc không đủ số lượng |
| EDITION\_NOT\_LATEST | 200 (cảnh báo) | Đã có ấn bản mới hơn của sách này |
| COUPON\_INVALID | 422 | Mã giảm giá không hợp lệ |
| COUPON\_EXPIRED | 422 | Mã giảm giá đã hết hạn hoặc hết lượt |
| COUPON\_MIN\_ORDER | 422 | Đơn chưa đạt giá trị tối thiểu để dùng mã |
| MAX\_QTY\_EXCEEDED | 422 | Vượt số lượng tối đa, vui lòng gửi yêu cầu báo giá sỉ |
| ADDRESS\_NOT\_SUPPORTED | 422 | Chưa hỗ trợ giao đến địa chỉ này |
| PAYMENT\_FAILED | 402 | Thanh toán chưa thành công, vui lòng thử lại |
| ORDER\_NOT\_CANCELLABLE | 422 | Đơn đã được đóng gói, không thể hủy |
| RETURN\_WINDOW\_CLOSED | 422 | Đã quá thời hạn đổi trả |
| OTP\_INVALID | 400 | Mã OTP không đúng hoặc đã hết hạn |
| TOO\_MANY\_REQUESTS | 429 | Bạn thao tác quá nhanh, vui lòng thử lại sau |
| ISBN\_DUPLICATE | 409 | (Admin) ISBN đã tồn tại |

### 16.3. Checklist ra mắt

- [ ] Toàn bộ test case Must đạt; không còn lỗi Nghiêm trọng/Cao
- [ ] UAT ký duyệt bởi biên tập, kho, kế toán, CSKH
- [ ] Dữ liệu sách thật đã nhập: ảnh, mô tả, mục lục, đọc thử, ISBN, QĐXB, "cập nhật đến"
- [ ] Tồn kho đầu kỳ đã kiểm kê và nhập theo lô
- [ ] Cổng thanh toán chuyển từ sandbox sang production; đã test giao dịch thật số nhỏ và hoàn tiền
- [ ] Đơn vị vận chuyển chạy thật; in vận đơn thử
- [ ] Hóa đơn điện tử phát hành thử thành công
- [ ] Email có SPF/DKIM/DMARC; mẫu email/ZNS đã duyệt
- [ ] Thủ tục website TMĐT với Bộ Công Thương hoàn tất; logo ở footer
- [ ] Các trang chính sách đã được luật sư rà soát
- [ ] Banner cookie và checkbox đồng ý dữ liệu hoạt động đúng
- [ ] SEO: sitemap.xml, robots.txt, Search Console, GA4, schema Book/Product
- [ ] Chuyển hướng 301 từ link cũ (nếu có website cũ)
- [ ] Sao lưu tự động chạy; đã thử khôi phục
- [ ] Giám sát, cảnh báo đã cấu hình và gửi tới đúng người
- [ ] Tài khoản admin có 2FA; xóa tài khoản test
- [ ] Hướng dẫn sử dụng Admin đã bàn giao, nhân viên đã được đào tạo
- [ ] Kế hoạch trực sự cố 2 tuần đầu sau ra mắt

### 16.4. Câu hỏi mở cần chủ dự án quyết định

- Ngưỡng miễn phí vận chuyển và mức phí ship mặc định?
- Chính sách đổi trả có chấp nhận "đổi ý" khi chưa bóc màng co không?
- Chọn 1 hay 2 đơn vị vận chuyển; có cho khách chọn không?
- Thời điểm xuất hóa đơn điện tử: khi thanh toán hay khi giao thành công?
- Có làm chương trình tích điểm ngay ở MVP không?
- Tự xây dựng (custom) hay dùng nền tảng có sẵn (Haravan/Sapo/WooCommerce)?
- Tên miền chính thức?
