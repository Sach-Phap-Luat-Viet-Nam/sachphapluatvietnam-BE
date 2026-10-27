namespace SachLuat.SharedKernel.Security;

/// <summary>Vai trò theo bảng 4.2 của tài liệu dự án.</summary>
public static class Roles
{
    public const string SuperAdmin = "super_admin";
    public const string ProductManager = "product_manager";
    public const string OrderManager = "order_manager";
    public const string WarehouseKeeper = "warehouse_keeper";
    public const string CustomerService = "customer_service";
    public const string Marketing = "marketing";
    public const string Accountant = "accountant";
    public const string Customer = "customer";

    /// <summary>Vai trò nhân viên (dùng được app Admin).</summary>
    public static readonly IReadOnlySet<string> Staff = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin, ProductManager, OrderManager, WarehouseKeeper, CustomerService, Marketing, Accountant,
    };

    /// <summary>
    /// Vai trò bắt buộc bật 2FA mới dùng được API Admin. Nhân viên vai trò khác tự chọn bật hay không; ai đã bật thì
    /// đăng nhập vẫn phải nhập mã.
    /// </summary>
    public static readonly IReadOnlySet<string> TwoFactorRequired = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin,
    };

    public static readonly IReadOnlyList<string> All = [.. Staff, Customer];
}
