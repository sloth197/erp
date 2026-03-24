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
    public string CurrentUsername => _currentUserContext.Username ?? "Guest";
    public string CurrentStatus => IsAuthenticated ? "Active" : "Pending";
    public string CurrentRole => ResolveCurrentRole();
    public string PermissionBadge => $"Perm {_currentUserContext.PermissionCodes.Count}";

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
            UpdateSelectedMenuState();
        }
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(CurrentUsername));
        OnPropertyChanged(nameof(CurrentStatus));
        OnPropertyChanged(nameof(CurrentRole));
        OnPropertyChanged(nameof(PermissionBadge));

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
            new MenuEntry("대시보드(Home)", null, typeof(HomeViewModel), () => _navigationService.NavigateTo<HomeViewModel>()),
            new MenuEntry("알림/공지", null, typeof(NoticesViewModel), () => _navigationService.NavigateTo<NoticesViewModel>()),
            new MenuEntry("내 정보", null, typeof(MyInfoViewModel), () => _navigationService.NavigateTo<MyInfoViewModel>()));

        AddGroup(
            "기준정보(Master Data)",
            new MenuEntry("사용자/권한관리", PermissionCodes.MasterUsersRead, typeof(UsersManagementViewModel), () => _navigationService.NavigateTo<UsersManagementViewModel>()),
            new MenuEntry("거래처 관리", PermissionCodes.MasterPartnersRead, typeof(PartnersViewModel), () => _navigationService.NavigateTo<PartnersViewModel>()),
            new MenuEntry("품목 관리", PermissionCodes.MasterItemsRead, typeof(ItemsViewModel), () => _navigationService.NavigateTo<ItemsViewModel>()),
            new MenuEntry("창고/로케이션 관리", PermissionCodes.MasterItemsRead, typeof(WarehousesViewModel), () => _navigationService.NavigateTo<WarehousesViewModel>()),
            new MenuEntry("코드관리", PermissionCodes.MasterPartnersRead, typeof(CodesViewModel), () => _navigationService.NavigateTo<CodesViewModel>()));

        AddGroup(
            "재고(Inventory)",
            new MenuEntry("재고조회", PermissionCodes.InventoryStockRead, typeof(InventoryOnHandViewModel), () => _navigationService.NavigateTo<InventoryOnHandViewModel>()),
            new MenuEntry("입고 등록", PermissionCodes.InventoryStockReceipt, typeof(StockReceiptViewModel), () => _navigationService.NavigateTo<StockReceiptViewModel>()),
            new MenuEntry("출고 등록", PermissionCodes.InventoryStockIssue, typeof(StockIssueViewModel), () => _navigationService.NavigateTo<StockIssueViewModel>()),
            new MenuEntry("재고조정", PermissionCodes.InventoryStockAdjust, typeof(StockAdjustViewModel), () => _navigationService.NavigateTo<StockAdjustViewModel>()));

        AddGroup(
            "구매/매입(Purchase)",
            new MenuEntry("발주", PermissionCodes.PurchaseOrdersRead, typeof(PurchaseOrdersViewModel), () => _navigationService.NavigateTo<PurchaseOrdersViewModel>()),
            new MenuEntry("입고", PermissionCodes.PurchaseOrdersWrite, typeof(PurchaseReceiptViewModel), () => _navigationService.NavigateTo<PurchaseReceiptViewModel>()));

        AddGroup(
            "판매/매출(Sales)",
            new MenuEntry("견적/주문", PermissionCodes.SalesOrdersRead, typeof(SalesOrdersViewModel), () => _navigationService.NavigateTo<SalesOrdersViewModel>()),
            new MenuEntry("출고/매출", PermissionCodes.SalesOrdersWrite, typeof(SalesRevenueViewModel), () => _navigationService.NavigateTo<SalesRevenueViewModel>()));

        AddGroup(
            "회계(Accounts)",
            new MenuEntry("매입/매출 전표", PermissionCodes.SalesOrdersRead, typeof(AccountVouchersViewModel), () => _navigationService.NavigateTo<AccountVouchersViewModel>()),
            new MenuEntry("간단 리포트", PermissionCodes.SalesOrdersRead, typeof(AccountReportsViewModel), () => _navigationService.NavigateTo<AccountReportsViewModel>()));

        AddGroup(
            "시스템(System)",
            new MenuEntry("환경설정(Settings)", PermissionCodes.SystemSettingsRead, typeof(SettingsViewModel), () => _navigationService.NavigateTo<SettingsViewModel>()),
            new MenuEntry("감사로그(Audit Log)", PermissionCodes.AuditRead, typeof(AuditLogViewModel), () => _navigationService.NavigateTo<AuditLogViewModel>()),
            new MenuEntry("코드 보기", null, typeof(CodeExplorerViewModel), () => _navigationService.NavigateTo<CodeExplorerViewModel>()));

        UpdateSelectedMenuState();
    }

    private void AddGroup(string title, params MenuEntry[] entries)
    {
        var items = entries
            .Where(entry => HasAccess(entry.PermissionCode))
            .Select(entry => new ShellMenuItem(
                entry.Title,
                entry.TargetViewModelType,
                new RelayCommand(() => entry.Navigate())))
            .ToList();

        if (items.Count > 0)
        {
            MenuGroups.Add(new ShellMenuGroup(title, items));
        }
    }

    private void UpdateSelectedMenuState()
    {
        var currentType = CurrentViewModel?.GetType();

        foreach (var group in MenuGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsSelected = currentType is not null && item.TargetViewModelType == currentType;
            }
        }
    }

    private bool HasAccess(string? permissionCode)
    {
        return permissionCode is null || _currentUserContext.HasPermission(permissionCode);
    }

    private string ResolveCurrentRole()
    {
        if (!IsAuthenticated)
        {
            return "Guest";
        }

        if (_currentUserContext.HasPermission(PermissionCodes.MasterUsersWrite) ||
            _currentUserContext.HasPermission(PermissionCodes.SystemSettingsWrite))
        {
            return "Admin";
        }

        return "Staff";
    }

    private readonly record struct MenuEntry(
        string Title,
        string? PermissionCode,
        Type? TargetViewModelType,
        Func<bool> Navigate);
}
