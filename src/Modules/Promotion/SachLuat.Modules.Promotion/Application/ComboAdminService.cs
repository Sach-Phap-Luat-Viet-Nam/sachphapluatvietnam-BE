using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Promotion.Application;

/// <summary>Quản lý combo bộ sách trên Admin (FR-16, FR-90).</summary>
internal sealed class ComboAdminService(
    PromotionDbContext dbContext,
    ICatalogSkuReader skuReader,
    IOptions<PromotionOptions> options,
    TimeProvider timeProvider,
    ILogger<ComboAdminService> logger)
{
    private const string SlugIndex = "ux_combos_slug";

    private readonly PromotionOptions _options = options.Value;

    public async Task<PagedResponse<AdminComboResponse>> ListAsync(
        AdminCombosQuery query,
        CancellationToken cancellationToken)
    {
        var combos = dbContext.Combos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = LikePatterns.Contains(query.Q.Trim());
            combos = combos.Where(combo =>
                EF.Functions.ILike(combo.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(combo.Slug, pattern, LikePatterns.EscapeCharacter));
        }

        var total = await combos.CountAsync(cancellationToken);
        var page = await combos
            .OrderByDescending(combo => combo.StartsAt)
            .ThenByDescending(combo => combo.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Include(combo => combo.Items)
            .ToListAsync(cancellationToken);

        var skuIds = page.SelectMany(combo => combo.Items.Select(item => item.SkuId));
        var skus = await LoadSkusAsync(skuIds, cancellationToken);
        var now = Now();
        return PagedResponse<AdminComboResponse>.Create(
            [.. page.Select(combo => ToResponse(combo, skus, now))],
            query,
            total);
    }

    public async Task<AdminComboResponse> GetAsync(long id, CancellationToken cancellationToken)
    {
        var combo = await FindAsync(id, tracking: false, cancellationToken);
        return await ToResponseAsync(combo, cancellationToken);
    }

    public async Task<AdminComboResponse> CreateAsync(UpsertComboRequest request, CancellationToken cancellationToken)
    {
        var items = ToSpecs(request);
        await EnsureItemsAllowedAsync(request.ComboPrice, items, cancellationToken);

        var combo = new Combo(ToDetails(request), items);
        dbContext.Combos.Add(combo);
        await SaveAsync(combo.Slug, cancellationToken);

        logger.LogInformation("Created combo {ComboId} ({Slug})", combo.Id, combo.Slug);
        return await ToResponseAsync(combo, cancellationToken);
    }

    public async Task<AdminComboResponse> UpdateAsync(
        long id,
        UpsertComboRequest request,
        CancellationToken cancellationToken)
    {
        var items = ToSpecs(request);
        await EnsureItemsAllowedAsync(request.ComboPrice, items, cancellationToken);

        var combo = await FindAsync(id, tracking: true, cancellationToken);
        combo.Update(ToDetails(request));
        combo.SetItems(items);
        await SaveAsync(combo.Slug, cancellationToken);

        logger.LogInformation("Updated combo {ComboId}", combo.Id);
        return await ToResponseAsync(combo, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var combo = await FindAsync(id, tracking: true, cancellationToken);
        dbContext.Combos.Remove(combo);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted combo {ComboId}", id);
    }

    /// <summary>
    /// SKU phải tồn tại ở Catalog, và giá combo phải thấp hơn tổng giá bán thường nhưng không vượt trần giảm giá
    /// (<see cref="PromotionOptions.MaxDiscountPercent"/>, cùng trần với chương trình giảm trên đơn).
    /// </summary>
    private async Task EnsureItemsAllowedAsync(
        decimal comboPrice,
        IReadOnlyList<ComboItemSpec> items,
        CancellationToken cancellationToken)
    {
        var skus = await LoadSkusAsync(items.Select(item => item.SkuId), cancellationToken);
        if (items.Any(item => !skus.ContainsKey(item.SkuId)))
        {
            throw PromotionErrors.ComboSkuNotFound();
        }

        var regularTotal = items.Sum(item => skus[item.SkuId].RegularSalePrice * item.Quantity);
        if (comboPrice >= regularTotal)
        {
            throw PromotionErrors.Invalid(
                $"Giá combo phải thấp hơn tổng giá bán thường của các sách ({regularTotal:N0}đ)");
        }

        if ((regularTotal - comboPrice) * 100 > regularTotal * _options.MaxDiscountPercent)
        {
            throw PromotionErrors.Invalid($"Combo giảm tối đa {_options.MaxDiscountPercent}% so với giá bán thường");
        }
    }

    private async Task SaveAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(SlugIndex))
        {
            throw PromotionErrors.ComboSlugDuplicate(slug);
        }
    }

    private async Task<Combo> FindAsync(long id, bool tracking, CancellationToken cancellationToken)
    {
        var combos = dbContext.Combos.Include(combo => combo.Items);
        return await (tracking ? combos : combos.AsNoTracking())
                .SingleOrDefaultAsync(combo => combo.Id == id, cancellationToken)
            ?? throw PromotionErrors.ComboNotFound();
    }

    private async Task<IReadOnlyDictionary<long, SkuInfo>> LoadSkusAsync(
        IEnumerable<long> skuIds,
        CancellationToken cancellationToken) =>
        await skuReader.GetSkusAsync([.. skuIds.Distinct()], cancellationToken);

    private async Task<AdminComboResponse> ToResponseAsync(Combo combo, CancellationToken cancellationToken) =>
        ToResponse(combo, await LoadSkusAsync(combo.Items.Select(item => item.SkuId), cancellationToken), Now());

    private static IReadOnlyList<ComboItemSpec> ToSpecs(UpsertComboRequest request) =>
        [.. (request.Items ?? []).Select(item => new ComboItemSpec(item.SkuId, item.Quantity))];

    private static ComboDetails ToDetails(UpsertComboRequest request) => new(
        request.Name,
        request.Slug,
        request.Description,
        request.ImageUrl,
        request.ComboPrice,
        request.StartsAt.UtcDateTime,
        request.EndsAt?.UtcDateTime,
        request.IsActive);

    private static AdminComboResponse ToResponse(
        Combo combo,
        IReadOnlyDictionary<long, SkuInfo> skus,
        DateTime now)
    {
        var items = combo.Items.OrderBy(item => item.Id).ToList();
        decimal? regularTotal = items.All(item => skus.ContainsKey(item.SkuId))
            ? items.Sum(item => skus[item.SkuId].RegularSalePrice * item.Quantity)
            : null;

        return new AdminComboResponse(
            combo.Id,
            combo.Name,
            combo.Slug,
            combo.Description,
            combo.ImageUrl,
            combo.ComboPrice,
            regularTotal,
            [.. items.Select(item => skus.TryGetValue(item.SkuId, out var sku)
                ? new AdminComboItemResponse(
                    item.SkuId, item.Quantity, sku.SkuCode, sku.BookTitle, sku.RegularSalePrice)
                : new AdminComboItemResponse(item.SkuId, item.Quantity, null, null, null))],
            combo.IsActive,
            combo.StartsAt,
            combo.EndsAt,
            combo.StatusAt(now),
            combo.CreatedAt,
            combo.UpdatedAt);
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
