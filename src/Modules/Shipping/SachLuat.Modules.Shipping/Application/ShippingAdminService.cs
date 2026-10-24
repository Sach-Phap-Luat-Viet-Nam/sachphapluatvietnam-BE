using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Shipping.Domain;
using SachLuat.Modules.Shipping.Infrastructure.Persistence;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Shipping.Application;

internal sealed class ShippingAdminService(
    ShippingDbContext dbContext,
    ShippingQuoteService quoteService,
    ILogger<ShippingAdminService> logger)
{
    private const string ProvinceIndexName = "ux_shipping_zone_provinces_key";

    public async Task<IReadOnlyList<ZoneResponse>> ListZonesAsync(CancellationToken cancellationToken)
    {
        var zones = await dbContext.Zones.AsNoTracking()
            .Include(zone => zone.Provinces)
            .OrderBy(zone => zone.SortOrder)
            .ThenBy(zone => zone.Id)
            .ToListAsync(cancellationToken);
        return [.. zones.Select(ToResponse)];
    }

    public async Task<ZoneResponse> CreateZoneAsync(UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        var zone = new ShippingZone(ToDetails(request));
        zone.SetProvinces(request.Provinces);
        dbContext.Zones.Add(zone);
        await SaveAsync(request, cancellationToken);
        logger.LogInformation("Created shipping zone {ZoneId}", zone.Id);
        return ToResponse(zone);
    }

    public async Task<ZoneResponse> UpdateZoneAsync(
        long id,
        UpsertZoneRequest request,
        CancellationToken cancellationToken)
    {
        var zone = await FindZoneAsync(id, cancellationToken);
        zone.Update(ToDetails(request));
        zone.SetProvinces(request.Provinces);
        await SaveAsync(request, cancellationToken);
        logger.LogInformation("Updated shipping zone {ZoneId}", zone.Id);
        return ToResponse(zone);
    }

    public async Task DeleteZoneAsync(long id, CancellationToken cancellationToken)
    {
        var zone = await FindZoneAsync(id, cancellationToken);
        dbContext.Zones.Remove(zone);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted shipping zone {ZoneId}", id);
    }

    public async Task<ShippingSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var values = await quoteService.GetSettingsAsync(cancellationToken);
        var stored = await dbContext.Settings.AsNoTracking()
            .AnyAsync(settings => settings.Id == ShippingSettings.SingletonId, cancellationToken);
        return ToResponse(values, isDefault: !stored);
    }

    public async Task<ShippingSettingsResponse> UpdateSettingsAsync(
        ShippingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var values = new ShippingSettingsValues(
            request.FreeShippingThreshold,
            request.PackagingWeightGram,
            request.DefaultBookWeightGram,
            request.FlatFee);
        var settings = await dbContext.Settings
            .SingleOrDefaultAsync(item => item.Id == ShippingSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            dbContext.Settings.Add(new ShippingSettings(values));
        }
        else
        {
            settings.Update(values);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Updated shipping settings: threshold {Threshold}, flat fee {FlatFee}",
            values.FreeShippingThreshold, values.FlatFee);
        return ToResponse(values, isDefault: false);
    }

    private async Task SaveAsync(UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(ProvinceIndexName))
        {
            // Báo tỉnh đầu tiên đã thuộc vùng khác để Admin sửa; không lộ chi tiết lỗi CSDL.
            var taken = await dbContext.Zones.AsNoTracking()
                .SelectMany(zone => zone.Provinces.Select(province => province.ProvinceKey))
                .ToListAsync(CancellationToken.None);
            var conflict = request.Provinces.FirstOrDefault(name => taken.Contains(ProvinceKey.Normalize(name)))
                ?? request.Provinces[0];
            throw ShippingErrors.ProvinceDuplicate(conflict);
        }
    }

    private async Task<ShippingZone> FindZoneAsync(long id, CancellationToken cancellationToken) =>
        await dbContext.Zones.Include(zone => zone.Provinces)
            .SingleOrDefaultAsync(zone => zone.Id == id, cancellationToken)
        ?? throw ShippingErrors.ZoneNotFound();

    private static ZoneDetails ToDetails(UpsertZoneRequest request) => new(
        request.Name,
        request.IsActive,
        request.SortOrder,
        request.BaseWeightGram,
        request.BaseFee,
        request.StepWeightGram,
        request.StepFee,
        request.EtaMinDays,
        request.EtaMaxDays);

    private static ZoneResponse ToResponse(ShippingZone zone) => new(
        zone.Id,
        zone.Name,
        zone.IsActive,
        zone.SortOrder,
        [.. zone.Provinces.Select(province => province.Name).Order(StringComparer.CurrentCulture)],
        zone.BaseWeightGram,
        zone.BaseFee,
        zone.StepWeightGram,
        zone.StepFee,
        zone.EtaMinDays,
        zone.EtaMaxDays,
        zone.UpdatedAt);

    private static ShippingSettingsResponse ToResponse(ShippingSettingsValues values, bool isDefault) => new(
        values.FreeShippingThreshold,
        values.PackagingWeightGram,
        values.DefaultBookWeightGram,
        values.FlatFee,
        isDefault);
}
