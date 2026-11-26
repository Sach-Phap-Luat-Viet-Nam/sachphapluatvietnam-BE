using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Content.Domain;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Content.Application;

internal sealed class StaticPageService(ContentDbContext dbContext, ILogger<StaticPageService> logger)
{
    public const string SlugIndex = "ux_static_pages_slug";

    /// <summary>Trang đã xuất bản theo slug; chưa xuất bản hoặc không có thì 404.</summary>
    public async Task<StaticPageResponse> GetPublishedAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await dbContext.StaticPages.AsNoTracking()
                .Where(page => page.IsPublished && page.Slug == normalized)
                .Select(page => new StaticPageResponse(
                    page.Slug, page.Title, page.Description, page.ContentHtml, page.UpdatedAt))
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw ContentErrors.StaticPageNotFound();
    }

    public async Task<PagedResponse<AdminStaticPageSummaryResponse>> ListAsync(
        PagedQuery query,
        CancellationToken cancellationToken)
    {
        var pages = dbContext.StaticPages.AsNoTracking();
        var total = await pages.CountAsync(cancellationToken);
        var items = await pages
            .OrderBy(page => page.Slug)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(page => new AdminStaticPageSummaryResponse(
                page.Id, page.Slug, page.Title, page.IsPublished, page.UpdatedAt))
            .ToListAsync(cancellationToken);
        return PagedResponse<AdminStaticPageSummaryResponse>.Create(items, query, total);
    }

    public async Task<AdminStaticPageResponse> GetAsync(long id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<AdminStaticPageResponse> CreateAsync(
        UpsertStaticPageRequest request,
        CancellationToken cancellationToken)
    {
        var page = new StaticPage(ToDetails(request));
        dbContext.StaticPages.Add(page);
        await SaveAsync(page.Slug, cancellationToken);
        logger.LogInformation("Created static page {StaticPageId} ({Slug})", page.Id, page.Slug);
        return ToResponse(page);
    }

    public async Task<AdminStaticPageResponse> UpdateAsync(
        long id,
        UpsertStaticPageRequest request,
        CancellationToken cancellationToken)
    {
        var page = await FindAsync(id, cancellationToken);
        page.Update(ToDetails(request));
        await SaveAsync(page.Slug, cancellationToken);
        logger.LogInformation("Updated static page {StaticPageId} ({Slug})", page.Id, page.Slug);
        return ToResponse(page);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var page = await FindAsync(id, cancellationToken);
        dbContext.StaticPages.Remove(page);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted static page {StaticPageId} ({Slug})", page.Id, page.Slug);
    }

    private async Task SaveAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(SlugIndex))
        {
            throw ContentErrors.StaticPageSlugTaken(slug);
        }
    }

    private static StaticPageDetails ToDetails(UpsertStaticPageRequest request) => new(
        request.Slug,
        request.Title,
        request.Description,
        request.ContentHtml,
        request.IsPublished);

    private static AdminStaticPageResponse ToResponse(StaticPage page) => new(
        page.Id,
        page.Slug,
        page.Title,
        page.Description,
        page.ContentHtml,
        page.IsPublished,
        page.CreatedAt,
        page.UpdatedAt);

    private async Task<StaticPage> FindAsync(long id, CancellationToken cancellationToken) =>
        await dbContext.StaticPages.SingleOrDefaultAsync(page => page.Id == id, cancellationToken)
        ?? throw ContentErrors.StaticPageNotFound();
}
