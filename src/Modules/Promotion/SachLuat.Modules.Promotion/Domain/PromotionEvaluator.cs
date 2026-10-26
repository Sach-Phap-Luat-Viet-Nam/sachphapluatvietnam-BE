using SachLuat.Modules.Promotion.Contracts;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Promotion.Domain;

/// <summary>Chương trình cần xét; <see cref="Coupon"/> khác null khi khách nhập mã của chương trình này.</summary>
internal sealed record PromotionCandidate(PromotionCampaign Promotion, Coupon? Coupon);

/// <summary>
/// Phần thuần của khuyến mãi (không truy cập CSDL): chọn chương trình lợi nhất cho khách, tính số tiền giảm và
/// phân bổ về từng dòng đơn. Số tiền luôn là VNĐ nguyên, làm tròn xuống.
/// </summary>
internal static class PromotionEvaluator
{
    /// <param name="candidates">Chương trình tự áp đang chạy, cộng thêm chương trình của mã khách nhập (nếu có).</param>
    /// <param name="customerUsage">Số lượt khách này đã dùng theo mã chương trình (chỉ cần với chương trình có giới hạn).</param>
    /// <param name="combos">
    /// Combo đang chạy có sách trong giỏ. Combo tính trước; chương trình giảm trên đơn **cộng dồn** với combo và tính
    /// trên số tiền còn lại của từng dòng sau khi trừ phần giảm của combo (điều kiện đơn tối thiểu vẫn xét trên tạm
    /// tính trước giảm giá). Giảm phí ship áp độc lập.
    /// </param>
    /// <param name="maxTotalDiscountPercent">
    /// Trần tổng mức giảm (combo + chương trình) trên giá <b>hiện hành</b> của dòng có combo, theo phần trăm; chỉ chặn
    /// phần giảm của chương trình, không chặn combo. Phần giảm combo được tính theo giá hiện hành (không gồm phần chênh
    /// niêm yết). 100 là không chặn thêm.
    /// </param>
    public static PromotionEvaluation Evaluate(
        IReadOnlyCollection<PromotionCandidate> candidates,
        PromotionRequest request,
        IReadOnlyDictionary<long, int> customerUsage,
        DateTime now,
        IReadOnlyCollection<Combo>? combos = null,
        int maxTotalDiscountPercent = 100)
    {
        var bundle = ComboEvaluator.Evaluate(combos ?? [], request.Lines, now);

        var outcomes = new List<Outcome>();
        foreach (var candidate in candidates)
        {
            var used = customerUsage.GetValueOrDefault(candidate.Promotion.Id);
            var (eligibility, outcome) = Assess(
                candidate, request, used, now, bundle.LineDiscounts, maxTotalDiscountPercent);

            if (eligibility == Eligibility.Eligible && outcome is not null)
            {
                outcomes.Add(outcome);
            }
            else if (candidate.Coupon is not null)
            {
                // Khách chủ động nhập mã nên phải biết vì sao mã không dùng được; chương trình tự áp thì bỏ qua êm.
                throw ToError(eligibility, candidate.Promotion);
            }
        }

        var order = Best(outcomes, PromotionKind.OrderDiscount);
        var shipping = Best(outcomes, PromotionKind.FreeShipping);

        // Phần giảm của combo và của chương trình cộng lại; mỗi dòng không bao giờ bị giảm quá tiền của dòng đó vì
        // chương trình chỉ tính trên phần còn lại sau combo.
        // Phần giảm theo giá hiện hành của combo + phần chênh niêm yết (để dòng hiện theo giá niêm yết vẫn khớp).
        var lineDiscounts = new Dictionary<long, decimal>(bundle.LineDiscounts);
        foreach (var (skuId, markup) in bundle.ListMarkups)
        {
            lineDiscounts[skuId] = lineDiscounts.GetValueOrDefault(skuId) + markup;
        }

        foreach (var (skuId, amount) in order?.LineDiscounts ?? [])
        {
            lineDiscounts[skuId] = lineDiscounts.GetValueOrDefault(skuId) + amount;
        }

        var applied = new[] { order, shipping }.OfType<Outcome>().ToList();

        return new PromotionEvaluation(
            bundle.Amount + (order?.Amount ?? 0m),
            shipping?.Amount ?? 0m,
            lineDiscounts,
            [
                .. bundle.Matches.Select(match => new AppliedPromotion(
                    match.Combo.Id, match.Combo.Name, PromotionKind.Combo, null, null, match.Amount)),
                .. applied.Select(outcome => new AppliedPromotion(
                    outcome.Promotion.Id,
                    outcome.Promotion.Name,
                    outcome.Promotion.Kind,
                    outcome.Coupon?.Id,
                    outcome.Coupon?.Code,
                    outcome.Amount)),
            ],
            applied.Any(outcome => outcome.Coupon is not null))
        {
            ListMarkups = bundle.ListMarkups,
        };
    }

    /// <summary>
    /// Phân bổ <paramref name="discount"/> theo tỷ lệ tiền từng dòng, làm tròn xuống; phần dư (vài đồng) cộng lần
    /// lượt cho dòng có tiền lớn nhất để tổng đúng bằng <paramref name="discount"/>.
    /// </summary>
    public static Dictionary<long, decimal> Allocate(IReadOnlyList<(long SkuId, decimal Total)> lines, decimal discount)
    {
        var result = lines.ToDictionary(line => line.SkuId, _ => 0m);
        var total = lines.Sum(line => line.Total);
        if (discount <= 0 || total <= 0)
        {
            return result;
        }

        foreach (var (skuId, lineTotal) in lines)
        {
            result[skuId] = Math.Floor(discount * lineTotal / total);
        }

        var remainder = discount - result.Values.Sum();
        foreach (var (skuId, _) in lines.OrderByDescending(line => line.Total).ThenBy(line => line.SkuId))
        {
            if (remainder <= 0)
            {
                break;
            }

            result[skuId]++;
            remainder--;
        }

        return result;
    }

    internal static decimal ComputeAmount(PromotionCampaign promotion, decimal basis)
    {
        var amount = promotion.DiscountType == DiscountType.Percent
            ? Math.Floor(basis * promotion.DiscountValue / 100m)
            : promotion.DiscountValue;
        if (promotion.MaxDiscountAmount is { } cap)
        {
            amount = Math.Min(amount, cap);
        }

        return Math.Min(amount, basis);
    }

    private static (Eligibility Eligibility, Outcome? Outcome) Assess(
        PromotionCandidate candidate,
        PromotionRequest request,
        int customerUsed,
        DateTime now,
        IReadOnlyDictionary<long, decimal> comboDiscounts,
        int maxTotalDiscountPercent)
    {
        var promotion = candidate.Promotion;
        var coupon = candidate.Coupon;

        if (promotion.StartsAt > now)
        {
            return (Eligibility.NotStarted, null);
        }

        if (!promotion.IsActive || promotion.EndsAt <= now || promotion.UsageLimit <= promotion.UsedCount
            || coupon is { IsActive: false } || coupon?.IsExhausted == true)
        {
            return (Eligibility.Expired, null);
        }

        if (promotion.Audience == PromotionAudience.MembersOnly && request.Customer.UserId is null)
        {
            return (Eligibility.LoginRequired, null);
        }

        if (promotion.PerCustomerLimit <= customerUsed)
        {
            return (Eligibility.CustomerLimitReached, null);
        }

        var subtotal = request.Lines.Sum(line => line.UnitPrice * line.Quantity);
        if (subtotal < promotion.MinOrderAmount)
        {
            return (Eligibility.MinOrderNotMet, null);
        }

        if (promotion.Kind == PromotionKind.FreeShipping)
        {
            var shippingDiscount = ComputeAmount(promotion, request.ShippingFee);
            return shippingDiscount > 0
                ? (Eligibility.Eligible, new Outcome(promotion, coupon, shippingDiscount, null))
                : (Eligibility.NotApplicable, null);
        }

        // Tính trên tiền còn lại sau combo. `Room` là phần chương trình được giảm thêm trên dòng đó: toàn bộ tiền còn
        // lại, trừ dòng có combo thì bị chặn bởi trần tổng mức giảm.
        var eligibleLines = request.Lines
            .Where(line => promotion.CoversLine(line) && !(promotion.ExcludeOnSaleItems && line.IsOnSale))
            .Select(line =>
            {
                var comboDiscount = comboDiscounts.GetValueOrDefault(line.SkuId);
                var payable = (line.UnitPrice * line.Quantity) - comboDiscount;
                var room = Room(line, payable, comboDiscount, maxTotalDiscountPercent);
                return (line.SkuId, Payable: payable, Room: room);
            })
            .ToList();
        var discount = Math.Min(
            ComputeAmount(promotion, eligibleLines.Sum(line => line.Payable)),
            eligibleLines.Sum(line => line.Room));

        // Phân bổ theo `Room` để không dòng nào vượt phần được giảm thêm; không có combo thì `Room` là tiền của dòng.
        return discount > 0
            ? (Eligibility.Eligible, new Outcome(
                promotion,
                coupon,
                discount,
                Allocate([.. eligibleLines.Select(line => (line.SkuId, line.Room))], discount)))
            : (Eligibility.NotApplicable, null);
    }

    /// <summary>
    /// Phần chương trình giảm trên đơn còn được giảm thêm trên một dòng. Dòng không có combo: toàn bộ tiền của dòng như
    /// trước đây. Dòng có combo: tổng giảm (combo + chương trình) không vượt
    /// <paramref name="maxTotalDiscountPercent"/> của giá hiện hành.
    /// </summary>
    private static decimal Room(PromotionLine line, decimal payable, decimal comboDiscount, int maxTotalDiscountPercent)
    {
        if (comboDiscount <= 0)
        {
            return payable;
        }

        var currentTotal = line.UnitPrice * line.Quantity;
        var allowance = Math.Floor(currentTotal * maxTotalDiscountPercent / 100m) - comboDiscount;
        return Math.Clamp(allowance, 0m, payable);
    }

    // Lợi nhất cho khách trước; hòa thì ưu tiên mã khách chủ động nhập, rồi độ ưu tiên, rồi chương trình cũ hơn.
    private static Outcome? Best(IEnumerable<Outcome> outcomes, PromotionKind kind) => outcomes
        .Where(outcome => outcome.Promotion.Kind == kind)
        .OrderByDescending(outcome => outcome.Amount)
        .ThenByDescending(outcome => outcome.Coupon is not null)
        .ThenByDescending(outcome => outcome.Promotion.Priority)
        .ThenBy(outcome => outcome.Promotion.Id)
        .FirstOrDefault();

    private static BusinessException ToError(Eligibility eligibility, PromotionCampaign promotion) =>
        eligibility switch
        {
            Eligibility.NotStarted => PromotionErrors.CouponInvalid(),
            Eligibility.Expired => PromotionErrors.CouponExpired(),
            Eligibility.CustomerLimitReached => PromotionErrors.CouponCustomerLimit(),
            Eligibility.LoginRequired => PromotionErrors.CouponLoginRequired(),
            Eligibility.MinOrderNotMet => PromotionErrors.CouponMinOrder(promotion.MinOrderAmount),
            _ => PromotionErrors.CouponNotApplicable(),
        };

    private enum Eligibility
    {
        Eligible,
        NotStarted,
        Expired,
        LoginRequired,
        CustomerLimitReached,
        MinOrderNotMet,
        NotApplicable,
    }

    private sealed record Outcome(
        PromotionCampaign Promotion,
        Coupon? Coupon,
        decimal Amount,
        Dictionary<long, decimal>? LineDiscounts);
}
