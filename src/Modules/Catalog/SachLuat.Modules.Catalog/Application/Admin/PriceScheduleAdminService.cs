using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;

namespace SachLuat.Modules.Catalog.Application.Admin;

/// <summary>Lịch giá (giảm giá có thời hạn) của SKU trên Admin (FR-63).</summary>
internal sealed class PriceScheduleAdminService(
    CatalogDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PriceScheduleAdminService> logger)
{
    public async Task<IReadOnlyList<PriceScheduleResponse>> ListAsync(long skuId, CancellationToken cancellationToken)
    {
        var sku = await dbContext.Skus.AsNoTracking()
            .Include(s => s.PriceSchedules)
            .SingleOrDefaultAsync(s => s.Id == skuId, cancellationToken)
            ?? throw CatalogErrors.NotFound("SKU");

        var now = Now();
        return
        [
            .. sku.PriceSchedules
                .OrderByDescending(schedule => schedule.StartsAt)
                .ThenByDescending(schedule => schedule.Id)
                .Select(schedule => ToResponse(sku, schedule, now)),
        ];
    }

    public async Task<PriceScheduleResponse> CreateAsync(
        long skuId,
        UpsertPriceScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sku = await LoadLockedAsync(skuId, cancellationToken);

        var schedule = sku.AddPriceSchedule(
            request.SalePrice, request.StartsAt.UtcDateTime, request.EndsAt.UtcDateTime, request.Note, now);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created price schedule {PriceScheduleId} for SKU {SkuId}", schedule.Id, sku.Id);
        return ToResponse(sku, schedule, now);
    }

    public async Task<PriceScheduleResponse> UpdateAsync(
        long scheduleId,
        UpsertPriceScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var now = Now();
        var skuId = await FindSkuIdAsync(scheduleId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sku = await LoadLockedAsync(skuId, cancellationToken);

        var schedule = sku.UpdatePriceSchedule(
            scheduleId, request.SalePrice, request.StartsAt.UtcDateTime, request.EndsAt.UtcDateTime, request.Note, now);
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Updated price schedule {PriceScheduleId} for SKU {SkuId}", schedule.Id, sku.Id);
        return ToResponse(sku, schedule, now);
    }

    public async Task DeleteAsync(long scheduleId, CancellationToken cancellationToken)
    {
        var skuId = await FindSkuIdAsync(scheduleId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sku = await LoadLockedAsync(skuId, cancellationToken);

        sku.RemovePriceSchedule(scheduleId, Now());
        await dbContext.SaveCatalogChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Deleted price schedule {PriceScheduleId} for SKU {SkuId}", scheduleId, skuId);
    }

    private static PriceScheduleResponse ToResponse(Sku sku, SkuPriceSchedule schedule, DateTime now) => new(
        schedule.Id,
        sku.Id,
        schedule.SalePrice,
        Pricing.DiscountPercent(sku.ListPrice, schedule.SalePrice),
        schedule.StartsAt,
        schedule.EndsAt,
        schedule.Note,
        schedule.StatusAt(now),
        schedule.CreatedAt,
        schedule.UpdatedAt);

    private async Task<long> FindSkuIdAsync(long scheduleId, CancellationToken cancellationToken) =>
        await dbContext.SkuPriceSchedules
            .Where(schedule => schedule.Id == scheduleId)
            .Select(schedule => (long?)schedule.SkuId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw CatalogErrors.NotFound("lịch giá");

    // Khóa dòng SKU: hai người cùng thêm lịch thì người sau thấy lịch của người trước, không lọt lịch chồng nhau.
    private async Task<Sku> LoadLockedAsync(long skuId, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM catalog.skus WHERE id = {skuId} FOR UPDATE", cancellationToken);
        return await dbContext.Skus
                .Include(sku => sku.PriceSchedules)
                .SingleOrDefaultAsync(sku => sku.Id == skuId, cancellationToken)
            ?? throw CatalogErrors.NotFound("SKU");
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
