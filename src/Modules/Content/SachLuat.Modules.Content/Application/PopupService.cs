using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Content.Domain;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Content.Application;

internal sealed class PopupService(
    ContentDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PopupService> logger)
{
    /// <summary>Popup Storefront hiển thị lúc này: bật, trong lịch, ưu tiên cao nhất; null nếu không có.</summary>
    public Task<PopupResponse?> GetActiveAsync(CancellationToken cancellationToken) =>
        dbContext.Popups.AsNoTracking()
            .Where(Popup.ShowingAt(Now()))
            .OrderByDescending(popup => popup.Priority)
            .ThenByDescending(popup => popup.StartsAt)
            .ThenByDescending(popup => popup.Id)
            .Select(popup => new PopupResponse(
                popup.Id, popup.Title, popup.ImageUrl, popup.LinkUrl, popup.DisplayFrequency, popup.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<AdminPopupResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var popups = dbContext.Popups.AsNoTracking();
        var total = await popups.CountAsync(cancellationToken);
        var page = await popups
            .OrderByDescending(popup => popup.Priority)
            .ThenByDescending(popup => popup.StartsAt)
            .ThenByDescending(popup => popup.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        var now = Now();
        return PagedResponse<AdminPopupResponse>.Create([.. page.Select(popup => ToResponse(popup, now))], query, total);
    }

    public async Task<AdminPopupResponse> GetAsync(long id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken), Now());

    public async Task<AdminPopupResponse> CreateAsync(UpsertPopupRequest request, CancellationToken cancellationToken)
    {
        var popup = new Popup(ToDetails(request));
        dbContext.Popups.Add(popup);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created popup {PopupId}", popup.Id);
        return ToResponse(popup, Now());
    }

    public async Task<AdminPopupResponse> UpdateAsync(
        long id,
        UpsertPopupRequest request,
        CancellationToken cancellationToken)
    {
        var popup = await FindAsync(id, cancellationToken);
        popup.Update(ToDetails(request));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated popup {PopupId}", popup.Id);
        return ToResponse(popup, Now());
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var popup = await FindAsync(id, cancellationToken);
        dbContext.Popups.Remove(popup);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted popup {PopupId}", popup.Id);
    }

    private static PopupDetails ToDetails(UpsertPopupRequest request) => new(
        request.Title,
        request.ImageUrl,
        request.LinkUrl,
        request.StartsAt.UtcDateTime,
        request.EndsAt?.UtcDateTime,
        request.IsActive,
        request.Priority,
        request.DisplayFrequency);

    private static AdminPopupResponse ToResponse(Popup popup, DateTime now) => new(
        popup.Id,
        popup.Title,
        popup.ImageUrl,
        popup.LinkUrl,
        popup.StartsAt,
        popup.EndsAt,
        popup.IsActive,
        popup.Priority,
        popup.DisplayFrequency,
        popup.StatusAt(now),
        popup.CreatedAt,
        popup.UpdatedAt);

    private async Task<Popup> FindAsync(long id, CancellationToken cancellationToken) =>
        await dbContext.Popups.SingleOrDefaultAsync(popup => popup.Id == id, cancellationToken)
        ?? throw ContentErrors.PopupNotFound();

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
