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

public sealed class NoticesViewModel : PlaceholderPageViewModel
{
    public NoticesViewModel() : base("알림/공지", "알림/공지 모듈은 다음 단계에서 구현됩니다.")
    {
    }
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
    public WarehousesViewModel() : base("창고/로케이션", "창고/로케이션 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.MasterPartnersRead)]
public sealed class CodesViewModel : PlaceholderPageViewModel
{
    public CodesViewModel() : base("코드관리", "단위/세금/결제조건 코드관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.InventoryStockRead)]
public sealed class InventoryStockViewModel : PlaceholderPageViewModel
{
    public InventoryStockViewModel() : base("재고조회", "재고현황 조회 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.InventoryStockReceipt)]
public sealed class InventoryInOutViewModel : PlaceholderPageViewModel
{
    public InventoryInOutViewModel() : base("입출고", "입출고 처리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.InventoryStockAdjust)]
public sealed class InventoryAdjustmentViewModel : PlaceholderPageViewModel
{
    public InventoryAdjustmentViewModel() : base("재고조정", "재고조정 처리 화면(placeholder)")
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

[RequiredPermission(PermissionCodes.PurchaseOrdersWrite)]
public sealed class PurchaseReceiptViewModel : PlaceholderPageViewModel
{
    public PurchaseReceiptViewModel() : base("입고", "매입 입고 처리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersRead)]
public sealed class SalesOrdersViewModel : PlaceholderPageViewModel
{
    public SalesOrdersViewModel() : base("견적/주문", "견적/주문 관리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersWrite)]
public sealed class SalesRevenueViewModel : PlaceholderPageViewModel
{
    public SalesRevenueViewModel() : base("출고/매출", "출고/매출 처리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersRead)]
public sealed class AccountVouchersViewModel : PlaceholderPageViewModel
{
    public AccountVouchersViewModel() : base("매입/매출 전표", "회계 전표 처리 화면(placeholder)")
    {
    }
}

[RequiredPermission(PermissionCodes.SalesOrdersRead)]
public sealed class AccountReportsViewModel : PlaceholderPageViewModel
{
    public AccountReportsViewModel() : base("간단 리포트", "회계 리포트 화면(placeholder)")
    {
    }
}

