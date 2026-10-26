using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Promotion.Infrastructure.Persistence;

/// <summary>
/// Schema <c>promotion</c>. <c>reference</c> (mã đơn), id sách/SKU/danh mục trong phạm vi và SKU trong combo tham chiếu
/// module khác nên không đặt khóa ngoại chéo schema.
/// </summary>
internal sealed class PromotionDbContext(DbContextOptions<PromotionDbContext> options) : DbContext(options)
{
    public const string Schema = "promotion";

    public DbSet<PromotionCampaign> Promotions => Set<PromotionCampaign>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<PromotionRedemption> Redemptions => Set<PromotionRedemption>();

    public DbSet<Combo> Combos => Set<Combo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MapEnum<PromotionKind>(configurationBuilder);
        MapEnum<PromotionMode>(configurationBuilder);
        MapEnum<DiscountType>(configurationBuilder);
        MapEnum<PromotionScopeType>(configurationBuilder);
        MapEnum<PromotionAudience>(configurationBuilder);
        MapEnum<RedemptionStatus>(configurationBuilder);
    }

    private static void MapEnum<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum =>
        configurationBuilder.Properties<TEnum>()
            .HaveConversion<SnakeCaseEnumConverter<TEnum>>()
            .HaveMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);
}
