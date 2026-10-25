using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

internal sealed class AuthService(
    UserManager<AppUser> userManager,
    AuthDbContext dbContext,
    TokenService tokenService,
    ISessionNotifier sessionNotifier,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    // So mật khẩu với hash giả khi email không tồn tại, để thời gian phản hồi không lộ email nào đã đăng ký.
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), BCryptPasswordHasher.WorkFactor));

    private readonly AuthOptions _options = authOptions.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw AuthErrors.EmailTaken();
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
        };

        EnsureSucceeded(await userManager.CreateAsync(user, request.Password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.Customer));
        logger.LogInformation("Registered customer {UserId}", user.Id);

        return await IssueSessionAsync(user, isPersistent: false, isMultiFactor: false, cancellationToken);
    }

    /// <param name="trustedDeviceToken">Giá trị cookie <c>sl_2fa_trust</c> của thiết bị, null khi không có.</param>
    public async Task<(LoginResponse Response, IssuedRefreshToken? RefreshToken)> LoginAsync(
        LoginRequest request,
        string? trustedDeviceToken,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _ = BCrypt.Net.BCrypt.Verify(request.Password, DummyPasswordHash.Value);
            throw AuthErrors.InvalidCredentials();
        }

        await EnsureNotLockedOutAsync(user);
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await RegisterFailedAttemptAsync(user);
            throw AuthErrors.InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        // Kiểm tra sau mật khẩu để người không biết mật khẩu không dò được tài khoản nào bị vô hiệu hóa.
        EnsureActive(user);

        // Kiểm tra sau mật khẩu để không lộ email nào là Super Admin; chưa cấp phiên, cookie hay token 2FA nào.
        if (request.Portal != LoginPortal.Admin && await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            logger.LogWarning("Super admin {UserId} attempted to log in via {Portal}", user.Id, request.Portal);
            throw AuthErrors.SuperAdminPortalForbidden();
        }

        var skipTwoFactor = user.TwoFactorEnabled
            && await IsTrustedDeviceAsync(user, trustedDeviceToken, cancellationToken);
        if (user.TwoFactorEnabled && !skipTwoFactor)
        {
            var ticket = tokenService.CreateTwoFactorTicket(user.Id, request.RememberMe);
            return (new LoginResponse(RequiresTwoFactor: true, ticket, Session: null), null);
        }

        await EndOtherSessionsAsync(user.Id, cancellationToken);
        var session = await IssueSessionAsync(user, request.RememberMe, isMultiFactor: skipTwoFactor, cancellationToken);
        return (new LoginResponse(RequiresTwoFactor: false, TwoFactorToken: null, session.Response), session.RefreshToken);
    }

    public async Task<TwoFactorLoginResult> LoginTwoFactorAsync(
        TwoFactorLoginRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await tokenService.ValidateTwoFactorTicketAsync(request.TwoFactorToken)
            ?? throw AuthErrors.TwoFactorTokenInvalid();
        var user = await FindUserAsync(ticket.UserId) ?? throw AuthErrors.TwoFactorTokenInvalid();

        await EnsureNotLockedOutAsync(user);
        await VerifyAuthenticatorCodeAsync(user, request.Code);
        EnsureActive(user);

        await EndOtherSessionsAsync(user.Id, cancellationToken);
        var session = await IssueSessionAsync(user, ticket.RememberMe, isMultiFactor: true, cancellationToken);
        var trustedDevice = request.TrustDevice && await CanTrustDeviceAsync(user)
            ? await IssueTrustedDeviceAsync(user.Id, cancellationToken)
            : null;
        return new TwoFactorLoginResult(session, trustedDevice);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw AuthErrors.InvalidRefreshToken();

        if (stored.RevokedAt is not null)
        {
            // Token đã bị thay thế mà vẫn được dùng lại: có thể bị đánh cắp, thu hồi mọi phiên của user.
            logger.LogWarning("Revoked refresh token reused for user {UserId}; revoking all sessions", stored.UserId);
            await RevokeAllAsync(stored.UserId, cancellationToken);
            throw AuthErrors.InvalidRefreshToken();
        }

        var user = await FindUserAsync(stored.UserId);
        if (!stored.IsActive(now) || user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        stored.Revoke(now);
        return await IssueSessionAsync(user, stored.IsPersistent, stored.IsMultiFactor, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId) ?? throw AuthErrors.UserNotFound();
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded && result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            throw AuthErrors.InvalidCredentials();
        }

        EnsureSucceeded(result);
        await RevokeAllAsync(userId, cancellationToken);
        await RevokeTrustedDevicesAsync(userId, cancellationToken);
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(long userId)
    {
        var user = await FindUserAsync(userId) ?? throw AuthErrors.UserNotFound();
        if (user.TwoFactorEnabled)
        {
            throw AuthErrors.TwoFactorAlreadyEnabled();
        }

        EnsureSucceeded(await userManager.ResetAuthenticatorKeyAsync(user));
        var key = await userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("Authenticator key was not generated.");

        var issuer = Uri.EscapeDataString(_options.TotpIssuer);
        var account = Uri.EscapeDataString(user.Email ?? user.Id.ToString(CultureInfo.InvariantCulture));
        var uri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6";
        return new TwoFactorSetupResponse(key, uri);
    }

    /// <summary>Bật 2FA khi mã đúng; thu hồi các phiên cũ và cấp phiên mới đã qua 2FA.</summary>
    public async Task<AuthResult> EnableTwoFactorAsync(long userId, string code, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId) ?? throw AuthErrors.UserNotFound();
        if (user.TwoFactorEnabled)
        {
            throw AuthErrors.TwoFactorAlreadyEnabled();
        }

        await VerifyAuthenticatorCodeAsync(user, code);
        EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, true));
        logger.LogInformation("Two-factor authentication enabled for user {UserId}", user.Id);

        await RevokeAllAsync(userId, cancellationToken);
        return await IssueSessionAsync(user, isPersistent: false, isMultiFactor: true, cancellationToken);
    }

    public async Task<UserResponse> GetUserAsync(long userId)
    {
        var user = await FindUserAsync(userId) ?? throw AuthErrors.UserNotFound();
        return await ToResponseAsync(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request)
    {
        var user = await FindUserAsync(userId) ?? throw AuthErrors.UserNotFound();
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber;
        user.BirthDate = request.BirthDate;
        user.Occupation = request.Occupation;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        return await ToResponseAsync(user);
    }

    internal async Task<UserResponse> ToResponseAsync(AppUser user) =>
        ToResponse(user, await userManager.GetRolesAsync(user));

    internal static UserResponse ToResponse(AppUser user, IEnumerable<string> userRoles)
    {
        var roles = userRoles.Order(StringComparer.Ordinal).ToList();
        var isStaff = roles.Any(Roles.Staff.Contains);
        return new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.PhoneNumber,
            user.BirthDate,
            user.Occupation,
            roles,
            user.TwoFactorEnabled,
            RequiresTwoFactorSetup: isStaff && !user.TwoFactorEnabled,
            user.IsActive,
            user.Theme,
            user.AvatarUrl);
    }

    internal static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw AuthErrors.IdentityFailed(result.Errors.Select(error => error.Description));
        }
    }

    private async Task<AuthResult> IssueSessionAsync(
        AppUser user,
        bool isPersistent,
        bool isMultiFactor,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var roles = await userManager.GetRolesAsync(user);
        var sessionVersion = await GetSessionVersionAsync(user.Id, cancellationToken);
        var accessToken = tokenService.CreateAccessToken(user, roles, isMultiFactor, sessionVersion);

        var (refreshToken, hash) = TokenService.CreateRefreshToken();
        var expiresAt = isPersistent
            ? now.AddDays(_options.RefreshTokenDays)
            : now.AddHours(_options.SessionRefreshTokenHours);
        var stored = new RefreshToken(user.Id, hash, expiresAt, isPersistent, isMultiFactor);
        stored.MarkCreated(now);
        dbContext.RefreshTokens.Add(stored);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken.Token,
            "Bearer",
            accessToken.ExpiresInSeconds,
            await ToResponseAsync(user),
            AuthClaimTypes.LoginViaPortal,
            ImpersonatorId: null,
            sessionVersion);
        return new AuthResult(response, new IssuedRefreshToken(refreshToken, expiresAt, isPersistent));
    }

    // Super Admin chỉ được nhớ thiết bị khi email nằm trong cấu hình. Kiểm tra ở cả lúc cấp lẫn lúc dùng: tài khoản được
    // nâng lên Super Admin sau đó, hoặc bị gỡ khỏi cấu hình, thì không còn được bỏ qua 2FA.
    private async Task<bool> CanTrustDeviceAsync(AppUser user) =>
        !await userManager.IsInRoleAsync(user, Roles.SuperAdmin)
        || (user.Email is { } email
            && _options.TrustedDeviceSuperAdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase));

    private async Task<bool> IsTrustedDeviceAsync(AppUser user, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = TokenService.HashRefreshToken(token);
        var isTrusted = await dbContext.TrustedDevices.AsNoTracking().AnyAsync(
            device => device.TokenHash == hash && device.UserId == user.Id && device.ExpiresAt > now,
            cancellationToken);
        return isTrusted && await CanTrustDeviceAsync(user);
    }

    private async Task<IssuedTrustedDevice> IssueTrustedDeviceAsync(long userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.TrustedDevices
            .Where(device => device.UserId == userId && device.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        // Cùng cách sinh token ngẫu nhiên 256 bit với refresh token.
        var (token, hash) = TokenService.CreateRefreshToken();
        var expiresAt = now.AddDays(_options.TrustedDeviceDays);
        dbContext.TrustedDevices.Add(new TrustedDevice(userId, hash, now, expiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Trusted device registered for user {UserId} until {ExpiresAt}", userId, expiresAt);
        return new IssuedTrustedDevice(token, expiresAt);
    }

    private Task<int> RevokeTrustedDevicesAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.TrustedDevices.Where(device => device.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    private async Task VerifyAuthenticatorCodeAsync(AppUser user, string code)
    {
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, code);
        if (!isValid)
        {
            await RegisterFailedAttemptAsync(user);
            throw AuthErrors.OtpInvalid();
        }

        await userManager.ResetAccessFailedCountAsync(user);
    }

    internal static void EnsureActive(AppUser user)
    {
        if (!user.IsActive)
        {
            throw AuthErrors.AccountDisabled();
        }
    }

    private async Task EnsureNotLockedOutAsync(AppUser user)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            throw AuthErrors.AccountLocked();
        }
    }

    // FR-03: 5 lần sai liên tiếp thì khóa 15 phút (cấu hình Lockout của Identity).
    private async Task RegisterFailedAttemptAsync(AppUser user)
    {
        await userManager.AccessFailedAsync(user);
        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("User {UserId} locked out after repeated failed attempts", user.Id);
            throw AuthErrors.AccountLocked();
        }
    }

    /// <summary>
    /// Kết thúc mọi phiên của user: thu hồi refresh token và tăng phiên bản phiên nên access token đang cầm hết hiệu lực
    /// ngay. Không báo FE; trả về số refresh token bị thu hồi.
    /// </summary>
    internal async Task<int> RevokeAllAsync(long userId, CancellationToken cancellationToken) =>
        (await EndSessionsAsync(userId, cancellationToken)).Revoked;

    /// <summary>
    /// Như <see cref="RevokeAllAsync"/> rồi báo các thiết bị đang mở qua Pusher kèm <paramref name="reason"/>. Với
    /// <paramref name="onlyIfHadSessions"/> thì chỉ báo khi thật sự có phiên bị thu hồi (tránh bắn thừa khi đăng nhập
    /// lần đầu); buộc đăng xuất thì luôn báo vì access token còn sống có thể không còn refresh token.
    /// </summary>
    internal async Task<int> ForceLogoutAsync(
        long userId,
        SessionEndReason reason,
        bool onlyIfHadSessions,
        CancellationToken cancellationToken)
    {
        var (revoked, version) = await EndSessionsAsync(userId, cancellationToken);
        if (reason == SessionEndReason.ForcedLogout)
        {
            // Đăng nhập nơi khác (SignedInElsewhere) không được xóa: nếu không thì thiết bị tin cậy hết tác dụng ngay lần
            // đăng nhập kế tiếp. Chỉ buộc đăng xuất, đặt lại mật khẩu, vô hiệu hóa mới thu hồi.
            await RevokeTrustedDevicesAsync(userId, cancellationToken);
        }

        if (!onlyIfHadSessions || revoked > 0)
        {
            await sessionNotifier.NotifySessionEndedAsync(userId, version, reason, cancellationToken);
        }

        return revoked;
    }

    /// <summary>Phiên bản phiên hiện tại của user trong CSDL (đọc mới, không dùng bản đang theo dõi trong bộ nhớ).</summary>
    internal Task<int> GetSessionVersionAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.SessionVersion)
            .SingleAsync(cancellationToken);

    /// <summary>
    /// Mỗi tài khoản chỉ đăng nhập một nơi: đăng nhập mới đá mọi phiên cũ và báo cho chúng. Gọi ngay trước khi cấp phiên
    /// mới để phiên mới mang đúng phiên bản vừa tăng.
    /// </summary>
    private Task EndOtherSessionsAsync(long userId, CancellationToken cancellationToken) =>
        ForceLogoutAsync(userId, SessionEndReason.SignedInElsewhere, onlyIfHadSessions: true, cancellationToken);

    private async Task<(int Revoked, int Version)> EndSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        // Xóa hẳn thay vì đánh dấu thu hồi: thiết bị bị đá thường còn thử refresh, và token đã thu hồi bị dùng lại sẽ
        // bị coi là bị đánh cắp (RefreshAsync) và kéo sập luôn phiên mới của chính chủ.
        var revoked = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteDeleteAsync(cancellationToken);

        // Tăng nguyên tử trong một câu UPDATE: hai lần đăng nhập đồng thời không cùng đọc một phiên bản cũ.
        var versions = await dbContext.Database
            .SqlQuery<int>($"UPDATE auth.users SET session_version = session_version + 1 WHERE id = {userId} RETURNING session_version AS \"Value\"")
            .ToListAsync(cancellationToken);
        return (revoked, versions.Count == 1 ? versions[0] : throw AuthErrors.UserNotFound());
    }

    private Task<AppUser?> FindUserAsync(long userId) =>
        userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture));
}
