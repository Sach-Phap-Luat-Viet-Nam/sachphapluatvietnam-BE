using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

internal sealed class UserNameReader(AuthDbContext dbContext) : IUserNameReader
{
    public async Task<IReadOnlyDictionary<long, string>> GetNamesAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var ids = userIds.Distinct().ToList();
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName, user.Email })
            .ToListAsync(cancellationToken);
        // Chưa có họ tên thì dùng email để vẫn biết là ai.
        return users.ToDictionary(
            user => user.Id,
            user => string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? $"#{user.Id}" : user.FullName);
    }
}
