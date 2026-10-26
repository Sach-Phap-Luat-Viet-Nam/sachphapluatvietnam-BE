namespace SachLuat.Modules.Ordering.Domain;

/// <summary>Snapshot địa chỉ lúc đặt (mục 10.3), lưu JSONB. Địa chỉ 2 cấp tỉnh/thành – phường/xã.</summary>
internal sealed class ShippingAddress
{
    public ShippingAddress(string province, string ward, string streetAddress)
    {
        Province = province;
        Ward = ward;
        StreetAddress = streetAddress;
    }

    // EF Core
    private ShippingAddress()
    {
    }

    public string Province { get; private set; } = string.Empty;

    public string Ward { get; private set; } = string.Empty;

    public string StreetAddress { get; private set; } = string.Empty;
}

/// <summary>Thông tin xuất hóa đơn công ty (FR-36), lưu JSONB.</summary>
internal sealed class InvoiceInfo
{
    public InvoiceInfo(string companyName, string taxCode, string address, string email)
    {
        CompanyName = companyName;
        TaxCode = taxCode;
        Address = address;
        Email = email;
    }

    // EF Core
    private InvoiceInfo()
    {
    }

    public string CompanyName { get; private set; } = string.Empty;

    public string TaxCode { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;
}

/// <summary>Chương trình khuyến mãi đã áp cho đơn (snapshot tên, mã, số tiền), lưu JSONB.</summary>
internal sealed class AppliedPromotionSnapshot
{
    public AppliedPromotionSnapshot(long promotionId, string name, string kind, string? couponCode, decimal amount)
    {
        PromotionId = promotionId;
        Name = name;
        Kind = kind;
        CouponCode = couponCode;
        Amount = amount;
    }

    // EF Core
    private AppliedPromotionSnapshot()
    {
    }

    public long PromotionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary><c>order_discount</c>, <c>free_shipping</c> hoặc <c>combo</c> (khi đó <see cref="PromotionId"/> là id combo).</summary>
    public string Kind { get; private set; } = string.Empty;

    public string? CouponCode { get; private set; }

    /// <summary>Số tiền giảm trên đơn hoặc trên phí ship, tùy <see cref="Kind"/>.</summary>
    public decimal Amount { get; private set; }
}

/// <summary>Các khoản giảm giá và phí ship của đơn, tính sẵn lúc đặt hàng.</summary>
/// <param name="Discount">Tổng giảm trên đơn; bằng tổng <c>discount_amount</c> các dòng.</param>
/// <param name="ShippingFee">Phí ship khách phải trả theo biểu giá, sau miễn phí theo ngưỡng, trước giảm giá ship.</param>
/// <param name="ShippingBaseFee">Phí theo biểu giá trước khi miễn phí theo ngưỡng (báo cáo phần doanh nghiệp bù).</param>
/// <param name="ShippingDiscount">Giảm trên phí ship từ chương trình khuyến mãi.</param>
internal sealed record OrderCharges(
    decimal Discount,
    decimal ShippingFee,
    decimal ShippingBaseFee,
    decimal ShippingDiscount,
    IReadOnlyList<AppliedPromotionSnapshot> AppliedPromotions)
{
    public static OrderCharges Shipping(decimal shippingFee) => new(0m, shippingFee, shippingFee, 0m, []);
}
