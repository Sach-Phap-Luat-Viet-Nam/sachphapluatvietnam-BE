using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Auth.Domain;

/// <summary>Thông tin xuất hóa đơn công ty đã lưu của user (FR-07), một bộ cho mỗi user.</summary>
internal sealed class UserInvoiceInfo : IAuditable
{
    // Cùng giới hạn với thông tin hóa đơn trên đơn hàng.
    public const int CompanyNameMaxLength = 255;
    public const int TaxCodeMaxLength = 14;
    public const int AddressMaxLength = 500;
    public const int EmailMaxLength = 256;

    public UserInvoiceInfo(long userId, InvoiceDetails details)
    {
        UserId = userId;
        Update(details);
    }

    // EF Core
    private UserInvoiceInfo()
    {
    }

    public long UserId { get; private set; }

    public string CompanyName { get; private set; } = string.Empty;

    /// <summary>Mã số thuế: 10 số, chi nhánh thêm "-xxx".</summary>
    public string TaxCode { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    /// <summary>Email nhận hóa đơn điện tử.</summary>
    public string Email { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public void Update(InvoiceDetails details)
    {
        CompanyName = details.CompanyName.Trim();
        TaxCode = details.TaxCode.Trim();
        Address = details.Address.Trim();
        Email = details.Email.Trim();
    }
}

internal sealed record InvoiceDetails(string CompanyName, string TaxCode, string Address, string Email);
