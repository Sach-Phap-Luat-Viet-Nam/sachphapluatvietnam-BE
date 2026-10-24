using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application;

internal sealed class CatalogSkuReader(CatalogDbContext dbContext, TimeProvider timeProvider) : ICatalogSkuReader
{
    public async Task<IReadOnlyDictionary<long, SkuInfo>> GetSkusAsync(
        IReadOnlyCollection<long> skuIds,
        CancellationToken cancellationToken)
    {
        if (skuIds.Count == 0)
        {
            return new Dictionary<long, SkuInfo>();
        }

        var ids = skuIds.Distinct().ToList();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = await (
                from sku in dbContext.Skus.AsNoTracking()
                join edition in dbContext.Editions.AsNoTracking() on sku.EditionId equals edition.Id
                join book in dbContext.Books.AsNoTracking() on edition.BookId equals book.Id
                where ids.Contains(sku.Id)
                select new
                {
                    sku.Id,
                    sku.SkuCode,
                    BookId = book.Id,
                    book.Title,
                    book.Slug,
                    BookStatus = book.Status,
                    EditionId = edition.Id,
                    edition.EditionName,
                    edition.Isbn,
                    sku.CoverType,
                    sku.ListPrice,
                    // Giá hiện hành, khớp Sku.CurrentPriceAt.
                    CurrentPrice = sku.PriceSchedules
                        .Where(schedule => schedule.StartsAt <= now && schedule.EndsAt > now
                            && schedule.SalePrice < sku.SalePrice)
                        .Select(schedule => (decimal?)schedule.SalePrice)
                        .FirstOrDefault() ?? sku.SalePrice,
                    RegularSalePrice = sku.SalePrice,
                    edition.WeightGram,
                    edition.CoverImageUrl,
                    edition.IsLatest,
                    EditionStatus = edition.Status,
                    CategoryIds = book.Categories.Select(category => category.Id).ToList(),
                })
            .ToListAsync(cancellationToken);
        var parentOf = await ParentMapAsync(rows.Any(row => row.CategoryIds.Count > 0), cancellationToken);

        return rows.ToDictionary(row => row.Id, row => new SkuInfo(
            row.Id,
            row.SkuCode,
            row.BookId,
            row.Title,
            row.Slug,
            row.EditionId,
            row.EditionName,
            row.Isbn,
            SnakeCaseEnumConverter<CoverType>.ToProvider(row.CoverType),
            row.ListPrice,
            row.CurrentPrice,
            row.RegularSalePrice,
            row.WeightGram,
            row.CoverImageUrl,
            row.IsLatest,
            WithAncestors(row.CategoryIds, parentOf),
            IsSellable: row.BookStatus == BookStatus.Active
                && row.EditionStatus is EditionStatus.OnSale or EditionStatus.Clearance));
    }

    // Mỗi lần nạp cả cây danh mục (vài chục dòng) rẻ hơn truy vấn đệ quy cho từng sách.
    private async Task<IReadOnlyDictionary<long, long?>> ParentMapAsync(bool needed, CancellationToken cancellationToken) =>
        needed
            ? await dbContext.Categories.AsNoTracking()
                .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken)
            : new Dictionary<long, long?>();

    private static List<long> WithAncestors(IEnumerable<long> categoryIds, IReadOnlyDictionary<long, long?> parentOf)
    {
        var result = new HashSet<long>();
        foreach (var id in categoryIds)
        {
            // Chặn vòng lặp dữ liệu hỏng: HashSet.Add trả false khi đã gặp.
            for (long? current = id; current is { } value && result.Add(value);)
            {
                current = parentOf.GetValueOrDefault(value);
            }
        }

        return [.. result];
    }
}
