# Auth: tài khoản, đăng nhập, phân quyền

## Mục đích

Đăng ký/đăng nhập cho khách hàng (FR-01, FR-03), tài khoản nhân viên theo vai trò bảng 4.2 (FR-97),
bắt buộc xác thực 2 bước (2FA) cho mọi API Admin (NFR-15). Module `SachLuat.Modules.Auth`, schema `auth`.

## Luồng xử lý

- **Mật khẩu**: ASP.NET Core Identity, băm bằng **bcrypt** work factor 12 (NFR-11), tối thiểu 8 ký tự.
- **Đăng nhập theo app**: `POST /auth/login` nhận `portal` = `storefront` (mặc định) hoặc `admin`. Tài khoản có vai
  trò `super_admin` chỉ đăng nhập được với `portal = admin` (app Admin); ở Storefront trả `SUPER_ADMIN_PORTAL_FORBIDDEN`
  (403) sau khi kiểm tra đúng mật khẩu (không lộ email nào là Super Admin), không cấp cookie hay token 2FA. Các vai trò
  khác đăng nhập được ở cả hai app (Admin app tự từ chối tài khoản chỉ có `customer`).
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
| `users_admin` | super_admin | `/admin/users`, `/admin/customers` |
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
- **Danh sách nhân viên và khách hàng** (Super Admin): `/admin/users` (nhân viên) và `/admin/customers` (user có vai trò
  `customer`) tìm `q` trong email, họ tên, SĐT (ILIKE, escape `%`/`_`); nhân viên xếp theo id, khách mới đăng ký đứng
  đầu. Chi tiết khách `/admin/customers/{id}`: tài khoản, sổ địa chỉ, thông tin hóa đơn; id không phải khách hàng →
  `USER_NOT_FOUND`.
- **Super Admin quản lý tài khoản dưới cấp** (policy `users_admin`; đích không phải chính mình, không có vai trò
  `super_admin`, ngược lại → `CANNOT_MANAGE_USER` 422):
  - **Đổi vai trò** `PUT /admin/users/{id}/roles`: không đổi được vai trò của chính mình hay Super Admin khác (chỉ có
    1–2 tài khoản Super Admin), tránh tự gỡ hoặc tự cấp quyền.
  - **Không cấp vai trò `super_admin` qua API**: `POST /admin/users` và `PUT .../roles` có `super_admin` trong `roles` →
    `VALIDATION_ERROR` (400). Super Admin chỉ tạo từ cấu hình `Auth:BootstrapAdmin` hoặc thao tác trực tiếp trên CSDL,
    để một tài khoản Super Admin bị lộ không tự nhân thêm Super Admin khác.
  - **Đặt mật khẩu mới** `PUT /admin/users/{id}/password`: dùng khi nhân viên/khách quên mật khẩu. Đổi hash và
    security stamp trong một lần ghi, mở khóa nếu đang bị khóa do sai mật khẩu (`access_failed_count = 0`,
    `lockout_end = null`), thu hồi mọi phiên. Không gửi email, admin tự báo mật khẩu cho người dùng.
  - **Vô hiệu hóa / kích hoạt lại** `POST /admin/users/{id}/deactivate`, `/activate`: cờ `is_active` riêng, khác với
    khóa 15 phút do sai mật khẩu. Vô hiệu hóa thì thu hồi mọi phiên; sau đó `/auth/login` và `/auth/login/2fa`
    trả `ACCOUNT_DISABLED` (403), `/auth/refresh` trả `INVALID_REFRESH_TOKEN`, fast login vào tài khoản này trả
    `ACCOUNT_DISABLED`. Login kiểm tra cờ **sau** mật khẩu: sai mật khẩu vẫn chỉ báo `INVALID_CREDENTIALS`, nên người
    không biết mật khẩu không dò được tài khoản nào bị vô hiệu hóa.
  - Ghi log `Warning` kèm `ActorUserId`, `UserId` (không log mật khẩu).
- **Mỗi tài khoản một nơi đăng nhập + buộc đăng xuất tức thì**:
  - Cột `auth.users.session_version` (số nguyên) nằm trong access token (claim `sv`). Mỗi request, JWT bearer so `sv`
    với CSDL (`OnTokenValidated`); lệch hoặc thiếu claim → 401. Nhờ đó access token hết hiệu lực **ngay**, không chờ
    `Jwt:AccessTokenMinutes`. Token cấp trước bản này không có `sv` nên người dùng phải đăng nhập lại một lần.
  - Tăng phiên bản (nguyên tử, `UPDATE ... RETURNING`) và xóa mọi refresh token còn hiệu lực khi: đăng nhập mới
    (login/2FA) đá phiên cũ; Super Admin "Đăng xuất hộ"; đặt mật khẩu mới; vô hiệu hóa tài khoản. Refresh token bị
    **xóa** (không chỉ đánh dấu thu hồi) để thiết bị bị đá thử refresh không bị coi là token bị đánh cắp và làm sập
    phiên mới. Đăng ký và refresh không tăng phiên bản.
  - Fast login (impersonate) mang đúng `sv` hiện tại của tài khoản đích và **không** tăng nó: không đá chính chủ,
    nhưng chính chủ đăng nhập lại hoặc bị buộc đăng xuất thì phiên vào hộ cũng chấm dứt.
  - **Pusher**: sau khi kết thúc phiên, backend bắn sự kiện `session-ended` `{reason, version}` lên kênh riêng
    `private-user-{id}`; `reason` là `signed_in_elsewhere` (đăng nhập nơi khác; chỉ bắn khi có phiên bị đá) hoặc
    `forced_logout` (đăng xuất hộ / đặt mật khẩu mới có phiên / vô hiệu hóa; "đăng xuất hộ" luôn bắn vì thiết bị có thể
    chỉ còn access token). FE bỏ qua sự kiện có `version` ≤ `sessionVersion` của chính nó (thiết bị vừa đăng nhập),
    còn lại thì xóa phiên và hiện thông báo. Gửi lỗi/quá 3 giây chỉ log `Warning`, không làm hỏng request; thiết bị
    bỏ lỡ sự kiện vẫn nhận 401 ở lần gọi API kế tiếp. Chưa cấu hình Pusher thì dùng `NullSessionNotifier` (không bắn).
- **Cờ đường đăng nhập**: mọi access token có claim `login_via`:
  - `portal`: người dùng tự đăng nhập (login, 2FA, register, refresh). Token cũ không có claim này coi là `portal`.
  - `admin`: Super Admin fast login; kèm claim `impersonator_id` = id của admin.
  - `AuthResponse` trả thêm `loginVia`, `impersonatorId`, `sessionVersion`. Module khác đọc qua `ICurrentUser.ImpersonatorUserId`
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
| POST | `/auth/login` | Không | `{email, password, rememberMe, portal?}` → `{requiresTwoFactor, twoFactorToken?, session?}` |
| POST | `/auth/login/2fa` | Không | `{twoFactorToken, code}` → `AuthResponse` + cookie |
| POST | `/auth/refresh` | Cookie | → `AuthResponse` + cookie mới |
| POST | `/auth/logout` | Cookie | → 204, xóa cookie |
| POST | `/auth/password/change` | Bearer | `{currentPassword, newPassword}` → 204, thu hồi mọi phiên |
| POST | `/auth/2fa/setup` | Bearer | → `{sharedKey, authenticatorUri}` |
| POST | `/auth/2fa/enable` | Bearer | `{code}` → `AuthResponse` (amr=mfa) |
| GET/PATCH | `/me` | Bearer | Hồ sơ (FR-05) → `MeResponse`; PATCH nhận `{fullName, phoneNumber?, birthDate?, occupation?}` và ghi đè cả 4 field |
| PUT | `/me/preferences` | Bearer | `{theme: "dark" \| "light"}` → `{theme}`: lưu giao diện storefront khách chọn |
| POST | `/me/avatar/upload-signature` | Bearer | → `ReplaceUploadSignatureResponse`: chữ ký upload avatar lên Cloudinary (xem mục Ảnh đại diện) |
| PUT | `/me/avatar` | Bearer | `{avatarUrl}` → `{avatarUrl}`: lưu `secure_url` vừa upload |
| DELETE | `/me/avatar` | Bearer | → 204, bỏ avatar (`avatarUrl` về `null`) |
| GET | `/me/addresses` | Bearer | Sổ địa chỉ (FR-06) → `AddressResponse[]`, mặc định đứng đầu |
| POST | `/me/addresses` | Bearer | `UpsertAddressRequest` → 201 `AddressResponse[]` (cả sổ) |
| PUT | `/me/addresses/{id}` | Bearer | `UpsertAddressRequest` → `AddressResponse[]` |
| POST | `/me/addresses/{id}/default` | Bearer | Đặt làm mặc định → `AddressResponse[]` |
| DELETE | `/me/addresses/{id}` | Bearer | → 204 |
| PUT | `/me/invoice-info` | Bearer | Lưu thông tin hóa đơn (FR-07) `{companyName, taxCode, address, email}` → `InvoiceInfoResponse` |
| DELETE | `/me/invoice-info` | Bearer | → 204 |
| GET | `/admin/users` | users_admin | `?q=&page=&limit=` → `PagedResponse<UserResponse>` (nhân viên) |
| POST | `/admin/users` | users_admin | `{email, fullName, password, roles[]}` → 201 `UserResponse` |
| PUT | `/admin/users/{id}/roles` | users_admin | `{roles[]}` → `UserResponse` |
| GET | `/admin/customers` | users_admin | `?q=&page=&limit=` → `PagedResponse<UserResponse>` (khách hàng) |
| GET | `/admin/customers/{id}` | users_admin | → `AdminCustomerResponse` |
| POST | `/admin/users/{id}/logout` | users_admin | Đăng xuất hộ (force logout): kết thúc mọi phiên của user **ngay lập tức** và báo qua Pusher → `{revokedSessions}` (số refresh token; có thể 0 mà access token vẫn bị cắt) |
| POST | `/realtime/auth` | Bearer | Ký quyền nghe kênh Pusher riêng: body `{socketId, channelName}`; chỉ `private-user-{id của mình}` (khác → 403); chưa cấu hình Pusher → 503 `REALTIME_UNAVAILABLE`. Trả JSON `{auth}` cho pusher-js |
| PUT | `/admin/users/{id}/password` | users_admin | `{newPassword}` (8–128 ký tự) → 204; mở khóa, thu hồi mọi phiên |
| POST | `/admin/users/{id}/deactivate` | users_admin | Vô hiệu hóa, thu hồi mọi phiên → `UserResponse` (`isActive: false`) |
| POST | `/admin/users/{id}/activate` | users_admin | Kích hoạt lại → `UserResponse` (`isActive: true`) |
| POST | `/admin/users/{id}/impersonate` | users_admin | → `AuthResponse` của tài khoản đích (`loginVia: "admin"`), không cookie |

`AuthResponse`: `{accessToken, tokenType: "Bearer", expiresIn, loginVia: "portal" | "admin", impersonatorId?,
user: {id, email, fullName, roles[], twoFactorEnabled, requiresTwoFactorSetup, isActive, theme, avatarUrl, ...}}`.

**Ảnh đại diện** (`avatarUrl`): link ảnh hoặc `null` (chưa có, FE hiện ảnh mặc định). Có trong `UserResponse` (login,
refresh, 2FA, register, fast login, danh sách/chi tiết nhân viên và khách ở Admin) và `MeResponse`. File không đi qua API:

1. `POST /me/avatar/upload-signature` → `{uploadUrl, apiKey, timestamp, publicId, overwrite: true, allowedFormats,
   signature}`. Chữ ký hiệu lực 1 giờ.
2. FE gửi `multipart/form-data` tới `uploadUrl`: `file`, `api_key`, `timestamp`, `public_id`, `overwrite` (chuỗi
   `true`), `allowed_formats`, `signature`. **Không** gửi `folder` (thư mục đã nằm trong `public_id`).
3. `PUT /me/avatar {avatarUrl: secure_url}`.

`public_id` = `{Cloudinary:AvatarFolder}/{userId}-{16 ký tự hex HMAC-SHA256(ApiSecret, userId)}`, cố định theo user:
upload mới ghi đè ảnh cũ (mỗi user chiếm đúng một ảnh trên Cloudinary), không đoán được link avatar của người khác, và
chữ ký của user này không ghi được vào ảnh người khác. Khi đã cấu hình Cloudinary, `PUT` chỉ nhận đúng
`https://res.cloudinary.com/{CloudName}/image/upload/[v123/]{publicId}.{jpg|jpeg|png|webp}` của chính user, sai thì 422
`AVATAR_URL_INVALID`. Chưa cấu hình (local/test) thì nhận mọi link `https`, còn bước 1 trả 503 `UPLOAD_NOT_CONFIGURED`.
Link không phải `https` hoặc dài quá 500 ký tự → 400 `VALIDATION_ERROR`. `PUT`/`DELETE` chỉ cập nhật cột `avatar_url`.

**Giao diện storefront** (`theme`): `"dark"`, `"light"` hoặc `null` (chưa chọn, FE dùng mặc định tối). Có trong `UserResponse`
(login, refresh, 2FA, register, fast login) và `MeResponse`, nên FE biết ngay khi vào phiên. Lưu bằng
`PUT /me/preferences` (thiếu hoặc giá trị lạ → `VALIDATION_ERROR` 400), chỉ cập nhật cột `theme`. `null` khác `"dark"`:
FE dựa vào đó để đẩy lựa chọn đã có ở trình duyệt lên tài khoản lần đầu (xem `docs/features/storefront-theme.md` ở repo FE).

`AdminCustomerResponse`: `{account: UserResponse, addresses: AddressResponse[], invoiceInfo: InvoiceInfoResponse |
null}`.

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
`CANNOT_IMPERSONATE` (422), `CANNOT_MANAGE_USER` (422), `ACCOUNT_DISABLED` (403),
`USER_NOT_FOUND` (404), `TOO_MANY_REQUESTS` (429),
`ADDRESS_NOT_FOUND` (404, kể cả địa chỉ của người khác), `ADDRESS_LIMIT_REACHED` (422),
`SUPER_ADMIN_PORTAL_FORBIDDEN` (403), `REALTIME_UNAVAILABLE` (503), `AVATAR_URL_INVALID` (422),
`UPLOAD_NOT_CONFIGURED` (503).

## Cấu hình cần thiết

| Key | Ghi chú |
| --- | --- |
| `Jwt:SigningKey` | **Bí mật**, ≥ 32 ký tự, user-secrets / secret manager |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` | `appsettings.json` |
| `Auth:RefreshTokenDays`, `Auth:SessionRefreshTokenHours`, `Auth:TotpIssuer` | Mặc định 30 ngày / 12 giờ |
| `Auth:BootstrapAdmin:Email`, `Auth:BootstrapAdmin:Password` | **Bí mật**, chỉ cần lần đầu |
| `RateLimiting:AuthPermitPerMinute` | Mặc định 10 |
| `Pusher:AppId`, `Pusher:Key`, `Pusher:Secret`, `Pusher:Cluster` | Tùy chọn; thiếu bất kỳ khóa nào thì tắt realtime. `Secret` là **bí mật** (Render: `Pusher__Secret`). FE cần `NEXT_PUBLIC_PUSHER_KEY` và `NEXT_PUBLIC_PUSHER_CLUSTER` |
| `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret` | Dùng chung với ảnh bìa/popup; `ApiSecret` là **bí mật**. Đổi `ApiSecret` thì `publicId` avatar đổi theo: avatar cũ vẫn hiển thị, lần upload sau sang ảnh mới |
| `Cloudinary:AvatarFolder` | Mặc định `sachluat/avatars` |
| `TrustedProxy:Secret` | **Bí mật**, trống hoặc ≥ 32 ký tự, phải trùng `PROXY_SECRET` bên FE. Render: `TrustedProxy__Secret` |

## Thay đổi CSDL (migration liên quan)

`CreateAuthSchema` (schema `auth`): bảng Identity đổi tên `users`, `roles`, `user_roles`, `user_claims`,
`user_logins`, `user_tokens` (lưu khóa TOTP), `role_claims`; bảng `refresh_tokens`. Seed 8 vai trò của bảng 4.2.

`AddUserTheme`: thêm cột `auth.users.theme` (varchar(30), cho phép null, lưu `dark`/`light`). Chỉ thêm cột, dữ liệu cũ
giữ nguyên (null). **Phải chạy migration lên CSDL production trước khi deploy bản này**, backend không tự migrate.

`AddUserAvatarUrl`: cột `auth.users.avatar_url` (varchar(500), cho phép null). Chỉ thêm cột, dữ liệu cũ giữ nguyên
(null). **Phải chạy migration lên CSDL production trước khi deploy bản này** (thiếu cột thì đọc user bị lỗi).

`AddUserSessionVersion`: cột `auth.users.session_version` (integer, not null, mặc định 0). Chỉ thêm cột.
**Phải chạy migration lên CSDL production trước khi deploy bản này** (thiếu cột thì mọi request có token đều lỗi).

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
- Mỗi request có token tốn thêm một truy vấn theo khóa chính (`session_version`); chưa cache.
- Hai tab cùng refresh một lúc có thể bị coi là dùng lại token và bị đăng xuất.
- Avatar: bỏ avatar không xóa ảnh trên Cloudinary (ảnh nằm chờ lần upload sau ghi đè); chưa giới hạn dung lượng file
  ở phía Cloudinary (theo gói tài khoản) và chưa resize khi upload, FE nên resize khi hiển thị (`c_fill,w_..,h_..`).
  Admin chưa đổi/xóa avatar hộ khách được.
- Chưa có nhật ký thao tác (FR-98) cho việc đổi quyền, fast login, đăng xuất hộ, đặt mật khẩu mới và vô hiệu hóa
  tài khoản (hiện chỉ ghi log).
