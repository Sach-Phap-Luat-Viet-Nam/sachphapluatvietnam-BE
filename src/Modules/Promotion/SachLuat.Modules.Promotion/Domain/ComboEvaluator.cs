using SachLuat.Modules.Promotion.Contracts;

namespace SachLuat.Modules.Promotion.Domain;

/// <param name="Sets">Số bộ của combo có trong giỏ.</param>
/// <param name="Amount">
/// Tổng tiền giảm của tất cả các bộ (không phải một bộ), so với giá niêm yết cho sách được phủ trọn.
/// </param>
internal sealed record ComboMatch(Combo Combo, int Sets, decimal Amount);

/// <param name="Amount">Tổng tiền giảm combo hiển thị cho khách: giảm theo giá hiện hành + <paramref name="ListMarkups"/>.</param>
/// <param name="LineDiscounts">
/// Phần giảm theo <b>giá hiện hành</b> trên từng SKU (<c>tổng giá hiện hành − giá combo</c> của các bộ): đây là phần
/// làm giảm số tiền khách trả, dùng để tính tiền còn lại cho chương trình giảm trên đơn và trần tổng mức giảm.
/// </param>
/// <param name="ListMarkups">
/// Chênh lệch <c>giá niêm yết − giá hiện hành</c> của các cuốn được combo phủ trọn: nâng tiền dòng lên giá niêm yết và
/// cộng vào tiền giảm combo nên số khách phải trả không đổi.
/// </param>
internal sealed record ComboEvaluation(
    decimal Amount,
    IReadOnlyDictionary<long, decimal> LineDiscounts,
    IReadOnlyDictionary<long, decimal> ListMarkups,
    IReadOnlyList<ComboMatch> Matches)
{
    public static ComboEvaluation None { get; } = new(0m, new Dictionary<long, decimal>(), new Dictionary<long, decimal>(), []);
}

/// <summary>
/// Phần thuần của combo (không truy cập CSDL): nhận diện các bộ sách đủ trong giỏ và tính tiền giảm. Số tiền luôn là
/// VNĐ nguyên.
/// </summary>
internal static class ComboEvaluator
{
    /// <summary>
    /// Combo áp được khi <b>giá combo không cao hơn tổng giá hiện hành</b> (khách không bao giờ trả nhiều hơn mua lẻ).
    /// Tiền giảm hiển thị là <c>tổng giá niêm yết − giá combo</c> của các bộ: phần <c>tổng giá hiện hành − giá combo</c>
    /// là giảm thật, phần chênh <c>niêm yết − hiện hành</c> (chỉ với cuốn được bộ phủ trọn, cuốn dư lẻ thì không) được
    /// trả riêng ở <see cref="ComboEvaluation.ListMarkups"/>. Nhiều combo chung SKU thì xét tham lam: combo giảm nhiều
    /// mỗi bộ (theo giá hiện hành) được lấy sách trước, hòa thì combo tạo sớm hơn. Tiền giảm được phân bổ về từng SKU
    /// theo tỷ lệ tiền của phần sách thuộc combo.
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
        var listPrices = new Dictionary<long, decimal>();
        foreach (var line in lines)
        {
            remaining[line.SkuId] = remaining.GetValueOrDefault(line.SkuId) + line.Quantity;
            prices[line.SkuId] = line.UnitPrice;
            // Giá niêm yết không bao giờ thấp hơn giá hiện hành; không khai báo thì coi như bằng giá hiện hành.
            listPrices[line.SkuId] = Math.Max(line.ListPrice ?? line.UnitPrice, line.UnitPrice);
        }

        var candidates = combos
            .Where(combo => combo.IsRunningAt(now) && combo.Items.All(item => prices.ContainsKey(item.SkuId)))
            .Select(combo => (
                Combo: combo,
                PerSet: RetailPrice(combo, prices) - combo.ComboPrice,
                PerSetMarkup: ListRetailPrice(combo, listPrices) - RetailPrice(combo, prices)))
            // Giá combo ≤ tổng giá hiện hành, và có gì để giảm (giảm thật hoặc chênh lệch niêm yết).
            .Where(candidate => candidate.PerSet >= 0 && candidate.PerSet + candidate.PerSetMarkup > 0)
            .OrderByDescending(candidate => candidate.PerSet)
            .ThenBy(candidate => candidate.Combo.Id);

        // Bước 1: chọn bộ tham lam, trừ dần số cuốn còn lại.
        var picks = new List<(Combo Combo, int Sets, decimal PerSet)>();
        foreach (var (combo, perSet, _) in candidates)
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

            picks.Add((combo, sets, perSet));
        }

        // Bước 2: tiền giảm. Chỉ cuốn được bộ phủ trọn (không còn cuốn lẻ dư) mới tính theo giá niêm yết, vì tiền dòng
        // của SKU đó chuyển hẳn sang giá niêm yết; cuốn dư lẻ giữ giá hiện hành nên không có phần chênh.
        var matches = new List<ComboMatch>();
        var lineDiscounts = new Dictionary<long, decimal>();
        var listMarkups = new Dictionary<long, decimal>();
        foreach (var (combo, sets, perSet) in picks)
        {
            var currentAmount = perSet * sets;
            var markups = combo.Items
                .Where(item => remaining[item.SkuId] == 0)
                .Select(item => (item.SkuId, Markup: (listPrices[item.SkuId] - prices[item.SkuId]) * item.Quantity * sets))
                .Where(entry => entry.Markup > 0)
                .ToList();
            var amount = currentAmount + markups.Sum(entry => entry.Markup);
            if (amount <= 0)
            {
                continue;
            }

            matches.Add(new ComboMatch(combo, sets, amount));
            foreach (var (skuId, markup) in markups)
            {
                listMarkups[skuId] = listMarkups.GetValueOrDefault(skuId) + markup;
            }

            var shares = PromotionEvaluator.Allocate(
                [.. combo.Items.Select(item => (item.SkuId, prices[item.SkuId] * item.Quantity * sets))],
                currentAmount);
            foreach (var (skuId, share) in shares)
            {
                lineDiscounts[skuId] = lineDiscounts.GetValueOrDefault(skuId) + share;
            }
        }

        return matches.Count == 0
            ? ComboEvaluation.None
            : new ComboEvaluation(matches.Sum(match => match.Amount), lineDiscounts, listMarkups, matches);
    }

    private static decimal RetailPrice(Combo combo, IReadOnlyDictionary<long, decimal> prices) =>
        combo.Items.Sum(item => prices[item.SkuId] * item.Quantity);

    private static decimal ListRetailPrice(Combo combo, IReadOnlyDictionary<long, decimal> listPrices) =>
        combo.Items.Sum(item => listPrices[item.SkuId] * item.Quantity);
}
