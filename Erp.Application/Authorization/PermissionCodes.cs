namespace Erp.Application.Authorization;

public static class PermissionCodes
{
    public const string SystemSettingsRead = "System.Settings.Read";
    public const string SystemSettingsWrite = "System.Settings.Write";

    public const string MasterUsersRead = "Master.Users.Read";
    public const string MasterUsersWrite = "Master.Users.Write";
    public const string MasterItemsRead = "Master.Items.Read";
    public const string MasterItemsWrite = "Master.Items.Write";
    public const string MasterItemsExport = "Master.Items.Export";
    public const string MasterPartnersRead = "Master.Partners.Read";
    public const string MasterPartnersWrite = "Master.Partners.Write";

    public const string InventoryStockRead = "Inventory.Stock.Read";
    public const string InventoryStockWrite = "Inventory.Stock.Write";

    public const string PurchaseOrdersRead = "Purchase.Orders.Read";
    public const string PurchaseOrdersWrite = "Purchase.Orders.Write";

    public const string SalesOrdersRead = "Sales.Orders.Read";
    public const string SalesOrdersWrite = "Sales.Orders.Write";

    public const string AuditRead = "Audit.Read";

    public static readonly IReadOnlyList<string> All =
    [
        SystemSettingsRead,
        SystemSettingsWrite,
        MasterUsersRead,
        MasterUsersWrite,
        MasterItemsRead,
        MasterItemsWrite,
        MasterItemsExport,
        MasterPartnersRead,
        MasterPartnersWrite,
        InventoryStockRead,
        InventoryStockWrite,
        PurchaseOrdersRead,
        PurchaseOrdersWrite,
        SalesOrdersRead,
        SalesOrdersWrite,
        AuditRead
    ];
}
