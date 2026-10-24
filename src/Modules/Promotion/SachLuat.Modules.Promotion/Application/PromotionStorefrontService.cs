using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;

namespace SachLuat.Modules.Promotion.Application;

internal sealed class PromotionStorefrontService(PromotionDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>Số ưu đãi tối đa hiện ở trang khuyến mãi.</summary>
    public const int MaxPublicPromotions = 20;

    /// <summary>
    /// Ưu đãi đang chạy và công khai, theo độ ưu tiên. Không trả mã giảm giá; mã riêng gửi cho từng khách
    /// (<c>IsPublic = false</c>) không hiện ở đây.
    /// </summary>
    public async Task<IReadOnlyList<PublicPromotionResponse>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await dbContext.Promotions.AsNoTracking()
            .Where(PromotionCampaign.RunningAt(now))
            .Where(promotion => promotion.IsPublic)
            .OrderByDescending(promotion => promotion.Priority)
            .ThenByDescending(promotion => promotion.StartsAt)
            .ThenByDescending(promotion => promotion.Id)
            .Take(MaxPublicPromotions)
            .Select(promotion => new PublicPromotionResponse(
                promotion.Id,
                promotion.Title,
                promotion.Description,
                promotion.Kind,
                promotion.Mode,
                promotion.DiscountType,
                promotion.DiscountValue,
                promotion.MaxDiscountAmount,
                promotion.MinOrderAmount,
                promotion.ScopeType,
                promotion.ExcludeOnSaleItems,
                promotion.Audience,
                promotion.StartsAt,
                promotion.EndsAt))
            .ToListAsync(cancellationToken);
    }
}
