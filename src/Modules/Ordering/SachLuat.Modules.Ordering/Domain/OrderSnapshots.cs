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
