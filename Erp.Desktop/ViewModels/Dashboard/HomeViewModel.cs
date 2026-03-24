using System.Collections.ObjectModel;
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
    private int auditLogsLast24Hours;

    public ObservableCollection<string> ImplementedModules { get; } =
    [
        "회원가입 이메일 인증 (SMTP)",
        "품목 관리",
        "재고 조회 / 입고 / 출고 / 조정",
        "사용자/권한 관리",
        "감사 로그",
        "내 정보 / 비밀번호 변경"
    ];

    public ObservableCollection<string> PlannedModules { get; } =
    [
        "알림/공지",
        "거래처 관리",
        "창고/로케이션 상세 관리",
        "코드관리",
        "구매/매입",
        "판매/매출",
        "회계 리포트"
    ];

    public int ImplementedModuleCount => ImplementedModules.Count;
    public int PlannedModuleCount => PlannedModules.Count;

    public bool CanOpenUsers => _currentUserContext.HasPermission(PermissionCodes.MasterUsersRead);
    public bool CanOpenItems => _currentUserContext.HasPermission(PermissionCodes.MasterItemsRead);
    public bool CanOpenInventoryOnHand => _currentUserContext.HasPermission(PermissionCodes.InventoryStockRead);
    public bool CanOpenStockReceipt => _currentUserContext.HasPermission(PermissionCodes.InventoryStockReceipt);
    public bool CanOpenAuditLog => _currentUserContext.HasPermission(PermissionCodes.AuditRead);

    public HomeViewModel(
        IHomeDashboardQueryService homeDashboardQueryService,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext)
    {
        _homeDashboardQueryService = homeDashboardQueryService;
        _navigationService = navigationService;
        _currentUserContext = currentUserContext;

        _ = RefreshAsync();
    }

    private bool CanRefresh() => !IsBusy;
    private bool CanNavigateUsers() => !IsBusy && CanOpenUsers;
    private bool CanNavigateItems() => !IsBusy && CanOpenItems;
    private bool CanNavigateInventoryOnHand() => !IsBusy && CanOpenInventoryOnHand;
    private bool CanNavigateStockReceipt() => !IsBusy && CanOpenStockReceipt;
    private bool CanNavigateAuditLog() => !IsBusy && CanOpenAuditLog;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
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
            AuditLogsLast24Hours = summary.AuditLogsLast24Hours;
            LastUpdatedText = $"업데이트: {summary.SnapshotUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
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

    [RelayCommand(CanExecute = nameof(CanNavigateAuditLog))]
    private void OpenAuditLog()
    {
        _navigationService.NavigateTo<AuditLogViewModel>();
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        OpenItemsCommand.NotifyCanExecuteChanged();
        OpenInventoryOnHandCommand.NotifyCanExecuteChanged();
        OpenStockReceiptCommand.NotifyCanExecuteChanged();
        OpenUsersManagementCommand.NotifyCanExecuteChanged();
        OpenAuditLogCommand.NotifyCanExecuteChanged();
    }
}
