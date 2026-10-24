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
                    edition.WeightGram,
                    edition.CoverImageUrl,
                    edition.IsLatest,
                    EditionStatus = edition.Status,
                })
            .ToListAsync(cancellationToken);

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
            row.WeightGram,
            row.CoverImageUrl,
            row.IsLatest,
            IsSellable: row.BookStatus == BookStatus.Active
                && row.EditionStatus is EditionStatus.OnSale or EditionStatus.Clearance));
    }
}
