using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;

namespace SachLuat.Modules.Promotion.Application;

/// <summary>Combo đang chạy cho khách xem (trang <c>/combo/{slug}</c> và mục "có trong combo" ở trang sách).</summary>
internal sealed class ComboStorefrontService(
    PromotionDbContext dbContext,
    ICatalogSkuReader skuReader,
    TimeProvider timeProvider)
{
    /// <summary>Số combo tối đa trả về một lần; combo ít nên không phân trang.</summary>
    public const int MaxPublicCombos = 50;

    /// <summary>
    /// Combo đang chạy, mới nhất trước; <paramref name="skuId"/> lọc các combo có chứa SKU đó. Combo có sách không
    /// còn ở Catalog hoặc đang ngừng bán bị ẩn khỏi danh sách (trang chi tiết vẫn mở được, kèm <c>isSellable</c>).
    /// </summary>
    public async Task<IReadOnlyList<ComboResponse>> ListAsync(long? skuId, CancellationToken cancellationToken)
    {
        var combos = dbContext.Combos.AsNoTracking().Include(combo => combo.Items).Where(Combo.RunningAt(Now()));
        if (skuId is { } id)
        {
            combos = combos.Where(combo => combo.Items.Any(item => item.SkuId == id));
        }

        var page = await combos
            .OrderByDescending(combo => combo.StartsAt)
            .ThenByDescending(combo => combo.Id)
            .Take(MaxPublicCombos)
            .ToListAsync(cancellationToken);

        var skus = await LoadSkusAsync(page, cancellationToken);
        return [.. page
            .Select(combo => ToResponse(combo, skus))
            .OfType<ComboResponse>()
            .Where(response => response.IsSellable)];
    }

    public async Task<ComboResponse> GetAsync(string slug, CancellationToken cancellationToken)
    {
        var combo = await dbContext.Combos.AsNoTracking()
            .Include(item => item.Items)
            .Where(Combo.RunningAt(Now()))
            .SingleOrDefaultAsync(item => item.Slug == slug, cancellationToken)
            ?? throw PromotionErrors.ComboNotFound();

        // Sách trong bộ không còn ở Catalog thì không dựng được trang: coi như combo không tồn tại.
        return ToResponse(combo, await LoadSkusAsync([combo], cancellationToken))
            ?? throw PromotionErrors.ComboNotFound();
    }

    private async Task<IReadOnlyDictionary<long, SkuInfo>> LoadSkusAsync(
        IEnumerable<Combo> combos,
        CancellationToken cancellationToken) =>
        await skuReader.GetSkusAsync(
            [.. combos.SelectMany(combo => combo.Items.Select(item => item.SkuId)).Distinct()],
            cancellationToken);

    private static ComboResponse? ToResponse(Combo combo, IReadOnlyDictionary<long, SkuInfo> skus)
    {
        if (combo.Items.Any(item => !skus.ContainsKey(item.SkuId)))
        {
            return null;
        }

        var items = combo.Items.OrderBy(item => item.Id)
            .Select(item =>
            {
                var sku = skus[item.SkuId];
                return new ComboItemResponse(
                    sku.SkuId,
                    sku.SkuCode,
                    sku.BookTitle,
                    sku.BookSlug,
                    sku.EditionName,
                    sku.CoverType,
                    sku.CoverImageUrl,
                    item.Quantity,
                    sku.SalePrice,
                    sku.IsSellable);
            })
            .ToList();
        var retailPrice = items.Sum(item => item.UnitPrice * item.Quantity);

        return new ComboResponse(
            combo.Id,
            combo.Name,
            combo.Slug,
            combo.Description,
            combo.ImageUrl,
            combo.ComboPrice,
            retailPrice,
            Math.Max(0m, retailPrice - combo.ComboPrice),
            items,
            items.All(item => item.IsSellable),
            combo.StartsAt,
            combo.EndsAt);
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
