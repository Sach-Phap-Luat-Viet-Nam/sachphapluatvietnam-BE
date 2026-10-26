using System.Reflection;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.UnitTests.Modules.Promotion;

/// <summary>Dữ liệu dựng sẵn cho test khuyến mãi. Mặc định là "giảm 20% tổng đơn, tự áp, mọi sách".</summary>
internal static class PromotionTestData
{
    public static readonly DateTime Now = new(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc);

    public static PromotionCampaign Campaign(
        long id = 1,
        PromotionKind kind = PromotionKind.OrderDiscount,
        PromotionMode mode = PromotionMode.Automatic,
        DiscountType discountType = DiscountType.Percent,
        decimal value = 20m,
        decimal? maxDiscount = null,
        decimal minOrder = 0m,
        PromotionScopeType scope = PromotionScopeType.All,
        long[]? targets = null,
        bool excludeOnSale = false,
        PromotionAudience audience = PromotionAudience.Everyone,
        int? usageLimit = null,
        int? perCustomerLimit = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        bool isActive = true,
        int priority = 0,
        int usedCount = 0)
    {
        var campaign = new PromotionCampaign(
            new PromotionDetails(
                $"Chương trình {id}",
                $"Tiêu đề {id}",
                Description: null,
                kind,
                mode,
                discountType,
                value,
                maxDiscount,
                minOrder,
                scope,
                excludeOnSale,
                audience,
                usageLimit,
                perCustomerLimit,
                startsAt ?? Now.AddDays(-1),
                endsAt,
                isActive,
                priority,
                IsPublic: true),
            targets ?? []);
        SetId(campaign, id);
        for (var index = 0; index < usedCount; index++)
        {
            campaign.IncrementUsage();
        }

        return campaign;
    }

    /// <summary>Mặc định là combo "SKU 1 + SKU 2" giá 70.000đ, đang chạy.</summary>
    public static Combo NewCombo(
        long id = 1,
        decimal price = 70_000m,
        (long SkuId, int Quantity)[]? items = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        bool isActive = true)
    {
        var combo = new Combo(
            new ComboDetails(
                $"Combo {id}",
                $"combo-{id}",
                Description: null,
                ImageUrl: null,
                price,
                startsAt ?? Now.AddDays(-1),
                endsAt,
                isActive),
            (items ?? [(1, 1), (2, 1)]).Select(item => new ComboItemSpec(item.SkuId, item.Quantity)));
        SetId(combo, id);
        return combo;
    }

    public static Coupon NewCoupon(long id = 10, long promotionId = 1, string code = "MUATHI2026", int? usageLimit = null)
    {
        var coupon = new Coupon(promotionId, code, usageLimit);
        SetId(coupon, id);
        return coupon;
    }

    public static PromotionLine Line(
        long skuId,
        decimal unitPrice,
        int quantity = 1,
        long bookId = 0,
        long[]? categoryIds = null,
        bool isOnSale = false) => new(skuId, bookId == 0 ? skuId : bookId, categoryIds ?? [], unitPrice, quantity, isOnSale);

    public static PromotionRequest Request(
        IReadOnlyList<PromotionLine> lines,
        decimal shippingFee = 30_000m,
        long? userId = null,
        string? phone = "0901234567",
        string? couponCode = null) => new(lines, shippingFee, new PromotionCustomer(userId, phone), couponCode);

    public static PromotionEvaluation Evaluate(
        PromotionRequest request,
        IEnumerable<PromotionCandidate> candidates,
        IReadOnlyDictionary<long, int>? customerUsage = null,
        IEnumerable<Combo>? combos = null) =>
        PromotionEvaluator.Evaluate(
            [.. candidates], request, customerUsage ?? new Dictionary<long, int>(), Now, [.. combos ?? []]);

    public static PromotionCandidate Auto(PromotionCampaign campaign) => new(campaign, null);

    private static void SetId(Entity entity, long id) =>
        (typeof(Entity).GetProperty(nameof(Entity.Id), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Entity.Id not found"))
        .SetValue(entity, id);
}
