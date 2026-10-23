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

- **Super Admin đầu tiên**: tạo lúc khởi động từ cấu hình `Auth:BootstrapAdmin` nếu email chưa tồn tại.
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
| GET/PATCH | `/me` | Bearer | Hồ sơ: `fullName, phoneNumber, birthDate, occupation` (FR-05) |
| GET | `/admin/users` | users_admin | Danh sách nhân viên (phân trang) |
| POST | `/admin/users` | users_admin | `{email, fullName, password, roles[]}` → 201 |
| PUT | `/admin/users/{id}/roles` | users_admin | `{roles[]}` |

`AuthResponse`: `{accessToken, tokenType: "Bearer", expiresIn, user: {id, email, fullName, roles[], twoFactorEnabled, requiresTwoFactorSetup, ...}}`.

Mã lỗi: `INVALID_CREDENTIALS` (401), `ACCOUNT_LOCKED` (423), `EMAIL_TAKEN` (409), `INVALID_REFRESH_TOKEN` (401),
`TWO_FACTOR_TOKEN_INVALID` (401), `OTP_INVALID` (400), `TWO_FACTOR_ALREADY_ENABLED` (409),
`CANNOT_REMOVE_OWN_ADMIN` (422), `USER_NOT_FOUND` (404), `TOO_MANY_REQUESTS` (429).

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

## Hạn chế / việc còn lại

- Chưa xác thực email/OTP SMS–Zalo, quên mật khẩu (FR-01, FR-04): chờ module Notification.
- Chưa có đăng nhập mạng xã hội (FR-02), tài khoản tổ chức (FR-09), sổ địa chỉ (FR-06), xóa tài khoản (FR-08).
- Chưa có mã khôi phục 2FA; mã TOTP vừa dùng chưa bị chặn dùng lại trong cùng 30 giây.
- Hai tab cùng refresh một lúc có thể bị coi là dùng lại token và bị đăng xuất.
- Chưa có nhật ký thao tác (FR-98) cho việc đổi quyền.
- Request gọi thẳng vào Render (không qua FE) bị tính theo IP proxy của Render, dùng chung một quota.
