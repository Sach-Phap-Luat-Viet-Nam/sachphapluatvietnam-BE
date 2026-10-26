using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Promotion.Domain;
using SachLuat.Modules.Promotion.Infrastructure.Persistence;

namespace SachLuat.Modules.Promotion.Application;

/// <summary>
/// Khuyến mãi cho đơn hàng (<see cref="IPromotionService"/>): tính giảm giá, ghi nhận và trả lượt dùng.
/// </summary>
/// <remarks>
/// Ghi nhận/trả lượt khóa dòng chương trình (<c>FOR UPDATE</c>) trước khi đọc lại giới hạn, nên hai đơn cùng tranh
/// lượt cuối cùng chỉ một đơn thắng. Mọi đường ghi đều khóa chương trình trước nên bộ đếm của mã (coupon) cũng được
/// tuần tự hóa theo, không cần khóa riêng.
/// </remarks>
internal sealed class PromotionService(
    PromotionDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<PromotionOptions> options,
    ILogger<PromotionService> logger) : IPromotionService
{
    public async Task<PromotionEvaluation> EvaluateAsync(
        PromotionRequest request,
        CancellationToken cancellationToken)
    {
        var now = Now();
        var candidates = (await dbContext.Promotions.AsNoTracking()
                .Include(promotion => promotion.Targets)
                .Where(promotion => promotion.Mode == PromotionMode.Automatic)
                .Where(PromotionCampaign.RunningAt(now))
                .ToListAsync(cancellationToken))
            .Select(promotion => new PromotionCandidate(promotion, null))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            candidates.Add(await FindCouponCandidateAsync(request.CouponCode, cancellationToken));
        }

        var usage = await CustomerUsageAsync(candidates, request.Customer, cancellationToken);
        var combos = await RunningCombosAsync(request.Lines, now, cancellationToken);
        return PromotionEvaluator.Evaluate(
            candidates, request, usage, now, combos, options.Value.MaxTotalDiscountPercent);
    }

    public async Task RedeemAsync(
        string reference,
        PromotionEvaluation evaluation,
        PromotionCustomer customer,
        CancellationToken cancellationToken)
    {
        // Combo chưa có giới hạn lượt và PromotionId của nó là id combo (không phải chương trình) nên không ghi nhận.
        var redeemable = evaluation.Applied
            .Where(item => item.Kind != PromotionKind.Combo)
            .OrderBy(item => item.PromotionId)
            .ToList();
        if (redeemable.Count == 0)
        {
            return;
        }

        var now = Now();
        var customerKey = PromotionCustomerKey.From(customer);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var applied in redeemable)
        {
            await LockAsync(applied.PromotionId, cancellationToken);

            // Gọi lại cho cùng đơn (sau lỗi giữa chừng): đã ghi nhận thì bỏ qua, không tính trùng.
            if (await dbContext.Redemptions.AnyAsync(
                    redemption => redemption.Reference == reference && redemption.PromotionId == applied.PromotionId,
                    cancellationToken))
            {
                continue;
            }

            var promotion = await dbContext.Promotions
                .SingleOrDefaultAsync(item => item.Id == applied.PromotionId, cancellationToken);
            var coupon = applied.CouponId is { } couponId
                ? await dbContext.Coupons.SingleOrDefaultAsync(item => item.Id == couponId, cancellationToken)
                : null;
            if (promotion is null || !promotion.IsRunningAt(now)
                || (applied.CouponId is not null && (coupon is not { IsActive: true, IsExhausted: false })))
            {
                throw applied.CouponId is null
                    ? PromotionErrors.PromotionUnavailable()
                    : PromotionErrors.CouponExpired();
            }

            if (promotion.PerCustomerLimit is { } perCustomer && customerKey is not null
                && await dbContext.Redemptions.CountAsync(
                    redemption => redemption.PromotionId == promotion.Id
                        && redemption.CustomerKey == customerKey
                        && redemption.Status == RedemptionStatus.Redeemed,
                    cancellationToken) >= perCustomer)
            {
                throw applied.CouponId is null
                    ? PromotionErrors.PromotionUnavailable()
                    : PromotionErrors.CouponCustomerLimit();
            }

            dbContext.Redemptions.Add(new PromotionRedemption(
                reference, promotion.Id, applied.CouponId, customerKey, applied.Kind, applied.Amount, now));
            promotion.IncrementUsage();
            coupon?.IncrementUsage();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Redeemed {PromotionCount} promotions for order {OrderCode}", redeemable.Count, reference);
    }

    public async Task ReleaseAsync(string reference, CancellationToken cancellationToken)
    {
        var promotionIds = await dbContext.Redemptions.AsNoTracking()
            .Where(redemption => redemption.Reference == reference && redemption.Status == RedemptionStatus.Redeemed)
            .Select(redemption => redemption.PromotionId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        if (promotionIds.Count == 0)
        {
            return;
        }

        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var promotionId in promotionIds)
        {
            await LockAsync(promotionId, cancellationToken);

            // Đọc lại sau khi khóa: lần trả lượt song song cho cùng đơn đã commit thì không còn dòng nào để trả.
            var redemptions = await dbContext.Redemptions
                .Where(redemption => redemption.Reference == reference
                    && redemption.PromotionId == promotionId
                    && redemption.Status == RedemptionStatus.Redeemed)
                .ToListAsync(cancellationToken);
            if (redemptions.Count == 0)
            {
                continue;
            }

            // IgnoreQueryFilters: chương trình đã xóa mềm vẫn phải được trả lượt cho đúng số liệu.
            var promotion = await dbContext.Promotions.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == promotionId, cancellationToken);
            foreach (var redemption in redemptions)
            {
                redemption.Release(now);
                promotion.DecrementUsage();
                if (redemption.CouponId is { } couponId)
                {
                    (await dbContext.Coupons.SingleAsync(item => item.Id == couponId, cancellationToken))
                        .DecrementUsage();
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Released promotions of order {OrderCode}", reference);
    }

    // Chỉ nạp combo có ít nhất một sách trong giỏ; Include lấy đủ các dòng của combo để biết giỏ có đủ bộ không.
    private async Task<List<Combo>> RunningCombosAsync(
        IReadOnlyList<PromotionLine> lines,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var skuIds = lines.Select(line => line.SkuId).ToList();
        return await dbContext.Combos.AsNoTracking()
            .Include(combo => combo.Items)
            .Where(Combo.RunningAt(now))
            .Where(combo => combo.Items.Any(item => skuIds.Contains(item.SkuId)))
            .ToListAsync(cancellationToken);
    }

    private async Task<PromotionCandidate> FindCouponCandidateAsync(string rawCode, CancellationToken cancellationToken)
    {
        var code = CouponCodes.Normalize(rawCode);
        var coupon = await dbContext.Coupons.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        var promotion = coupon is null
            ? null
            : await dbContext.Promotions.AsNoTracking().Include(item => item.Targets)
                .SingleOrDefaultAsync(item => item.Id == coupon.PromotionId, cancellationToken);

        // Mã tắt thủ công, chương trình đã xóa hoặc không phải loại dùng mã: coi như mã không tồn tại.
        if (coupon is not { IsActive: true } || promotion is not { Mode: PromotionMode.Coupon })
        {
            throw PromotionErrors.CouponInvalid();
        }

        return new PromotionCandidate(promotion, coupon);
    }

    private async Task<Dictionary<long, int>> CustomerUsageAsync(
        IEnumerable<PromotionCandidate> candidates,
        PromotionCustomer customer,
        CancellationToken cancellationToken)
    {
        var customerKey = PromotionCustomerKey.From(customer);
        var limitedIds = candidates
            .Where(candidate => candidate.Promotion.PerCustomerLimit is not null)
            .Select(candidate => candidate.Promotion.Id)
            .ToList();
        if (customerKey is null || limitedIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Redemptions.AsNoTracking()
            .Where(redemption => redemption.CustomerKey == customerKey
                && redemption.Status == RedemptionStatus.Redeemed
                && limitedIds.Contains(redemption.PromotionId))
            .GroupBy(redemption => redemption.PromotionId)
            .Select(group => new { PromotionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PromotionId, item => item.Count, cancellationToken);
    }

    // Tên bảng cố định (không lấy từ input); id truyền dạng tham số.
    private Task LockAsync(long promotionId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM promotion.promotions WHERE id = {promotionId} FOR UPDATE", cancellationToken);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
