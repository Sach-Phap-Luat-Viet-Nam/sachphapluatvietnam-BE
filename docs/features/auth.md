# Auth: tài khoản, đăng nhập, phân quyền

## Mục đích

Đăng ký/đăng nhập cho khách hàng (FR-01, FR-03), tài khoản nhân viên theo vai trò bảng 4.2 (FR-97),
bắt buộc xác thực 2 bước (2FA) cho mọi API Admin (NFR-15). Module `SachLuat.Modules.Auth`, schema `auth`.

## Luồng xử lý

- **Mật khẩu**: ASP.NET Core Identity, băm bằng **bcrypt** work factor 12 (NFR-11), tối thiểu 8 ký tự.
- **Khóa tài khoản** (FR-03): sai mật khẩu hoặc mã 2FA 5 lần liên tiếp → khóa 15 phút (`ACCOUNT_LOCKED`, 423).
- **Phiên đăng nhập** (mục 11):
  - Access token JWT 15 phút, gửi trong header `Authorization: Bearer ...`.
  - Refresh token ngẫu nhiên 256 bit trong cookie `sl_refresh` (HttpOnly, Secure, SameSite=Strict,
    path `/api/v1/auth`); DB chỉ lưu bản băm SHA-256.
  - `rememberMe = true`: cookie 30 ngày; ngược lại là cookie phiên, token hết hạn sau 12 giờ.
  - Mỗi lần `/auth/refresh` thu hồi token cũ, cấp token mới. Dùng lại token đã thu hồi → coi là bị đánh cắp,
    thu hồi mọi phiên của user.
- **2FA cho nhân viên** (TOTP, Google Authenticator...):
  1. Nhân viên đăng nhập bằng mật khẩu → token có `amr=pwd`, `user.requiresTwoFactorSetup = true`.
     Gọi API Admin lúc này bị 403.
  2. `POST /auth/2fa/setup` → `sharedKey`, `authenticatorUri` (hiển thị QR từ URI).
  3. `POST /auth/2fa/enable` với mã 6 số → token mới có `amr=mfa`, các phiên cũ bị thu hồi.
  4. Các lần sau: `/auth/login` trả `requiresTwoFactor = true` + `twoFactorToken` (hạn 5 phút) →
     `POST /auth/login/2fa` với mã 6 số.
- **Phân quyền**: claim `role` theo bảng 4.2. Policy Admin = đúng vai trò + `amr=mfa`:

| Policy | Vai trò | Dùng cho |
| --- | --- | --- |
| `catalog_admin` | super_admin, product_manager | `/admin/books`, `/admin/editions`, `/admin/skus`, `/admin/categories`, `/admin/authors`, `/admin/legal-documents` |
| `inventory_admin` | super_admin, warehouse_keeper | `/admin/inventory/...` |
| `inventory_approve` | super_admin | Duyệt/từ chối phiếu nhập |
| `orders_read` | super_admin, order_manager, customer_service, accountant | Xem đơn Admin |
| `orders_manage` | super_admin, order_manager | Đổi trạng thái đơn |
| `users_admin` | super_admin | `/admin/users` |
| `content_admin` | super_admin, marketing | `/admin/popups`, `/admin/uploads/popup-signature` |

- **Super Admin đầu tiên**: tạo lúc khởi động từ cấu hình `Auth:BootstrapAdmin` nếu email chưa tồn tại.
- **Fast login (Super Admin vào hộ tài khoản)**:
  - `POST /admin/users/{id}/impersonate` (policy `users_admin`) trả `AuthResponse` của tài khoản đích, dùng để
    hỗ trợ, kiểm tra lỗi giúp nhân viên/khách hàng mà không cần mật khẩu của họ.
  - Chỉ vào hộ tài khoản **dưới cấp**: mọi tài khoản không có vai trò `super_admin`, không phải chính mình.
    Ngược lại → `CANNOT_IMPERSONATE` (422).
  - Chỉ cấp **access token 15 phút**, không có refresh token, không ghi cookie, nên phiên của chính admin
    (cookie `sl_refresh`) giữ nguyên. Hết hạn thì gọi lại API.
  - Token mang `amr=mfa` (admin đã qua 2FA) và vai trò của tài khoản đích. Không chặn thao tác nào
    trong phiên vào hộ.
  - Mỗi lần fast login ghi log `Warning` kèm `ActorUserId`, `TargetUserId`.
- **Super Admin quản lý tài khoản dưới cấp** (policy `users_admin`; đích không phải chính mình, không có vai trò
  `super_admin`, ngược lại → `CANNOT_MANAGE_USER` 422):
  - **Đặt mật khẩu mới** `PUT /admin/users/{id}/password`: dùng khi nhân viên/khách quên mật khẩu. Đổi hash và
    security stamp trong một lần ghi, mở khóa nếu đang bị khóa do sai mật khẩu (`access_failed_count = 0`,
    `lockout_end = null`), thu hồi mọi phiên. Không gửi email, admin tự báo mật khẩu cho người dùng.
  - **Vô hiệu hóa / kích hoạt lại** `POST /admin/users/{id}/deactivate`, `/activate`: cờ `is_active` riêng, khác với
    khóa 15 phút do sai mật khẩu. Vô hiệu hóa thì thu hồi mọi phiên; sau đó `/auth/login` và `/auth/login/2fa`
    trả `ACCOUNT_DISABLED` (403), `/auth/refresh` trả `INVALID_REFRESH_TOKEN`, fast login vào tài khoản này trả
    `ACCOUNT_DISABLED`. Login kiểm tra cờ **sau** mật khẩu: sai mật khẩu vẫn chỉ báo `INVALID_CREDENTIALS`, nên người
    không biết mật khẩu không dò được tài khoản nào bị vô hiệu hóa.
  - Ghi log `Warning` kèm `ActorUserId`, `UserId` (không log mật khẩu).
- **Cờ đường đăng nhập**: mọi access token có claim `login_via`:
  - `portal`: người dùng tự đăng nhập (login, 2FA, register, refresh). Token cũ không có claim này coi là `portal`.
  - `admin`: Super Admin fast login; kèm claim `impersonator_id` = id của admin.
  - `AuthResponse` trả thêm `loginVia`, `impersonatorId`. Module khác đọc qua `ICurrentUser.ImpersonatorUserId`
    / `ICurrentUser.IsImpersonated`.
- **Rate limit** (NFR-13): nhóm `/auth/*` và `/orders/lookup` tối đa 10 request/phút/IP (cấu hình được), vượt → 429.
  - IP thật của khách: FE (Next.js trên Vercel) proxy `/api/v1/*` sang backend, gửi kèm `X-Client-Ip` và
    `X-Proxy-Secret`. `TrustedProxyMiddleware` chỉ tin `X-Client-Ip` khi secret khớp `TrustedProxy:Secret`,
    rồi xóa header secret. Không dùng `X-Forwarded-For` vì Render nối thêm giá trị client gửi nên giả được IP.
  - Chưa cấu hình secret, hoặc secret sai: dùng IP kết nối (sau Render là IP proxy, mọi khách chung quota).

## API (method, route, request, response, mã lỗi)

Tiền tố `/api/v1`.

| Method | Route | Xác thực | Request → Response |
| --- | --- | --- | --- |
| POST | `/auth/register` | Không | `{email, password, fullName, phoneNumber?}` → 201 `AuthResponse` + cookie |
| POST | `/auth/login` | Không | `{email, password, rememberMe}` → `{requiresTwoFactor, twoFactorToken?, session?}` |
| POST | `/auth/login/2fa` | Không | `{twoFactorToken, code}` → `AuthResponse` + cookie |
| POST | `/auth/refresh` | Cookie | → `AuthResponse` + cookie mới |
| POST | `/auth/logout` | Cookie | → 204, xóa cookie |
| POST | `/auth/password/change` | Bearer | `{currentPassword, newPassword}` → 204, thu hồi mọi phiên |
| POST | `/auth/2fa/setup` | Bearer | → `{sharedKey, authenticatorUri}` |
| POST | `/auth/2fa/enable` | Bearer | `{code}` → `AuthResponse` (amr=mfa) |
| GET/PATCH | `/me` | Bearer | Hồ sơ (FR-05) → `MeResponse`; PATCH nhận `{fullName, phoneNumber?, birthDate?, occupation?}` và ghi đè cả 4 field |
| GET | `/me/addresses` | Bearer | Sổ địa chỉ (FR-06) → `AddressResponse[]`, mặc định đứng đầu |
| POST | `/me/addresses` | Bearer | `UpsertAddressRequest` → 201 `AddressResponse[]` (cả sổ) |
| PUT | `/me/addresses/{id}` | Bearer | `UpsertAddressRequest` → `AddressResponse[]` |
| POST | `/me/addresses/{id}/default` | Bearer | Đặt làm mặc định → `AddressResponse[]` |
| DELETE | `/me/addresses/{id}` | Bearer | → 204 |
| PUT | `/me/invoice-info` | Bearer | Lưu thông tin hóa đơn (FR-07) `{companyName, taxCode, address, email}` → `InvoiceInfoResponse` |
| DELETE | `/me/invoice-info` | Bearer | → 204 |
| GET | `/admin/users` | users_admin | Danh sách nhân viên (phân trang) |
| POST | `/admin/users` | users_admin | `{email, fullName, password, roles[]}` → 201 |
| PUT | `/admin/users/{id}/roles` | users_admin | `{roles[]}` |
| POST | `/admin/users/{id}/logout` | users_admin | Đăng xuất hộ: thu hồi mọi phiên của user trên mọi thiết bị → `{revokedSessions}` |
| PUT | `/admin/users/{id}/password` | users_admin | `{newPassword}` (8–128 ký tự) → 204; mở khóa, thu hồi mọi phiên |
| POST | `/admin/users/{id}/deactivate` | users_admin | Vô hiệu hóa, thu hồi mọi phiên → `UserResponse` (`isActive: false`) |
| POST | `/admin/users/{id}/activate` | users_admin | Kích hoạt lại → `UserResponse` (`isActive: true`) |
| POST | `/admin/users/{id}/impersonate` | users_admin | → `AuthResponse` của tài khoản đích (`loginVia: "admin"`), không cookie |

`AuthResponse`: `{accessToken, tokenType: "Bearer", expiresIn, loginVia: "portal" | "admin", impersonatorId?,
user: {id, email, fullName, roles[], twoFactorEnabled, requiresTwoFactorSetup, isActive, ...}}`.

**Hồ sơ để điền sẵn form đặt hàng** (`MeResponse`): các field của `user` ở trên, thêm `defaultAddress` và
`invoiceInfo` (`null` khi chưa có):

```json
{
  "id": 42,
  "email": "khach@example.com",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0901234567",
  "birthDate": null,
  "occupation": "lawyer",
  "roles": ["customer"],
  "twoFactorEnabled": false,
  "requiresTwoFactorSetup": false,
  "defaultAddress": {
    "id": 7,
    "recipientName": "Nguyễn Văn A",
    "phone": "0901234567",
    "province": "Hà Nội",
    "ward": "Phường Cửa Nam",
    "streetAddress": "12 Hàng Bài",
    "isDefault": true,
    "updatedAt": "2026-10-24T05:30:00Z"
  },
  "invoiceInfo": {
    "companyName": "Công ty TNHH Luật ABC",
    "taxCode": "0101234567",
    "address": "1 Tràng Tiền, Hà Nội",
    "email": "ketoan@abc.vn",
    "updatedAt": "2026-10-24T05:30:00Z"
  }
}
```

Điền vào `POST /orders`: `customerName` ← `defaultAddress.recipientName` (không có địa chỉ thì `fullName`),
`phone` ← `defaultAddress.phone` (hoặc `phoneNumber`), `email` ← `email`,
`shippingAddress` ← `{province, ward, streetAddress}`, `invoiceInfo` ← `invoiceInfo`.
Checkbox "Lưu địa chỉ này" trên form đặt hàng: FE tự gọi `POST /me/addresses` / `PUT /me/invoice-info`;
`POST /orders` không tự lưu vào hồ sơ.

`UpsertAddressRequest`: `{recipientName, phone, province, ward, streetAddress, isDefault}`.

**Quy tắc sổ địa chỉ** (`AddressBook`):

- Tối đa 10 địa chỉ (`ADDRESS_LIMIT_REACHED`). Sổ không rỗng thì luôn đúng một địa chỉ mặc định.
- Địa chỉ đầu tiên tự là mặc định. `isDefault: true` khi thêm/sửa thì chuyển mặc định sang địa chỉ đó;
  `isDefault: false` không bỏ được cờ mặc định (muốn đổi thì đặt địa chỉ khác làm mặc định).
- Xóa địa chỉ mặc định thì địa chỉ cập nhật gần nhất còn lại thành mặc định.
- Mọi thao tác sửa sổ địa chỉ/hóa đơn khóa dòng `auth.users` (`FOR UPDATE`) để bấm nhiều lần không vượt giới hạn
  hay sinh hai địa chỉ mặc định.
- Validate giống form đặt hàng (`CreateOrderRequestValidator`): SĐT `0xxxxxxxxx`/`+84xxxxxxxxx`, tỉnh/phường ≤ 100,
  số nhà ≤ 500, mã số thuế 10 số hoặc 10 số + `-xxx`, email ≤ 256.

Mã lỗi: `INVALID_CREDENTIALS` (401), `ACCOUNT_LOCKED` (423), `EMAIL_TAKEN` (409), `INVALID_REFRESH_TOKEN` (401),
`TWO_FACTOR_TOKEN_INVALID` (401), `OTP_INVALID` (400), `TWO_FACTOR_ALREADY_ENABLED` (409),
`CANNOT_REMOVE_OWN_ADMIN` (422), `CANNOT_IMPERSONATE` (422), `CANNOT_MANAGE_USER` (422), `ACCOUNT_DISABLED` (403),
`USER_NOT_FOUND` (404), `TOO_MANY_REQUESTS` (429),
`ADDRESS_NOT_FOUND` (404, kể cả địa chỉ của người khác), `ADDRESS_LIMIT_REACHED` (422).

## Cấu hình cần thiết

| Key | Ghi chú |
| --- | --- |
| `Jwt:SigningKey` | **Bí mật**, ≥ 32 ký tự, user-secrets / secret manager |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` | `appsettings.json` |
| `Auth:RefreshTokenDays`, `Auth:SessionRefreshTokenHours`, `Auth:TotpIssuer` | Mặc định 30 ngày / 12 giờ |
| `Auth:BootstrapAdmin:Email`, `Auth:BootstrapAdmin:Password` | **Bí mật**, chỉ cần lần đầu |
| `RateLimiting:AuthPermitPerMinute` | Mặc định 10 |
| `TrustedProxy:Secret` | **Bí mật**, trống hoặc ≥ 32 ký tự, phải trùng `PROXY_SECRET` bên FE. Render: `TrustedProxy__Secret` |

## Thay đổi CSDL (migration liên quan)

`CreateAuthSchema` (schema `auth`): bảng Identity đổi tên `users`, `roles`, `user_roles`, `user_claims`,
`user_logins`, `user_tokens` (lưu khóa TOTP), `role_claims`; bảng `refresh_tokens`. Seed 8 vai trò của bảng 4.2.

`AddUserAddressesAndInvoiceInfo`: bảng `user_addresses` (`recipient_name, phone, province, ward, street_address,
is_default`, index `user_id`) và `user_invoice_infos` (khóa chính `user_id`), cả hai cascade khi xóa user.
Chỉ thêm bảng, không đổi dữ liệu cũ.

`AddUserIsActive`: cột `users.is_active` (boolean, not null, mặc định `true` cho tài khoản có sẵn).

## Hạn chế / việc còn lại

- Chưa xác thực email/OTP SMS–Zalo, quên mật khẩu (FR-01, FR-04): chờ module Notification.
- Chưa có đăng nhập mạng xã hội (FR-02), tài khoản tổ chức (FR-09), xóa tài khoản (FR-08).
- Tỉnh/thành, phường/xã nhập dạng chữ tự do, chưa có danh mục hành chính để chọn (FR-06); đơn hàng cũng vậy.
- Mỗi user chỉ lưu một bộ thông tin hóa đơn.
- Chưa có mã khôi phục 2FA; mã TOTP vừa dùng chưa bị chặn dùng lại trong cùng 30 giây.
- Hai tab cùng refresh một lúc có thể bị coi là dùng lại token và bị đăng xuất.
- Chưa có nhật ký thao tác (FR-98) cho việc đổi quyền, fast login, đăng xuất hộ, đặt mật khẩu mới và vô hiệu hóa
  tài khoản (hiện chỉ ghi log).
- Đăng xuất hộ, đặt mật khẩu mới và vô hiệu hóa chỉ thu hồi refresh token: access token user đang cầm vẫn dùng được
  tới khi hết hạn (tối đa 15 phút). Muốn chặn ngay thì cần kiểm tra security stamp hoặc danh sách token bị chặn ở mỗi request (chưa làm).
- Fast login không chặn thao tác nào (đổi mật khẩu, bật 2FA... thay người dùng); access token vào hộ không thu hồi
  được trước hạn 15 phút.
- `GET /admin/users` mới chỉ liệt kê nhân viên; muốn fast login khách hàng thì Admin cần biết id khách.
- Request gọi thẳng vào Render (không qua FE) bị tính theo IP proxy của Render, dùng chung một quota.
