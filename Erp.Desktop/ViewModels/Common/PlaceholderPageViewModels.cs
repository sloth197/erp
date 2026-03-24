using CommunityToolkit.Mvvm.ComponentModel;
using Erp.Application.Authorization;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public abstract partial class PlaceholderPageViewModel : ObservableObject
{
    protected PlaceholderPageViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;
}

[RequiredPermission(PermissionCodes.MasterPartnersRead)]
public sealed class PartnersViewModel : PlaceholderPageViewModel
{
    public PartnersViewModel() : base("거래처 관리", "고객/공급처 기준정보 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.MasterItemsRead)]
public sealed class WarehousesViewModel : PlaceholderPageViewModel
{
    public WarehousesViewModel() : base("창고 관리", "창고 기준정보 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.PurchaseOrdersRead)]
public sealed class PurchaseOrdersViewModel : PlaceholderPageViewModel
{
    public PurchaseOrdersViewModel() : base("발주", "발주 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersRead)]
public sealed class SalesOrdersViewModel : PlaceholderPageViewModel
{
    public SalesOrdersViewModel() : base("주문", "판매 주문 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersWrite)]
public sealed class SalesRevenueViewModel : PlaceholderPageViewModel
{
    public SalesRevenueViewModel() : base("출고", "판매 출고 처리 화면(placeholder)")
    {
    }
}

