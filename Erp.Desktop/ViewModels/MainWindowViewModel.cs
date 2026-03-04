using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAuthService _authService;

    public MainWindowViewModel(
        INavigationService navigationService,
        ICurrentUserContext currentUserContext,
        IAuthService authService)
    {
        _navigationService = navigationService;
        _currentUserContext = currentUserContext;
        _authService = authService;

        _navigationService.PropertyChanged += OnNavigationPropertyChanged;
        _currentUserContext.Changed += OnCurrentUserChanged;

        BuildMenu();
        _navigationService.NavigateTo<LoginViewModel>();
    }

    public ObservableCollection<ShellMenuGroup> MenuGroups { get; } = new();

    public object? CurrentViewModel => _navigationService.CurrentViewModel;
    public bool IsAuthenticated => _currentUserContext.IsAuthenticated;
    public string CurrentUsername => _currentUserContext.Username ?? "Anonymous";

    [RelayCommand(CanExecute = nameof(CanGoHome))]
    private void GoHome()
    {
        _navigationService.NavigateTo<HomeViewModel>();
    }

    private bool CanGoHome()
    {
        return IsAuthenticated;
    }

    [RelayCommand(CanExecute = nameof(CanLogout))]
    private async Task LogoutAsync()
    {
        try
        {
            await _authService.LogoutAsync();
            _navigationService.NavigateTo<LoginViewModel>();
        }
        catch
        {
            // No-op: logout should not block returning to login screen.
            _navigationService.NavigateTo<LoginViewModel>();
        }
    }

    private bool CanLogout()
    {
        return IsAuthenticated;
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INavigationService.CurrentViewModel))
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(CurrentUsername));

        GoHomeCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();

        BuildMenu();
    }

    private void BuildMenu()
    {
        MenuGroups.Clear();

        if (!IsAuthenticated)
        {
            return;
        }

        AddGroup(
            "공통(Common)",
            new MenuEntry("대시보드(Home)", null, () => _navigationService.NavigateTo<HomeViewModel>()),
            new MenuEntry("알림/공지", null, () => _navigationService.NavigateTo<NoticesViewModel>()),
            new MenuEntry("비밀번호 변경", null, () => _navigationService.NavigateTo<ChangePasswordViewModel>()),
            new MenuEntry("로그아웃", null, () => LogoutCommand.Execute(null)));

        AddGroup(
            "기준정보(Master Data)",
            new MenuEntry("사용자/권한관리", PermissionCodes.MasterUsersRead, () => _navigationService.NavigateTo<UsersManagementViewModel>()),
            new MenuEntry("거래처 관리", PermissionCodes.MasterPartnersRead, () => _navigationService.NavigateTo<PartnersViewModel>()),
            new MenuEntry("품목 관리", PermissionCodes.MasterItemsRead, () => _navigationService.NavigateTo<ItemsViewModel>()),
            new MenuEntry("창고/로케이션 관리", PermissionCodes.MasterItemsRead, () => _navigationService.NavigateTo<WarehousesViewModel>()),
            new MenuEntry("코드관리", PermissionCodes.MasterPartnersRead, () => _navigationService.NavigateTo<CodesViewModel>()));

        AddGroup(
            "재고(Inventory)",
            new MenuEntry("재고조회", PermissionCodes.InventoryStockRead, () => _navigationService.NavigateTo<InventoryOnHandViewModel>()),
            new MenuEntry("입고 등록", PermissionCodes.InventoryStockWrite, () => _navigationService.NavigateTo<StockReceiptViewModel>()),
            new MenuEntry("출고 등록", PermissionCodes.InventoryStockWrite, () => _navigationService.NavigateTo<StockIssueViewModel>()),
            new MenuEntry("재고조정", PermissionCodes.InventoryStockWrite, () => _navigationService.NavigateTo<StockAdjustViewModel>()));

        AddGroup(
            "구매/매입(Purchase)",
            new MenuEntry("발주", PermissionCodes.PurchaseOrdersRead, () => _navigationService.NavigateTo<PurchaseOrdersViewModel>()),
            new MenuEntry("입고", PermissionCodes.PurchaseOrdersWrite, () => _navigationService.NavigateTo<PurchaseReceiptViewModel>()));

        AddGroup(
            "판매/매출(Sales)",
            new MenuEntry("견적/주문", PermissionCodes.SalesOrdersRead, () => _navigationService.NavigateTo<SalesOrdersViewModel>()),
            new MenuEntry("출고/매출", PermissionCodes.SalesOrdersWrite, () => _navigationService.NavigateTo<SalesRevenueViewModel>()));

        AddGroup(
            "회계(Accounts)",
            new MenuEntry("매입/매출 전표", PermissionCodes.SalesOrdersRead, () => _navigationService.NavigateTo<AccountVouchersViewModel>()),
            new MenuEntry("간단 리포트", PermissionCodes.SalesOrdersRead, () => _navigationService.NavigateTo<AccountReportsViewModel>()));

        AddGroup(
            "시스템(System)",
            new MenuEntry("코드 보기", null, () => _navigationService.NavigateTo<CodeExplorerViewModel>()),
            new MenuEntry("환경설정(Settings)", PermissionCodes.SystemSettingsRead, () => _navigationService.NavigateTo<SettingsViewModel>()),
            new MenuEntry("감사로그(Audit Log)", PermissionCodes.AuditRead, () => _navigationService.NavigateTo<AuditLogViewModel>()));
    }

    private void AddGroup(string title, params MenuEntry[] entries)
    {
        var items = entries
            .Where(entry => HasAccess(entry.PermissionCode))
            .Select(entry => new ShellMenuItem(entry.Title, new RelayCommand(entry.Navigate)))
            .ToList();

        if (items.Count > 0)
        {
            MenuGroups.Add(new ShellMenuGroup(title, items));
        }
    }

    private bool HasAccess(string? permissionCode)
    {
        return permissionCode is null || _currentUserContext.HasPermission(permissionCode);
    }

    private readonly record struct MenuEntry(string Title, string? PermissionCode, Action Navigate);
}
