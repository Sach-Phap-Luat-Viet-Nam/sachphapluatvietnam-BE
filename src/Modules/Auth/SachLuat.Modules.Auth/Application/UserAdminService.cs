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
}
