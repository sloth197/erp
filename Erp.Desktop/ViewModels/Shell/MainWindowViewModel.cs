using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;
using Erp.Domain.Entities;

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
    public bool CanOpenNotices => IsAuthenticated;

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

    [RelayCommand(CanExecute = nameof(CanOpenNotices))]
    private void OpenNotices()
    {
        _navigationService.NavigateTo<NoticesViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanOpenMyInfo))]
    private void OpenMyInfo()
    {
        _navigationService.NavigateTo<MyInfoViewModel>();
    }

    private bool CanOpenMyInfo()
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
        OnPropertyChanged(nameof(CanOpenNotices));

        GoHomeCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();
        OpenNoticesCommand.NotifyCanExecuteChanged();
        OpenMyInfoCommand.NotifyCanExecuteChanged();

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
            "기본",
            new MenuEntry("대시보드", null, typeof(HomeViewModel), () => _navigationService.NavigateTo<HomeViewModel>()));

        AddGroup(
            "기준정보",
            new MenuEntry("거래처 관리", PermissionCodes.MasterPartnersRead, typeof(PartnersViewModel), () => _navigationService.NavigateTo<PartnersViewModel>(), UserJobGrade.Staff),
            new MenuEntry("품목 관리", PermissionCodes.MasterItemsRead, typeof(ItemsViewModel), () => _navigationService.NavigateTo<ItemsViewModel>(), UserJobGrade.Staff));

        AddGroup(
            "재고/물류",
            new MenuEntry("재고조회", PermissionCodes.InventoryStockRead, typeof(InventoryOnHandViewModel), () => _navigationService.NavigateTo<InventoryOnHandViewModel>(), UserJobGrade.Staff),
            new MenuEntry("입고 등록", PermissionCodes.InventoryStockReceipt, typeof(StockReceiptViewModel), () => _navigationService.NavigateTo<StockReceiptViewModel>(), UserJobGrade.AssistantManager),
            new MenuEntry("출고 등록", PermissionCodes.InventoryStockIssue, typeof(StockIssueViewModel), () => _navigationService.NavigateTo<StockIssueViewModel>(), UserJobGrade.Manager));

        AddGroup(
            "영업/구매",
            new MenuEntry("발주", PermissionCodes.PurchaseOrdersRead, typeof(PurchaseOrdersViewModel), () => _navigationService.NavigateTo<PurchaseOrdersViewModel>(), UserJobGrade.Staff),
            new MenuEntry("주문", PermissionCodes.SalesOrdersRead, typeof(SalesOrdersViewModel), () => _navigationService.NavigateTo<SalesOrdersViewModel>(), UserJobGrade.Staff),
            new MenuEntry("출고", PermissionCodes.SalesOrdersWrite, typeof(SalesRevenueViewModel), () => _navigationService.NavigateTo<SalesRevenueViewModel>(), UserJobGrade.Manager));

        AddGroup(
            "관리",
            new MenuEntry("사용자/권한관리", PermissionCodes.MasterUsersWrite, typeof(UsersManagementViewModel), () => _navigationService.NavigateTo<UsersManagementViewModel>(), UserJobGrade.GeneralManager),
            new MenuEntry("환경설정(Settings)", PermissionCodes.SystemSettingsRead, typeof(SettingsViewModel), () => _navigationService.NavigateTo<SettingsViewModel>(), UserJobGrade.Staff));

        UpdateSelectedMenuState();
    }

    private void AddGroup(string title, params MenuEntry[] entries)
    {
        var items = entries
            .Where(entry => HasAccess(entry.PermissionCode) && HasJobGradeAccess(entry.MinJobGrade))
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

    private bool HasJobGradeAccess(UserJobGrade? minimumGrade)
    {
        if (minimumGrade is null)
        {
            return true;
        }

        var currentGrade = _currentUserContext.JobGrade ?? UserJobGrade.Staff;
        return currentGrade >= minimumGrade.Value;
    }

    private string ResolveCurrentRole()
    {
        if (!IsAuthenticated)
        {
            return "Guest";
        }

        var systemRole = (_currentUserContext.HasPermission(PermissionCodes.MasterUsersWrite) ||
                          _currentUserContext.HasPermission(PermissionCodes.SystemSettingsWrite))
            ? "Admin"
            : "Staff";

        var jobGradeText = (_currentUserContext.JobGrade ?? UserJobGrade.Staff) switch
        {
            UserJobGrade.Staff => "사원",
            UserJobGrade.AssistantManager => "대리",
            UserJobGrade.Manager => "과장",
            UserJobGrade.DeputyGeneralManager => "차장",
            UserJobGrade.GeneralManager => "부장",
            UserJobGrade.President => "사장",
            _ => "사원"
        };

        return $"{systemRole} · {jobGradeText}";
    }

    private readonly record struct MenuEntry(
        string Title,
        string? PermissionCode,
        Type? TargetViewModelType,
        Func<bool> Navigate,
        UserJobGrade? MinJobGrade = null);
}
