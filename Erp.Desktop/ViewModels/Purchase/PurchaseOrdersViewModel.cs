using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.PurchaseOrdersRead)]
public sealed partial class PurchaseOrdersViewModel : ViewModelBase
{
    private readonly IPurchaseOrderQueryService _purchaseOrderQueryService;
    private readonly IPurchaseOrderCommandService _purchaseOrderCommandService;
    private Guid? _preferredSelectionId;

    [ObservableProperty]
    private string title = "발주";

    [ObservableProperty]
    private string description = "발주 등록, 승인, 입고 연계 전 흐름을 먼저 확인하는 시안 화면";

    [ObservableProperty]
    private string? supplierKeyword;

    [ObservableProperty]
    private string? itemKeyword;

    [ObservableProperty]
    private DateTime? dueDateFilter;

    [ObservableProperty]
    private ObservableCollection<PurchaseOrderStatusOption> statusOptions = new();

    [ObservableProperty]
    private PurchaseOrderStatusOption? selectedStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportOrdersCommand))]
    private ObservableCollection<PurchaseOrderListDto> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestApprovalCommand))]
    private PurchaseOrderListDto? selectedRow;

    [ObservableProperty]
    private int weekOrderCount;

    [ObservableProperty]
    private int pendingApprovalCount;

    [ObservableProperty]
    private int delayedCount;

    [ObservableProperty]
    private decimal weekOrderAmount;

    public string WeekOrderAmountDisplay => $"₩{WeekOrderAmount / 1_000_000m:0.0}M";

    public PurchaseOrdersViewModel(
        IPurchaseOrderQueryService purchaseOrderQueryService,
        IPurchaseOrderCommandService purchaseOrderCommandService)
    {
        _purchaseOrderQueryService = purchaseOrderQueryService;
        _purchaseOrderCommandService = purchaseOrderCommandService;

        StatusOptions = new ObservableCollection<PurchaseOrderStatusOption>(
        [
            new PurchaseOrderStatusOption(null, "전체"),
            new PurchaseOrderStatusOption("작성중", "작성중"),
            new PurchaseOrderStatusOption("승인대기", "승인대기"),
            new PurchaseOrderStatusOption("승인완료", "승인완료"),
            new PurchaseOrderStatusOption("입고완료", "입고완료")
        ]);
        SelectedStatus = StatusOptions.FirstOrDefault();

        _ = SearchAsync();
    }

    partial void OnWeekOrderAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(WeekOrderAmountDisplay));
    }

    partial void OnSelectedRowChanged(PurchaseOrderListDto? value)
    {
        _preferredSelectionId = value?.Id;
    }

    private bool CanSearch()
    {
        return !IsBusy;
    }

    private bool CanCreateOrder()
    {
        return !IsBusy;
    }

    private bool CanRequestApproval()
    {
        return !IsBusy && SelectedRow is not null;
    }

    private bool CanExportOrders()
    {
        return !IsBusy && Rows.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        await ReloadAsync(_preferredSelectionId, clearUserMessage: true);
    }

    [RelayCommand(CanExecute = nameof(CanCreateOrder))]
    private async Task CreateOrderAsync()
    {
        try
        {
            SetBusy(true, "신규 발주 생성 중...");
            var supplierName = string.IsNullOrWhiteSpace(SupplierKeyword) ? "신규 공급처" : SupplierKeyword.Trim();
            var itemSummary = string.IsNullOrWhiteSpace(ItemKeyword) ? "신규 품목 1건" : $"{ItemKeyword.Trim()} 1건";

            var commandResult = await _purchaseOrderCommandService.CreateDraftAsync(new CreatePurchaseOrderDraftCommand
            {
                SupplierName = supplierName,
                ItemSummary = itemSummary,
                DueDate = DueDateFilter
            });

            await ReloadAsync(commandResult.Id, clearUserMessage: false);
            SetSuccess(commandResult.Message);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRequestApproval))]
    private async Task RequestApprovalAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        try
        {
            SetBusy(true, "승인 요청 반영 중...");
            var commandResult = await _purchaseOrderCommandService.RequestApprovalAsync(new RequestPurchaseOrderApprovalCommand
            {
                PurchaseOrderId = SelectedRow.Id
            });

            await ReloadAsync(commandResult.Id, clearUserMessage: false);
            SetSuccess(commandResult.Message);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportOrders))]
    private void ExportOrders()
    {
        SetSuccess($"발주 {Rows.Count}건 엑셀 내보내기 요청을 생성했습니다. (데모)");
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SearchCommand.NotifyCanExecuteChanged();
        CreateOrderCommand.NotifyCanExecuteChanged();
        RequestApprovalCommand.NotifyCanExecuteChanged();
        ExportOrdersCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadAsync(Guid? preferredSelectionId, bool clearUserMessage)
    {
        try
        {
            if (clearUserMessage)
            {
                ClearUserMessage();
            }

            SetBusy(true, "발주 목록 조회 중...");

            var result = await _purchaseOrderQueryService.SearchPurchaseOrdersAsync(new SearchPurchaseOrdersQuery
            {
                SupplierKeyword = SupplierKeyword,
                ItemKeyword = ItemKeyword,
                DueDate = DueDateFilter,
                Status = SelectedStatus?.Code
            });

            Rows = new ObservableCollection<PurchaseOrderListDto>(result.Items);
            WeekOrderCount = result.WeekOrderCount;
            PendingApprovalCount = result.PendingApprovalCount;
            DelayedCount = result.DelayedCount;
            WeekOrderAmount = result.WeekOrderAmount;

            SelectedRow = preferredSelectionId is not null
                ? Rows.FirstOrDefault(x => x.Id == preferredSelectionId.Value) ?? Rows.FirstOrDefault()
                : Rows.FirstOrDefault();

            _preferredSelectionId = SelectedRow?.Id;

            if (Rows.Count == 0)
            {
                SetError("현재 조건과 일치하는 발주가 없습니다.");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public sealed record PurchaseOrderStatusOption(string? Code, string Name);
}
