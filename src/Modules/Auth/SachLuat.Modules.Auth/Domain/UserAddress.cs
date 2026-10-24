using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Auth.Domain;

/// <summary>Một địa chỉ giao hàng trong sổ địa chỉ của user (FR-06). Tạo/sửa qua <see cref="AddressBook"/>.</summary>
internal sealed class UserAddress : AuditableEntity
{
    // Cùng giới hạn với địa chỉ trên đơn hàng, để địa chỉ đã lưu điền vào form đặt hàng luôn hợp lệ.
    public const int RecipientNameMaxLength = 255;
    public const int PhoneMaxLength = 20;
    public const int ProvinceMaxLength = 100;
    public const int WardMaxLength = 100;
    public const int StreetAddressMaxLength = 500;

    internal UserAddress(long userId, AddressDetails details)
    {
        UserId = userId;
        Update(details);
    }

    // EF Core
    private UserAddress()
    {
    }

    public long UserId { get; private set; }

    public string RecipientName { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    /// <summary>Tỉnh/thành phố.</summary>
    public string Province { get; private set; } = string.Empty;

    /// <summary>Phường/xã.</summary>
    public string Ward { get; private set; } = string.Empty;

    /// <summary>Số nhà, tên đường.</summary>
    public string StreetAddress { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    internal void Update(AddressDetails details)
    {
        RecipientName = details.RecipientName.Trim();
        Phone = details.Phone.Trim();
        Province = details.Province.Trim();
        Ward = details.Ward.Trim();
        StreetAddress = details.StreetAddress.Trim();
    }

    internal void SetDefault(bool isDefault) => IsDefault = isDefault;
}

internal sealed record AddressDetails(
    string RecipientName,
    string Phone,
    string Province,
    string Ward,
    string StreetAddress);
