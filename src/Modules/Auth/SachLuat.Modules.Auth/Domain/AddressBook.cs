namespace SachLuat.Modules.Auth.Domain;

/// <summary>
/// Sổ địa chỉ của một user (FR-06): tối đa <see cref="MaxAddresses"/> địa chỉ, sổ không rỗng thì luôn có đúng một
/// địa chỉ mặc định. Tạo từ toàn bộ địa chỉ của user đã nạp (và khóa) trong cùng transaction.
/// </summary>
internal sealed class AddressBook(long userId, IEnumerable<UserAddress> addresses)
{
    public const int MaxAddresses = 10;

    private readonly List<UserAddress> _addresses = [.. addresses];

    public IReadOnlyList<UserAddress> Addresses => _addresses;

    public UserAddress Find(long addressId) =>
        _addresses.SingleOrDefault(address => address.Id == addressId) ?? throw AuthErrors.AddressNotFound();

    /// <summary>Thêm địa chỉ; địa chỉ đầu tiên luôn là mặc định.</summary>
    public UserAddress Add(AddressDetails details, bool makeDefault)
    {
        if (_addresses.Count >= MaxAddresses)
        {
            throw AuthErrors.AddressLimitReached(MaxAddresses);
        }

        var address = new UserAddress(userId, details);
        _addresses.Add(address);
        if (makeDefault || _addresses.Count == 1)
        {
            MakeDefault(address);
        }

        return address;
    }

    /// <remarks>
    /// <paramref name="makeDefault"/> = false không bỏ cờ mặc định: muốn đổi mặc định thì đặt địa chỉ khác làm
    /// mặc định, để sổ không bao giờ thiếu địa chỉ mặc định.
    /// </remarks>
    public void Update(UserAddress address, AddressDetails details, bool makeDefault)
    {
        EnsureOwned(address);
        address.Update(details);
        if (makeDefault)
        {
            MakeDefault(address);
        }
    }

    /// <summary>Xóa địa chỉ; xóa địa chỉ mặc định thì địa chỉ cập nhật gần nhất còn lại thành mặc định.</summary>
    public void Remove(UserAddress address)
    {
        EnsureOwned(address);
        _addresses.Remove(address);
        if (address.IsDefault && _addresses.Count > 0)
        {
            MakeDefault(_addresses
                .OrderByDescending(other => other.UpdatedAt)
                .ThenByDescending(other => other.Id)
                .First());
        }
    }

    public void MakeDefault(UserAddress address)
    {
        EnsureOwned(address);
        foreach (var other in _addresses)
        {
            other.SetDefault(ReferenceEquals(other, address));
        }
    }

    private void EnsureOwned(UserAddress address)
    {
        if (!_addresses.Contains(address))
        {
            throw AuthErrors.AddressNotFound();
        }
    }
}
