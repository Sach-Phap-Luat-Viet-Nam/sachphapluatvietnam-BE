using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.Modules.Shipping.Domain;
using SachLuat.Modules.Shipping.Infrastructure.Persistence;

namespace SachLuat.Modules.Shipping.Application;

internal sealed class ShippingQuoteService(ShippingDbContext dbContext, IOptions<ShippingOptions> options)
    : IShippingQuoteService
{
    public async Task<ShippingQuote> QuoteAsync(
        string? province,
        IReadOnlyCollection<ShippingItem> items,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var zone = await FindZoneAsync(province, cancellationToken);
        return ShippingQuoteCalculator.Calculate(settings, zone, items, subtotal);
    }

    /// <summary>Cấu hình đang áp dụng: dòng trong CSDL, hoặc mặc định từ cấu hình khi Admin chưa lưu.</summary>
    internal async Task<ShippingSettingsValues> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var stored = await dbContext.Settings.AsNoTracking()
            .Where(settings => settings.Id == ShippingSettings.SingletonId)
            .Select(settings => new ShippingSettingsValues(
                settings.FreeShippingThreshold,
                settings.PackagingWeightGram,
                settings.DefaultBookWeightGram,
                settings.FlatFee))
            .SingleOrDefaultAsync(cancellationToken);
        return stored ?? options.Value.ToValues();
    }

    private async Task<ShippingZone?> FindZoneAsync(string? province, CancellationToken cancellationToken)
    {
        var key = ProvinceKey.Normalize(province);
        if (key.Length == 0)
        {
            return null;
        }

        var zone = await dbContext.Zones.AsNoTracking()
            .Where(candidate => candidate.IsActive && candidate.Provinces.Any(item => item.ProvinceKey == key))
            .SingleOrDefaultAsync(cancellationToken);
        if (zone is not null)
        {
            return zone;
        }

        // Chưa cấu hình vùng nào: dùng phí cố định để hệ thống chạy được. Đã có vùng thì tỉnh nằm ngoài là chưa hỗ trợ.
        return await dbContext.Zones.AnyAsync(candidate => candidate.IsActive, cancellationToken)
            ? throw ShippingErrors.AddressNotSupported()
            : null;
    }
}
