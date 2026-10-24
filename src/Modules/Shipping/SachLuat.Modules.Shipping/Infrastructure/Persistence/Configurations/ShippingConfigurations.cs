using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Shipping.Domain;

namespace SachLuat.Modules.Shipping.Infrastructure.Persistence.Configurations;

internal sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("shipping_zones", table =>
        {
            table.HasCheckConstraint("ck_shipping_zones_weights", "base_weight_gram > 0 AND step_weight_gram > 0");
            table.HasCheckConstraint("ck_shipping_zones_fees", "base_fee >= 0 AND step_fee >= 0");
            table.HasCheckConstraint(
                "ck_shipping_zones_eta", "eta_min_days >= 0 AND eta_max_days >= eta_min_days");
        });

        builder.Property(zone => zone.Name).HasMaxLength(ShippingZone.NameMaxLength);
        builder.Property(zone => zone.BaseFee).HasPrecision(12, 0);
        builder.Property(zone => zone.StepFee).HasPrecision(12, 0);
        builder.HasIndex(zone => zone.SortOrder);

        builder.HasMany(zone => zone.Provinces).WithOne().HasForeignKey(province => province.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(zone => zone.Provinces).HasField("_provinces");
    }
}

internal sealed class ShippingZoneProvinceConfiguration : IEntityTypeConfiguration<ShippingZoneProvince>
{
    public void Configure(EntityTypeBuilder<ShippingZoneProvince> builder)
    {
        builder.ToTable("shipping_zone_provinces");
        builder.Property(province => province.ProvinceKey).HasMaxLength(ProvinceKey.MaxLength);
        builder.Property(province => province.Name).HasMaxLength(ProvinceKey.MaxLength);

        // Mỗi tỉnh chỉ thuộc một vùng; vi phạm trả SHIPPING_PROVINCE_DUPLICATE.
        builder.HasIndex(province => province.ProvinceKey).IsUnique().HasDatabaseName("ux_shipping_zone_provinces_key");
    }
}

internal sealed class ShippingSettingsConfiguration : IEntityTypeConfiguration<ShippingSettings>
{
    public void Configure(EntityTypeBuilder<ShippingSettings> builder)
    {
        builder.ToTable("shipping_settings", table =>
        {
            table.HasCheckConstraint("ck_shipping_settings_singleton", "id = 1");
            table.HasCheckConstraint(
                "ck_shipping_settings_values",
                "packaging_weight_gram >= 0 AND default_book_weight_gram > 0 AND flat_fee >= 0");
        });

        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.FreeShippingThreshold).HasPrecision(12, 0);
        builder.Property(settings => settings.FlatFee).HasPrecision(12, 0);
        builder.Ignore(settings => settings.Values);
    }
}
