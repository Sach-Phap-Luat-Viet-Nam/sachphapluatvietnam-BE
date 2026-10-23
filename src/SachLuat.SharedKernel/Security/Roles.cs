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

    /// <summary>Vai trò nhân viên: bắt buộc 2FA (FR-97, NFR-15).</summary>
    public static readonly IReadOnlySet<string> Staff = new HashSet<string>(StringComparer.Ordinal)
    {
        SuperAdmin, ProductManager, OrderManager, WarehouseKeeper, CustomerService, Marketing, Accountant,
    };

    public static readonly IReadOnlyList<string> All = [.. Staff, Customer];
}
