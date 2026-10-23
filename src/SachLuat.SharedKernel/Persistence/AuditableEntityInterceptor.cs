using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.SharedKernel.Persistence;

/// <summary>
/// Gán <c>CreatedAt</c>/<c>UpdatedAt</c> và chuyển lệnh xóa thành xóa mềm cho <see cref="ISoftDeletable"/>.
/// </summary>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // ToList: đổi State trong lúc duyệt ChangeTracker sẽ làm hỏng enumerator.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable })
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
            }

            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
            }
        }
    }
}
