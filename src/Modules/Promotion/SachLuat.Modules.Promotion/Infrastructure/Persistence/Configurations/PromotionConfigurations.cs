using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Promotion.Domain;

namespace SachLuat.Modules.Promotion.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<PromotionCampaign>
{
    public void Configure(EntityTypeBuilder<PromotionCampaign> builder)
    {
        builder.ToTable("promotions", table =>
        {
            table.HasCheckConstraint("ck_promotions_discount", "discount_value > 0");
            table.HasCheckConstraint(
                "ck_promotions_limits",
                "min_order_amount >= 0 AND used_count >= 0 AND (usage_limit IS NULL OR usage_limit > 0) "
                + "AND (per_customer_limit IS NULL OR per_customer_limit > 0)");
            table.HasCheckConstraint("ck_promotions_schedule", "ends_at IS NULL OR ends_at > starts_at");
        });

        builder.Property(promotion => promotion.Name).HasMaxLength(PromotionCampaign.NameMaxLength);
        builder.Property(promotion => promotion.Title).HasMaxLength(PromotionCampaign.NameMaxLength);
        builder.Property(promotion => promotion.Description).HasMaxLength(PromotionCampaign.DescriptionMaxLength);
        builder.Property(promotion => promotion.DiscountValue).HasPrecision(12, 0);
        builder.Property(promotion => promotion.MaxDiscountAmount).HasPrecision(12, 0);
        builder.Property(promotion => promotion.MinOrderAmount).HasPrecision(12, 0);
        builder.HasIndex(promotion => new { promotion.IsActive, promotion.StartsAt });

        builder.HasMany(promotion => promotion.Targets).WithOne().HasForeignKey(target => target.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(promotion => promotion.Targets).HasField("_targets");

        builder.HasQueryFilter(promotion => promotion.DeletedAt == null);
    }
}

internal sealed class PromotionTargetConfiguration : IEntityTypeConfiguration<PromotionTarget>
{
    public void Configure(EntityTypeBuilder<PromotionTarget> builder)
    {
        builder.ToTable("promotion_targets");
        builder.HasIndex(target => new { target.PromotionId, target.TargetId }).IsUnique();
    }
}

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons", table =>
        {
            table.HasCheckConstraint(
                "ck_coupons_limits", "used_count >= 0 AND (usage_limit IS NULL OR usage_limit > 0)");
        });

        builder.Property(coupon => coupon.Code).HasMaxLength(Coupon.CodeMaxLength);
        builder.HasIndex(coupon => coupon.Code).IsUnique().HasDatabaseName("ux_coupons_code");
        builder.HasIndex(coupon => coupon.PromotionId);

        builder.HasOne<PromotionCampaign>().WithMany().HasForeignKey(coupon => coupon.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> builder)
    {
        builder.ToTable("promotion_redemptions", table => table.HasCheckConstraint(
            "ck_promotion_redemptions_amount", "amount >= 0"));

        builder.Property(redemption => redemption.Reference).HasMaxLength(PromotionRedemption.ReferenceMaxLength);
        builder.Property(redemption => redemption.CustomerKey).HasMaxLength(PromotionRedemption.CustomerKeyMaxLength);
        builder.Property(redemption => redemption.Amount).HasPrecision(12, 0);

        // Ghi nhận lại cùng đơn không tính trùng lượt.
        builder.HasIndex(redemption => new { redemption.Reference, redemption.PromotionId })
            .IsUnique().HasDatabaseName("ux_promotion_redemptions_reference_promotion");

        // Đếm lượt đã dùng của một khách theo chương trình (giới hạn mỗi khách).
        builder.HasIndex(redemption => new { redemption.PromotionId, redemption.CustomerKey })
            .HasFilter("status = 'redeemed'");

        builder.HasOne<PromotionCampaign>().WithMany().HasForeignKey(redemption => redemption.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Coupon>().WithMany().HasForeignKey(redemption => redemption.CouponId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
