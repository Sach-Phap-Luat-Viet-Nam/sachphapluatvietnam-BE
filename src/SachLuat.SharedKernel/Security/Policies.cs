namespace SachLuat.SharedKernel.Security;

/// <summary>
/// Tên policy dùng trong <c>[Authorize(Policy = ...)]</c>. Mọi policy Admin yêu cầu đăng nhập có 2FA
/// (claim <see cref="AuthClaimTypes.AuthenticationMethod"/> = <see cref="AuthClaimTypes.MultiFactor"/>).
/// Đăng ký policy ở module Auth.
/// </summary>
public static class Policies
{
    public const string CatalogAdmin = "catalog_admin";
    public const string InventoryAdmin = "inventory_admin";
    public const string InventoryApprove = "inventory_approve";
    public const string OrdersRead = "orders_read";
    public const string OrdersManage = "orders_manage";
    public const string UsersAdmin = "users_admin";

    public static readonly IReadOnlyDictionary<string, string[]> StaffRoles = new Dictionary<string, string[]>
    {
        [CatalogAdmin] = [Roles.SuperAdmin, Roles.ProductManager],
        [InventoryAdmin] = [Roles.SuperAdmin, Roles.WarehouseKeeper],
        [InventoryApprove] = [Roles.SuperAdmin],
        [OrdersRead] = [Roles.SuperAdmin, Roles.OrderManager, Roles.CustomerService, Roles.Accountant],
        [OrdersManage] = [Roles.SuperAdmin, Roles.OrderManager],
        [UsersAdmin] = [Roles.SuperAdmin],
    };
}
