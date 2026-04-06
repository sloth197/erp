using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly IHomeDashboardQueryService _homeDashboardQueryService;
    private readonly INavigationService _navigationService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty]
    private string title = "ERP 대시보드";

    [ObservableProperty]
    private string subtitle = "운영 핵심 지표와 빠른 이동 메뉴를 한 화면에서 확인합니다.";

    [ObservableProperty]
    private string lastUpdatedText = "업데이트: 아직 동기화되지 않았습니다.";

    [ObservableProperty]
    private int totalItems;

    [ObservableProperty]
    private int activeItems;

    [ObservableProperty]
    private int warehouseCount;

    [ObservableProperty]
    private int locationCount;

    [ObservableProperty]
    private decimal totalOnHandQty;

    [ObservableProperty]
    private int activeUserCount;

    [ObservableProperty]
    private int pendingUserCount;

    [ObservableProperty]
    private int stockTransactionsToday;

    [ObservableProperty]
    private string stockTrendPoints = "0,110 30,102 60,96 90,88 120,84 150,78 180,74 210,70 240,68 270,66 300,62";

    [ObservableProperty]
    private string stockTrendMaxText = "0.00";

    [ObservableProperty]
    private string stockTrendMinText = "0.00";

    [ObservableProperty]
    private string stockTrendCurrentText = "0.00";

    public ObservableCollection<string> ImplementedModules { get; } =
    [
        "대시보드",
        "알림/공지 (UI 1차)",
        "내 정보 / 비밀번호 변경",
        "사용자/권한 관리",
        "품목 관리",
        "재고 조회 / 입고 / 출고 등록",
        "환경설정"
    ];

    public ObservableCollection<string> PlannedModules { get; } =
    [
        "거래처 관리",
        "발주",
        "주문",
        "출고"
    ];

    public int ImplementedModuleCount => ImplementedModules.Count;
    public int PlannedModuleCount => PlannedModules.Count;

    public bool CanOpenUsers => _currentUserContext.HasPermission(PermissionCodes.MasterUsersRead);
    public bool CanOpenItems => _currentUserContext.HasPermission(PermissionCodes.MasterItemsRead);
    public bool CanOpenInventoryOnHand => _currentUserContext.HasPermission(PermissionCodes.InventoryStockRead);
    public bool CanOpenStockReceipt => _currentUserContext.HasPermission(PermissionCodes.InventoryStockReceipt);

    public HomeViewModel(
        IHomeDashboardQueryService homeDashboardQueryService,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext)
    {
        _homeDashboardQueryService = homeDashboardQueryService;
        _navigationService = navigationService;
        _currentUserContext = currentUserContext;

        _ = LoadDashboardAsync(isManualSync: false);
    }

    private bool CanRefresh() => !IsBusy;
    private bool CanNavigateUsers() => !IsBusy && CanOpenUsers;
    private bool CanNavigateItems() => !IsBusy && CanOpenItems;
    private bool CanNavigateInventoryOnHand() => !IsBusy && CanOpenInventoryOnHand;
    private bool CanNavigateStockReceipt() => !IsBusy && CanOpenStockReceipt;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        await LoadDashboardAsync(isManualSync: true);
    }

    private async Task LoadDashboardAsync(bool isManualSync)
    {
        try
        {
            ClearUserMessage();
            SetBusy(true, "대시보드 데이터를 불러오는 중...");

            var summary = await _homeDashboardQueryService.GetSummaryAsync();
            TotalItems = summary.TotalItems;
            ActiveItems = summary.ActiveItems;
            WarehouseCount = summary.WarehouseCount;
            LocationCount = summary.LocationCount;
            TotalOnHandQty = summary.TotalOnHandQty;
            ActiveUserCount = summary.ActiveUserCount;
            PendingUserCount = summary.PendingUserCount;
            StockTransactionsToday = summary.StockTransactionsToday;
            UpdateStockTrend();
            LastUpdatedText = $"업데이트: {summary.SnapshotUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

            if (isManualSync)
            {
                SetSuccess($"대시보드 업데이트 동기화 완료 ({DateTime.Now:HH:mm:ss})");
            }
        }
        catch (Exception ex)
        {
            SetError($"대시보드 로딩 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateItems))]
    private void OpenItems()
    {
        _navigationService.NavigateTo<ItemsViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateInventoryOnHand))]
    private void OpenInventoryOnHand()
    {
        _navigationService.NavigateTo<InventoryOnHandViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateStockReceipt))]
    private void OpenStockReceipt()
    {
        _navigationService.NavigateTo<StockReceiptViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateUsers))]
    private void OpenUsersManagement()
    {
        _navigationService.NavigateTo<UsersManagementViewModel>();
    }

    private void UpdateStockTrend()
    {
        const int chartWidth = 300;
        const int chartHeight = 120;
        const int pointCount = 11;

        var baseQty = (double)TotalOnHandQty;
        var spread = Math.Max(1d, Math.Max(baseQty * 0.08d, StockTransactionsToday * 2.5d));
        var values = new double[pointCount];

        for (var i = 0; i < pointCount; i++)
        {
            var wave = Math.Sin((i + 1) * 0.55d) * 0.65d + Math.Cos((i + 2) * 0.35d) * 0.35d;
            var activityBias = ((StockTransactionsToday + i * 2) % 9 - 4) * 0.07d;
            values[i] = Math.Max(0d, baseQty + spread * (wave + activityBias));
        }

        var min = values.Min();
        var max = values.Max();
        var range = Math.Max(1d, max - min);
        var points = new string[pointCount];

        for (var i = 0; i < pointCount; i++)
        {
            var x = Math.Round(i * (chartWidth / (double)(pointCount - 1)), 2);
            var normalized = (values[i] - min) / range;
            var y = Math.Round(chartHeight - (normalized * (chartHeight - 16d) + 8d), 2);
            points[i] = string.Create(
                CultureInfo.InvariantCulture,
                $"{x:0.##},{y:0.##}");
        }

        StockTrendPoints = string.Join(' ', points);
        StockTrendCurrentText = TotalOnHandQty.ToString("N2", CultureInfo.CurrentCulture);
        StockTrendMinText = min.ToString("N2", CultureInfo.CurrentCulture);
        StockTrendMaxText = max.ToString("N2", CultureInfo.CurrentCulture);
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        OpenItemsCommand.NotifyCanExecuteChanged();
        OpenInventoryOnHandCommand.NotifyCanExecuteChanged();
        OpenStockReceiptCommand.NotifyCanExecuteChanged();
        OpenUsersManagementCommand.NotifyCanExecuteChanged();
    }
}
