namespace SachLuat.Modules.Shipping.Contracts;

/// <summary>Báo giá phí vận chuyển cho module khác (đơn hàng, xem trước thanh toán).</summary>
public interface IShippingQuoteService
{
    /// <summary>
    /// Tính phí ship theo tỉnh/thành, trọng lượng và tạm tính (trước giảm giá).
    /// <paramref name="province"/> trống thì dùng phí cố định. Tỉnh chưa được hỗ trợ ném
    /// <c>BusinessException</c> với mã <c>ADDRESS_NOT_SUPPORTED</c>.
    /// </summary>
    Task<ShippingQuote> QuoteAsync(
        string? province,
        IReadOnlyCollection<ShippingItem> items,
        decimal subtotal,
        CancellationToken cancellationToken);
}

/// <param name="WeightGram">Trọng lượng một cuốn; null thì dùng trọng lượng mặc định trong cấu hình.</param>
public sealed record ShippingItem(int? WeightGram, int Quantity);

/// <param name="Fee">Phí khách phải trả (0 khi đạt ngưỡng miễn phí).</param>
/// <param name="BaseFee">Phí theo biểu giá trước khi miễn phí theo ngưỡng; dùng báo cáo phần doanh nghiệp bù.</param>
/// <param name="FreeShippingThreshold">null: không miễn phí theo ngưỡng.</param>
/// <param name="AmountToFreeShipping">Số tiền còn thiếu để được miễn phí; 0 khi đã đạt hoặc không có ngưỡng.</param>
public sealed record ShippingQuote(
    decimal Fee,
    decimal BaseFee,
    bool IsFreeByThreshold,
    decimal? FreeShippingThreshold,
    decimal AmountToFreeShipping,
    int TotalWeightGram,
    string? ZoneName,
    int? EtaMinDays,
    int? EtaMaxDays);
