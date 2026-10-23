using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain.Editions;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class SkuConfiguration : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> builder)
    {
        builder.ToTable("skus", table => table.HasCheckConstraint(
            "ck_skus_prices",
            "list_price > 0 AND sale_price > 0 AND sale_price <= list_price"));

        builder.Property(sku => sku.SkuCode).HasMaxLength(Sku.SkuCodeMaxLength);
        builder.HasIndex(sku => sku.SkuCode).IsUnique();
        builder.HasIndex(sku => new { sku.EditionId, sku.CoverType }).IsUnique();

        // Tiền VNĐ: số nguyên, không dùng float (mục 10.3).
        builder.Property(sku => sku.ListPrice).HasPrecision(12, 0);
        builder.Property(sku => sku.SalePrice).HasPrecision(12, 0);
    }
}
