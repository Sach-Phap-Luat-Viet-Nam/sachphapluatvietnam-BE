using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Promotion.Application;

/// <summary>Quản lý chương trình khuyến mãi và mã giảm giá trên Admin (FR-90).</summary>
internal sealed class PromotionAdminService(
    PromotionDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PromotionAdminService> logger)
{
    private const string CouponCodeIndex = "ux_coupons_code";
    private const int GenerateAttempts = 5;

    public async Task<PagedResponse<AdminPromotionResponse>> ListAsync(
        AdminPromotionsQuery query,
        CancellationToken cancellationToken)
    {
        var promotions = dbContext.Promotions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = LikePatterns.Contains(query.Q.Trim());
            promotions = promotions.Where(promotion =>
                EF.Functions.ILike(promotion.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(promotion.Title, pattern, LikePatterns.EscapeCharacter));
        }

        var total = await promotions.CountAsync(cancellationToken);
        var page = await promotions
            .OrderByDescending(promotion => promotion.Priority)
            .ThenByDescending(promotion => promotion.StartsAt)
            .ThenByDescending(promotion => promotion.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Include(promotion => promotion.Targets)
            .Select(promotion => new
            {
                Promotion = promotion,
                CouponCount = dbContext.Coupons.Count(coupon => coupon.PromotionId == promotion.Id),
            })
            .ToListAsync(cancellationToken);

        var now = Now();
        return PagedResponse<AdminPromotionResponse>.Create(
            [.. page.Select(item => ToResponse(item.Promotion, item.CouponCount, now))], query, total);
    }

    public async Task<AdminPromotionResponse> GetAsync(long id, CancellationToken cancellationToken)
    {
        var promotion = await FindAsync(id, tracking: false, cancellationToken);
        return ToResponse(promotion, await CouponCountAsync(id, cancellationToken), Now());
    }

    public async Task<AdminPromotionResponse> CreateAsync(
        UpsertPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var promotion = new PromotionCampaign(ToDetails(request), request.TargetIds ?? []);
        dbContext.Promotions.Add(promotion);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created promotion {PromotionId} ({Kind})", promotion.Id, promotion.Kind);
        return ToResponse(promotion, 0, Now());
    }

    public async Task<AdminPromotionResponse> UpdateAsync(
        long id,
        UpsertPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var promotion = await FindAsync(id, tracking: true, cancellationToken);
        promotion.Update(ToDetails(request));
        promotion.SetTargets(request.TargetIds ?? []);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated promotion {PromotionId}", promotion.Id);
        return ToResponse(promotion, await CouponCountAsync(id, cancellationToken), Now());
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var promotion = await FindAsync(id, tracking: true, cancellationToken);
        dbContext.Promotions.Remove(promotion);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted promotion {PromotionId}", id);
    }

    public async Task<PromotionStatsResponse> GetStatsAsync(long id, CancellationToken cancellationToken)
    {
        var promotion = await FindAsync(id, tracking: false, cancellationToken);
        var redemptions = dbContext.Redemptions.AsNoTracking().Where(item => item.PromotionId == id);

        var released = await redemptions.CountAsync(item => item.Status == RedemptionStatus.Released, cancellationToken);
        var active = redemptions.Where(item => item.Status == RedemptionStatus.Redeemed);
        var totals = await active
            .GroupBy(item => item.Kind)
            .Select(group => new { Kind = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var customers = await active
            .Where(item => item.CustomerKey != null)
            .Select(item => item.CustomerKey)
            .Distinct()
            .CountAsync(cancellationToken);

        return new PromotionStatsResponse(
            id,
            promotion.UsedCount,
            released,
            totals.Where(item => item.Kind == PromotionKind.OrderDiscount).Sum(item => item.Amount),
            totals.Where(item => item.Kind == PromotionKind.FreeShipping).Sum(item => item.Amount),
            customers);
    }

    public async Task<PagedResponse<CouponResponse>> ListCouponsAsync(
        long promotionId,
        PagedQuery query,
        CancellationToken cancellationToken)
    {
        await FindAsync(promotionId, tracking: false, cancellationToken);
        var coupons = dbContext.Coupons.AsNoTracking().Where(coupon => coupon.PromotionId == promotionId);
        var total = await coupons.CountAsync(cancellationToken);
        var page = await coupons
            .OrderByDescending(coupon => coupon.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        return PagedResponse<CouponResponse>.Create([.. page.Select(ToResponse)], query, total);
    }

    public async Task<CouponResponse> CreateCouponAsync(
        long promotionId,
        CreateCouponRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCouponPromotionAsync(promotionId, cancellationToken);
        var coupon = new Coupon(promotionId, request.Code, request.UsageLimit);
        dbContext.Coupons.Add(coupon);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(CouponCodeIndex))
        {
            throw PromotionErrors.CouponCodeDuplicate(coupon.Code);
        }

        logger.LogInformation("Created coupon {CouponId} for promotion {PromotionId}", coupon.Id, promotionId);
        return ToResponse(coupon);
    }

    /// <summary>Sinh hàng loạt mã ngẫu nhiên (mặc định dùng một lần), ví dụ mã riêng gửi cho từng khách (BR-30).</summary>
    public async Task<IReadOnlyList<CouponResponse>> GenerateCouponsAsync(
        long promotionId,
        GenerateCouponsRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCouponPromotionAsync(promotionId, cancellationToken);
        var prefix = CouponCodes.Normalize(request.Prefix);
        var usageLimit = request.UsageLimit ?? 1;

        for (var attempt = 1; attempt <= GenerateAttempts; attempt++)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            while (codes.Count < request.Count)
            {
                codes.Add(CouponCodes.Generate(prefix));
            }

            // Mã sinh ngẫu nhiên hiếm khi trùng; trùng thì bỏ cả lô và sinh lại thay vì lưu thiếu.
            var taken = await dbContext.Coupons.AsNoTracking()
                .Where(coupon => codes.Contains(coupon.Code))
                .AnyAsync(cancellationToken);
            if (taken)
            {
                continue;
            }

            var coupons = codes.Select(code => new Coupon(promotionId, code, usageLimit)).ToList();
            dbContext.Coupons.AddRange(coupons);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation(CouponCodeIndex))
            {
                dbContext.ChangeTracker.Clear();
                continue;
            }

            logger.LogInformation(
                "Generated {Count} coupons for promotion {PromotionId}", coupons.Count, promotionId);
            return [.. coupons.Select(ToResponse)];
        }

        throw PromotionErrors.Invalid("Không sinh được mã không trùng, vui lòng thử lại hoặc đổi tiền tố");
    }

    public async Task<CouponResponse> UpdateCouponAsync(
        long couponId,
        UpdateCouponRequest request,
        CancellationToken cancellationToken)
    {
        var coupon = await dbContext.Coupons.SingleOrDefaultAsync(item => item.Id == couponId, cancellationToken)
            ?? throw PromotionErrors.CouponNotFound();
        coupon.SetActive(request.IsActive);
        coupon.SetLimit(request.UsageLimit);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated coupon {CouponId}", couponId);
        return ToResponse(coupon);
    }

    private async Task EnsureCouponPromotionAsync(long promotionId, CancellationToken cancellationToken)
    {
        var promotion = await FindAsync(promotionId, tracking: false, cancellationToken);
        if (promotion.Mode != PromotionMode.Coupon)
        {
            throw PromotionErrors.PromotionNotCoupon();
        }
    }

    private async Task<PromotionCampaign> FindAsync(long id, bool tracking, CancellationToken cancellationToken)
    {
        var promotions = dbContext.Promotions.Include(promotion => promotion.Targets);
        return await (tracking ? promotions : promotions.AsNoTracking())
                .SingleOrDefaultAsync(promotion => promotion.Id == id, cancellationToken)
            ?? throw PromotionErrors.PromotionNotFound();
    }

    private Task<int> CouponCountAsync(long promotionId, CancellationToken cancellationToken) =>
        dbContext.Coupons.CountAsync(coupon => coupon.PromotionId == promotionId, cancellationToken);

    private static PromotionDetails ToDetails(UpsertPromotionRequest request) => new(
        request.Name,
        request.Title,
        request.Description,
        request.Kind,
        request.Mode,
        request.DiscountType,
        request.DiscountValue,
        request.MaxDiscountAmount,
        request.MinOrderAmount,
        request.ScopeType,
        request.ExcludeOnSaleItems,
        request.Audience,
        request.UsageLimit,
        request.PerCustomerLimit,
        request.StartsAt.UtcDateTime,
        request.EndsAt?.UtcDateTime,
        request.IsActive,
        request.Priority,
        request.IsPublic);

    private static AdminPromotionResponse ToResponse(PromotionCampaign promotion, int couponCount, DateTime now) => new(
        promotion.Id,
        promotion.Name,
        promotion.Title,
        promotion.Description,
        promotion.Kind,
        promotion.Mode,
        promotion.DiscountType,
        promotion.DiscountValue,
        promotion.MaxDiscountAmount,
        promotion.MinOrderAmount,
        promotion.ScopeType,
        [.. promotion.Targets.Select(target => target.TargetId).Order()],
        promotion.ExcludeOnSaleItems,
        promotion.Audience,
        promotion.UsageLimit,
        promotion.PerCustomerLimit,
        promotion.UsedCount,
        couponCount,
        promotion.StartsAt,
        promotion.EndsAt,
        promotion.IsActive,
        promotion.Priority,
        promotion.IsPublic,
        promotion.StatusAt(now),
        promotion.CreatedAt,
        promotion.UpdatedAt);

    private static CouponResponse ToResponse(Coupon coupon) => new(
        coupon.Id,
        coupon.PromotionId,
        coupon.Code,
        coupon.IsActive,
        coupon.UsageLimit,
        coupon.UsedCount,
        coupon.CreatedAt);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
