using SachLuat.Modules.Promotion.Contracts;

namespace SachLuat.Modules.Promotion.Domain;

/// <param name="Sets">Số bộ của combo có trong giỏ.</param>
/// <param name="Amount">Tổng tiền giảm của tất cả các bộ (không phải một bộ).</param>
internal sealed record ComboMatch(Combo Combo, int Sets, decimal Amount);

internal sealed record ComboEvaluation(
    decimal Amount,
    IReadOnlyDictionary<long, decimal> LineDiscounts,
    IReadOnlyList<ComboMatch> Matches)
{
    public static ComboEvaluation None { get; } = new(0m, new Dictionary<long, decimal>(), []);
}

/// <summary>
/// Phần thuần của combo (không truy cập CSDL): nhận diện các bộ sách đủ trong giỏ và tính tiền giảm so với giá lẻ
/// hiện hành. Số tiền luôn là VNĐ nguyên.
/// </summary>
internal static class ComboEvaluator
{
    /// <summary>
    /// Mỗi bộ giảm <c>tổng giá lẻ hiện hành − giá combo</c>; combo không còn lợi (giá lẻ đang sale thấp hơn hoặc
    /// bằng giá combo) thì bỏ qua. Nhiều combo chung SKU thì xét tham lam: combo giảm nhiều mỗi bộ được lấy sách
    /// trước, hòa thì combo tạo sớm hơn. Tiền giảm được phân bổ về từng SKU theo tỷ lệ tiền của phần sách thuộc combo.
    /// </summary>
    public static ComboEvaluation Evaluate(
        IReadOnlyCollection<Combo> combos,
        IReadOnlyList<PromotionLine> lines,
        DateTime now)
    {
        if (combos.Count == 0)
        {
            return ComboEvaluation.None;
        }

        var remaining = new Dictionary<long, int>();
        var prices = new Dictionary<long, decimal>();
        foreach (var line in lines)
        {
            remaining[line.SkuId] = remaining.GetValueOrDefault(line.SkuId) + line.Quantity;
            prices[line.SkuId] = line.UnitPrice;
        }

        var candidates = combos
            .Where(combo => combo.IsRunningAt(now) && combo.Items.All(item => prices.ContainsKey(item.SkuId)))
            .Select(combo => (Combo: combo, PerSet: RetailPrice(combo, prices) - combo.ComboPrice))
            .Where(candidate => candidate.PerSet > 0)
            .OrderByDescending(candidate => candidate.PerSet)
            .ThenBy(candidate => candidate.Combo.Id);

        var matches = new List<ComboMatch>();
        var lineDiscounts = new Dictionary<long, decimal>();
        foreach (var (combo, perSet) in candidates)
        {
            var sets = combo.Items.Min(item => remaining[item.SkuId] / item.Quantity);
            if (sets == 0)
            {
                continue;
            }

            foreach (var item in combo.Items)
            {
                remaining[item.SkuId] -= item.Quantity * sets;
            }

            var amount = perSet * sets;
            matches.Add(new ComboMatch(combo, sets, amount));

            var shares = PromotionEvaluator.Allocate(
                [.. combo.Items.Select(item => (item.SkuId, prices[item.SkuId] * item.Quantity * sets))],
                amount);
            foreach (var (skuId, share) in shares)
            {
                lineDiscounts[skuId] = lineDiscounts.GetValueOrDefault(skuId) + share;
            }
        }

        return matches.Count == 0
            ? ComboEvaluation.None
            : new ComboEvaluation(matches.Sum(match => match.Amount), lineDiscounts, matches);
    }

    private static decimal RetailPrice(Combo combo, IReadOnlyDictionary<long, decimal> prices) =>
        combo.Items.Sum(item => prices[item.SkuId] * item.Quantity);
}
