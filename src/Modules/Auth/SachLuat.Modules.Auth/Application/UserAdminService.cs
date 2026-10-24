using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

/// <summary>Quản lý tài khoản nhân viên, khách hàng và vai trò (FR-97), chỉ Super Admin.</summary>
internal sealed class UserAdminService(
    UserManager<AppUser> userManager,
    AuthDbContext dbContext,
    AuthService authService,
    ProfileService profileService,
    TokenService tokenService,
    TimeProvider timeProvider,
    ILogger<UserAdminService> logger)
{
    public Task<PagedResponse<AdminUserResponse>> ListStaffAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        ListAsync(UsersInRoles(Roles.Staff), query, newestFirst: false, cancellationToken);

    public Task<PagedResponse<AdminUserResponse>> ListCustomersAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        ListAsync(UsersInRoles([Roles.Customer]), query, newestFirst: true, cancellationToken);

    /// <summary>Chi tiết khách hàng: hồ sơ, sổ địa chỉ, thông tin hóa đơn. Không phải khách hàng thì 404.</summary>
    public async Task<AdminCustomerResponse> GetCustomerAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await UsersInRoles([Roles.Customer]).AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw AuthErrors.UserNotFound();

        var roles = await RolesByUserAsync([user.Id], cancellationToken);
        return new AdminCustomerResponse(
            ToAdminResponse(user, roles),
            user.BirthDate,
            user.Occupation,
            await profileService.ListAddressesAsync(user.Id, cancellationToken),
            await profileService.GetInvoiceInfoAsync(user.Id, cancellationToken));
    }

    /// <summary>Tạo nhân viên với mật khẩu tạm; nhân viên phải bật 2FA trước khi dùng được API Admin.</summary>
    public async Task<AdminUserResponse> CreateStaffAsync(CreateStaffRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw AuthErrors.EmailTaken();
        }

        var user = new AppUser { UserName = request.Email, Email = request.Email, FullName = request.FullName.Trim() };
        AuthService.EnsureSucceeded(await userManager.CreateAsync(user, request.Password));
        AuthService.EnsureSucceeded(await userManager.AddToRolesAsync(user, request.Roles.Distinct()));
        logger.LogInformation("Created staff user {UserId} with roles {Roles}", user.Id, request.Roles);
        return await ToAdminResponseAsync(user);
    }

    public async Task<AdminUserResponse> SetRolesAsync(long actorUserId, long userId, SetRolesRequest request)
    {
        var user = await FindUserAsync(userId);
        var desired = request.Roles.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var current = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);

        if (actorUserId == userId && current.Contains(Roles.SuperAdmin) && !desired.Contains(Roles.SuperAdmin))
        {
            throw AuthErrors.CannotRemoveOwnAdmin();
        }

        AuthService.EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, current.Except(desired)));
        AuthService.EnsureSucceeded(await userManager.AddToRolesAsync(user, desired.Except(current)));
        logger.LogInformation(
            "User {ActorUserId} set roles of user {UserId} to {Roles}", actorUserId, userId, desired);
        return await ToAdminResponseAsync(user);
    }

    /// <summary>
    /// Đăng xuất hộ: thu hồi mọi phiên (refresh token) của user trên mọi thiết bị, ví dụ nhân viên nghỉ việc
    /// hoặc tài khoản bị lộ. Access token đã cấp vẫn dùng được tới khi hết hạn (tối đa <c>Jwt:AccessTokenMinutes</c>).
    /// </summary>
    public async Task<LogoutUserResponse> LogoutUserAsync(
        long actorUserId,
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        var revoked = await authService.RevokeAllAsync(user.Id, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} logged out user {UserId} ({RevokedSessions} sessions)", actorUserId, user.Id, revoked);
        return new LogoutUserResponse(revoked);
    }

    /// <summary>Khóa không thời hạn (nhân viên nghỉ việc, tài khoản bị lộ) và thu hồi mọi phiên.</summary>
    public async Task<AdminUserResponse> LockAsync(long actorUserId, long userId, CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        AuthService.EnsureSucceeded(await userManager.SetLockoutEnabledAsync(user, true));
        AuthService.EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, AppUser.DisabledLockoutEnd));
        var revoked = await authService.RevokeAllAsync(user.Id, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} locked user {UserId} ({RevokedSessions} sessions)", actorUserId, user.Id, revoked);
        return await ToAdminResponseAsync(user);
    }

    /// <summary>Mở khóa, kể cả tạm khóa 15 phút do đăng nhập sai; đếm lại số lần sai từ 0.</summary>
    public async Task<AdminUserResponse> UnlockAsync(long actorUserId, long userId)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        AuthService.EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, null));
        AuthService.EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        logger.LogWarning("User {ActorUserId} unlocked user {UserId}", actorUserId, user.Id);
        return await ToAdminResponseAsync(user);
    }

    /// <summary>
    /// Đặt mật khẩu tạm cho user quên mật khẩu (chưa có luồng quên mật khẩu qua email), thu hồi mọi phiên.
    /// </summary>
    public async Task ResetPasswordAsync(
        long actorUserId,
        long userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        var errors = new List<IdentityError>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, request.NewPassword);
            errors.AddRange(result.Errors);
        }

        if (errors.Count > 0)
        {
            AuthService.EnsureSucceeded(IdentityResult.Failed([.. errors]));
        }

        // Ghi hash mới trong một lần UPDATE: Remove + AddPassword có thể để user mất mật khẩu nếu lỗi giữa chừng.
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        AuthService.EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
        var revoked = await authService.RevokeAllAsync(user.Id, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} reset password of user {UserId} ({RevokedSessions} sessions)",
            actorUserId, user.Id, revoked);
    }

    /// <summary>
    /// Fast login: Super Admin lấy access token của tài khoản dưới cấp (không phải Super Admin).
    /// Không cấp refresh token để không ghi đè cookie phiên của chính admin; hết hạn thì gọi lại.
    /// </summary>
    public async Task<AuthResponse> ImpersonateAsync(long actorUserId, long userId)
    {
        var user = await FindUserAsync(userId);
        var roles = await userManager.GetRolesAsync(user);
        if (user.Id == actorUserId || roles.Contains(Roles.SuperAdmin))
        {
            throw AuthErrors.CannotImpersonate();
        }

        // Policy users_admin đã bắt admin qua 2FA nên phiên vào hộ cũng mang amr=mfa.
        var accessToken = tokenService.CreateAccessToken(user, roles, isMultiFactor: true, impersonatorId: actorUserId);
        logger.LogWarning("User {ActorUserId} impersonated user {UserId}", actorUserId, user.Id);

        return new AuthResponse(
            accessToken.Token,
            "Bearer",
            accessToken.ExpiresInSeconds,
            await authService.ToResponseAsync(user),
            AuthClaimTypes.LoginViaAdmin,
            actorUserId);
    }

    private IQueryable<AppUser> UsersInRoles(IReadOnlyCollection<string> roleNames)
    {
        var roleIds = dbContext.Roles
            .Where(role => roleNames.Contains(role.Name ?? string.Empty))
            .Select(role => role.Id);
        return dbContext.Users.Where(user => dbContext.UserRoles
            .Any(userRole => userRole.UserId == user.Id && roleIds.Contains(userRole.RoleId)));
    }

    private async Task<PagedResponse<AdminUserResponse>> ListAsync(
        IQueryable<AppUser> users,
        AdminUsersQuery query,
        bool newestFirst,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = LikePatterns.Contains(query.Q.Trim());
            const string Escape = LikePatterns.EscapeCharacter;
            users = users.Where(user => EF.Functions.ILike(user.Email ?? string.Empty, pattern, Escape)
                || EF.Functions.ILike(user.FullName, pattern, Escape)
                || EF.Functions.ILike(user.PhoneNumber ?? string.Empty, pattern, Escape));
        }

        var total = await users.CountAsync(cancellationToken);
        var ordered = newestFirst
            ? users.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id)
            : users.OrderBy(user => user.Id);
        var page = await ordered.AsNoTracking().Skip(query.Skip).Take(query.Limit).ToListAsync(cancellationToken);

        var roles = await RolesByUserAsync([.. page.Select(user => user.Id)], cancellationToken);
        return PagedResponse<AdminUserResponse>.Create(
            [.. page.Select(user => ToAdminResponse(user, roles))], query, total);
    }

    /// <summary>Vai trò của nhiều user trong một truy vấn (tránh N+1 khi liệt kê).</summary>
    private async Task<ILookup<long, string>> RolesByUserAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken)
    {
        var pairs = await dbContext.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);
        return pairs.ToLookup(pair => pair.UserId, pair => pair.Name ?? string.Empty);
    }

    private async Task<AdminUserResponse> ToAdminResponseAsync(AppUser user) =>
        ToAdminResponse(user, (await userManager.GetRolesAsync(user)).ToLookup(_ => user.Id));

    private AdminUserResponse ToAdminResponse(AppUser user, ILookup<long, string> rolesByUser)
    {
        var roles = rolesByUser[user.Id].Order(StringComparer.Ordinal).ToList();
        var now = timeProvider.GetUtcNow();
        var isLocked = user.LockoutEnd is { } lockoutEnd && lockoutEnd > now;
        return new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.PhoneNumber,
            roles,
            user.TwoFactorEnabled,
            RequiresTwoFactorSetup: roles.Any(Roles.Staff.Contains) && !user.TwoFactorEnabled,
            isLocked,
            isLocked && !user.IsDisabled ? user.LockoutEnd?.UtcDateTime : null,
            user.CreatedAt);
    }

    private async Task<AppUser> FindUserAsync(long userId) =>
        await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
        ?? throw AuthErrors.UserNotFound();

    /// <summary>Khóa/đặt lại mật khẩu chỉ cho tài khoản dưới cấp, cùng quy tắc với fast login.</summary>
    private async Task<AppUser> FindManageableUserAsync(long actorUserId, long userId)
    {
        var user = await FindUserAsync(userId);
        if (user.Id == actorUserId || await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            throw AuthErrors.CannotManageUser();
        }

        return user;
    }
}
