using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
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
    ILogger<UserAdminService> logger)
{
    public Task<PagedResponse<UserResponse>> ListStaffAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        ListAsync(UsersInRoles(Roles.Staff), query, newestFirst: false, cancellationToken);

    public Task<PagedResponse<UserResponse>> ListCustomersAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        ListAsync(UsersInRoles([Roles.Customer]), query, newestFirst: true, cancellationToken);

    /// <summary>Chi tiết khách hàng: tài khoản, sổ địa chỉ, thông tin hóa đơn. Không phải khách hàng thì 404.</summary>
    public async Task<AdminCustomerResponse> GetCustomerAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await UsersInRoles([Roles.Customer]).AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw AuthErrors.UserNotFound();

        var roles = await RolesByUserAsync([user.Id], cancellationToken);
        return new AdminCustomerResponse(
            AuthService.ToResponse(user, roles[user.Id]),
            await profileService.ListAddressesAsync(user.Id, cancellationToken),
            await profileService.GetInvoiceInfoAsync(user.Id, cancellationToken));
    }

    /// <summary>Tạo nhân viên với mật khẩu tạm; Super Admin phải bật 2FA trước khi dùng được API Admin.</summary>
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

    /// <summary>
    /// Đổi vai trò tài khoản dưới cấp. Không đổi vai trò của chính mình hay Super Admin khác (chỉ có 1–2 tài khoản,
    /// quản lý ngoài giao diện), tránh tự gỡ quyền hoặc tự cấp thêm quyền.
    /// </summary>
    public async Task<UserResponse> SetRolesAsync(long actorUserId, long userId, SetRolesRequest request)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        var desired = request.Roles.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var current = (await userManager.GetRolesAsync(user)).ToHashSet(StringComparer.Ordinal);

        AuthService.EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, current.Except(desired)));
        AuthService.EnsureSucceeded(await userManager.AddToRolesAsync(user, desired.Except(current)));
        logger.LogInformation(
            "User {ActorUserId} set roles of user {UserId} to {Roles}", actorUserId, userId, desired);
        return await authService.ToResponseAsync(user);
    }

    /// <summary>
    /// Buộc đăng xuất (đăng xuất hộ): kết thúc mọi phiên của user trên mọi thiết bị **ngay lập tức** (access token cũng hết
    /// hiệu lực, không chờ <c>Jwt:AccessTokenMinutes</c>) và báo thiết bị đang mở qua Pusher, ví dụ nhân viên nghỉ việc
    /// hoặc tài khoản bị lộ.
    /// </summary>
    public async Task<LogoutUserResponse> LogoutUserAsync(
        long actorUserId,
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();

        var revoked = await authService.ForceLogoutAsync(
            user.Id, SessionEndReason.ForcedLogout, onlyIfHadSessions: false, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} force-logged out user {UserId} ({RevokedSessions} sessions)",
            actorUserId, user.Id, revoked);
        return new LogoutUserResponse(revoked);
    }

    /// <summary>
    /// Super Admin đặt mật khẩu mới cho tài khoản dưới cấp (quên mật khẩu, bàn giao máy). Mở khóa nếu đang bị khóa do
    /// sai mật khẩu và thu hồi mọi phiên để mật khẩu cũ không còn giữ được phiên nào.
    /// </summary>
    public async Task ResetPasswordAsync(
        long actorUserId,
        long userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        foreach (var validator in userManager.PasswordValidators)
        {
            AuthService.EnsureSucceeded(await validator.ValidateAsync(userManager, user, request.NewPassword));
        }

        // Không dùng RemovePasswordAsync + AddPasswordAsync: hai lần ghi riêng, lỗi ở giữa thì tài khoản mất mật khẩu.
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        AuthService.EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));

        var revoked = await authService.ForceLogoutAsync(
            user.Id, SessionEndReason.ForcedLogout, onlyIfHadSessions: true, cancellationToken);
        logger.LogWarning(
            "User {ActorUserId} reset the password of user {UserId} ({RevokedSessions} sessions revoked)",
            actorUserId, user.Id, revoked);
    }

    /// <summary>
    /// Vô hiệu hóa (<paramref name="isActive"/> = false) hoặc kích hoạt lại tài khoản dưới cấp. Vô hiệu hóa thì thu hồi
    /// mọi phiên; access token đã cấp vẫn dùng được tới khi hết hạn như <see cref="LogoutUserAsync"/>.
    /// </summary>
    public async Task<UserResponse> SetActiveAsync(
        long actorUserId,
        long userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var user = await FindManageableUserAsync(actorUserId, userId);
        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;
            AuthService.EnsureSucceeded(await userManager.UpdateAsync(user));
            logger.LogWarning(
                "User {ActorUserId} set user {UserId} active = {IsActive}", actorUserId, user.Id, isActive);
        }

        if (!isActive)
        {
            await authService.ForceLogoutAsync(
                user.Id, SessionEndReason.ForcedLogout, onlyIfHadSessions: false, cancellationToken);
        }

        return await authService.ToResponseAsync(user);
    }

    /// <summary>
    /// Fast login: Super Admin lấy access token của tài khoản dưới cấp (không phải Super Admin).
    /// Không cấp refresh token để không ghi đè cookie phiên của chính admin; hết hạn thì gọi lại.
    /// </summary>
    public async Task<AuthResponse> ImpersonateAsync(long actorUserId, long userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();
        var roles = await userManager.GetRolesAsync(user);
        if (user.Id == actorUserId || roles.Contains(Roles.SuperAdmin))
        {
            throw AuthErrors.CannotImpersonate();
        }

        AuthService.EnsureActive(user);

        // Policy users_admin đã bắt admin qua 2FA nên phiên vào hộ cũng mang amr=mfa.
        // Cùng phiên bản với tài khoản đích và không tăng nó: vào hộ không đá chính chủ, nhưng chính chủ đăng nhập lại
        // hoặc bị buộc đăng xuất thì phiên vào hộ cũng hết hiệu lực.
        var sessionVersion = await authService.GetSessionVersionAsync(user.Id, cancellationToken);
        var accessToken = tokenService.CreateAccessToken(
            user, roles, isMultiFactor: true, sessionVersion, impersonatorId: actorUserId);
        logger.LogWarning("User {ActorUserId} impersonated user {UserId}", actorUserId, user.Id);

        return new AuthResponse(
            accessToken.Token,
            "Bearer",
            accessToken.ExpiresInSeconds,
            await authService.ToResponseAsync(user),
            AuthClaimTypes.LoginViaAdmin,
            actorUserId,
            sessionVersion);
    }

    private IQueryable<AppUser> UsersInRoles(IReadOnlyCollection<string> roleNames)
    {
        var roleIds = dbContext.Roles
            .Where(role => roleNames.Contains(role.Name ?? string.Empty))
            .Select(role => role.Id);
        return dbContext.Users.Where(user => dbContext.UserRoles
            .Any(userRole => userRole.UserId == user.Id && roleIds.Contains(userRole.RoleId)));
    }

    private async Task<PagedResponse<UserResponse>> ListAsync(
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
        return PagedResponse<UserResponse>.Create(
            [.. page.Select(user => AuthService.ToResponse(user, roles[user.Id]))], query, total);
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

    /// <summary>Chỉ quản lý được tài khoản dưới cấp: không phải chính mình, không có vai trò Super Admin.</summary>
    private async Task<AppUser> FindManageableUserAsync(long actorUserId, long userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture))
            ?? throw AuthErrors.UserNotFound();
        if (user.Id == actorUserId || await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            throw AuthErrors.CannotManageUser();
        }

        return user;
    }
}
