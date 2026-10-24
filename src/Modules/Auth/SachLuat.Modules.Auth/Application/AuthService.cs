using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

internal sealed class AuthService(
    UserManager<AppUser> userManager,
    AuthDbContext dbContext,
    TokenService tokenService,
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

    public async Task<(LoginResponse Response, IssuedRefreshToken? RefreshToken)> LoginAsync(
        LoginRequest request,
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

        if (user.TwoFactorEnabled)
        {
            var ticket = tokenService.CreateTwoFactorTicket(user.Id, request.RememberMe);
            return (new LoginResponse(RequiresTwoFactor: true, ticket, Session: null), null);
        }

        var session = await IssueSessionAsync(user, request.RememberMe, isMultiFactor: false, cancellationToken);
        return (new LoginResponse(RequiresTwoFactor: false, TwoFactorToken: null, session.Response), session.RefreshToken);
    }

    public async Task<AuthResult> LoginTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tokenService.ValidateTwoFactorTicketAsync(request.TwoFactorToken)
            ?? throw AuthErrors.TwoFactorTokenInvalid();
        var user = await FindUserAsync(ticket.UserId) ?? throw AuthErrors.TwoFactorTokenInvalid();

        await EnsureNotLockedOutAsync(user);
        await VerifyAuthenticatorCodeAsync(user, request.Code);
        EnsureActive(user);

        return await IssueSessionAsync(user, ticket.RememberMe, isMultiFactor: true, cancellationToken);
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
            user.Theme);
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
        var accessToken = tokenService.CreateAccessToken(user, roles, isMultiFactor);

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
            ImpersonatorId: null);
        return new AuthResult(response, new IssuedRefreshToken(refreshToken, expiresAt, isPersistent));
    }

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

    /// <summary>Thu hồi mọi refresh token còn hiệu lực của user; trả về số phiên bị thu hồi.</summary>
    internal Task<int> RevokeAllAsync(long userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private Task<AppUser?> FindUserAsync(long userId) =>
        userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture));
}
