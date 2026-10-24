using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

/// <summary>Quản lý tài khoản nhân viên và vai trò (FR-97), chỉ Super Admin.</summary>
internal sealed class UserAdminService(
    UserManager<AppUser> userManager,
    AuthService authService,
    TokenService tokenService,
    ILogger<UserAdminService> logger)
{
    public async Task<PagedResponse<UserResponse>> ListStaffAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var staffUserIds = new HashSet<long>();
        foreach (var role in Roles.Staff)
        {
            staffUserIds.UnionWith((await userManager.GetUsersInRoleAsync(role)).Select(user => user.Id));
        }

        var users = userManager.Users.Where(user => staffUserIds.Contains(user.Id));
        var total = await users.CountAsync(cancellationToken);
        var page = await users.OrderBy(user => user.Id).Skip(query.Skip).Take(query.Limit).ToListAsync(cancellationToken);

        var items = new List<UserResponse>(page.Count);
        foreach (var user in page)
        {
            items.Add(await authService.ToResponseAsync(user));
        }

        return PagedResponse<UserResponse>.Create(items, query, total);
    }

    /// <summary>Tạo nhân viên với mật khẩu tạm; nhân viên phải bật 2FA trước khi dùng được API Admin.</summary>
    public async Task<UserResponse> CreateStaffAsync(CreateStaffRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw AuthErrors.EmailTaken();
        }

        var user = new AppUser { UserName = request.Email, Email = request.Email, FullName = request.FullName.Trim() };
        AuthService.EnsureSucceeded(await userManager.CreateAsync(user, request.Password));
        AuthService.EnsureSucceeded(await userManager.AddToRolesAsync(user, request.Roles.Distinct()));
        logger.LogInformation("Created staff user {UserId} with roles {Roles}", user.Id, request.Roles);
        return await authService.ToResponseAsync(user);
    }

    public async Task<UserResponse> SetRolesAsync(long actorUserId, long userId, SetRolesRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();
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
        return await authService.ToResponseAsync(user);
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
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();

        var revoked = await authService.RevokeAllAsync(user.Id, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} logged out user {UserId} ({RevokedSessions} sessions)", actorUserId, user.Id, revoked);
        return new LogoutUserResponse(revoked);
    }

    /// <summary>
    /// Fast login: Super Admin lấy access token của tài khoản dưới cấp (không phải Super Admin).
    /// Không cấp refresh token để không ghi đè cookie phiên của chính admin; hết hạn thì gọi lại.
    /// </summary>
    public async Task<AuthResponse> ImpersonateAsync(long actorUserId, long userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();
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
}
