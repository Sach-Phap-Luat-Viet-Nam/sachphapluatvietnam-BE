using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain.Editions;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class SkuPriceScheduleConfiguration : IEntityTypeConfiguration<SkuPriceSchedule>
{
    public void Configure(EntityTypeBuilder<SkuPriceSchedule> builder)
    {
        builder.ToTable("sku_price_schedules", table =>
        {
            table.HasCheckConstraint("ck_sku_price_schedules_price", "sale_price > 0");
            table.HasCheckConstraint("ck_sku_price_schedules_period", "ends_at > starts_at");
        });

        builder.Property(schedule => schedule.SalePrice).HasPrecision(12, 0);
        builder.Property(schedule => schedule.Note).HasMaxLength(SkuPriceSchedule.NoteMaxLength);

        // Tìm lịch đang chạy của một SKU: sku_id = ? AND ends_at > now.
        builder.HasIndex(schedule => new { schedule.SkuId, schedule.EndsAt });
    }
}
