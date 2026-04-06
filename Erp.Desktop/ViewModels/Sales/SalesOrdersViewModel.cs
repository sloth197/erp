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

[RequiredPermission(PermissionCodes.SalesOrdersRead)]
public sealed partial class SalesOrdersViewModel : ViewModelBase
{
    private readonly ISalesOrderQueryService _salesOrderQueryService;
    private readonly ISalesOrderCommandService _salesOrderCommandService;
    private Guid? _preferredSelectionId;

    [ObservableProperty]
    private string title = "주문";

    [ObservableProperty]
    private string description = "주문 접수, 확정, 출고 연계 흐름을 점검하는 시안 화면";

    [ObservableProperty]
    private string? customerKeyword;

    [ObservableProperty]
    private DateTime? orderDateFilter;

    [ObservableProperty]
    private ObservableCollection<SalesChannelOption> channelOptions = new();

    [ObservableProperty]
    private SalesChannelOption? selectedChannel;

    [ObservableProperty]
    private ObservableCollection<SalesOrderStatusOption> statusOptions = new();

    [ObservableProperty]
    private SalesOrderStatusOption? selectedStatus;

    [ObservableProperty]
    private ObservableCollection<SalesOrderListDto> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmOrderCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlanDeliveryCommand))]
    private SalesOrderListDto? selectedRow;

    [ObservableProperty]
    private int todayReceivedCount;

    [ObservableProperty]
    private int pendingShipmentCount;

    [ObservableProperty]
    private int partialShipmentCount;

    [ObservableProperty]
    private int creditRiskCount;

    public SalesOrdersViewModel(
        ISalesOrderQueryService salesOrderQueryService,
        ISalesOrderCommandService salesOrderCommandService)
    {
        _salesOrderQueryService = salesOrderQueryService;
        _salesOrderCommandService = salesOrderCommandService;

        ChannelOptions = new ObservableCollection<SalesChannelOption>(
        [
            new SalesChannelOption(null, "전체"),
            new SalesChannelOption("직판", "직판"),
            new SalesChannelOption("대리점", "대리점"),
            new SalesChannelOption("온라인", "온라인")
        ]);
        StatusOptions = new ObservableCollection<SalesOrderStatusOption>(
        [
            new SalesOrderStatusOption(null, "전체"),
            new SalesOrderStatusOption("접수", "접수"),
            new SalesOrderStatusOption("확정", "확정"),
            new SalesOrderStatusOption("부분출고", "부분출고"),
            new SalesOrderStatusOption("완료", "완료")
        ]);

        SelectedChannel = ChannelOptions.FirstOrDefault();
        SelectedStatus = StatusOptions.FirstOrDefault();

        _ = SearchAsync();
    }

    partial void OnSelectedRowChanged(SalesOrderListDto? value)
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

    private bool CanConfirmOrder()
    {
        return !IsBusy && SelectedRow is not null;
    }

    private bool CanPlanDelivery()
    {
        return !IsBusy && SelectedRow is not null;
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
            SetBusy(true, "신규 주문 생성 중...");
            var customerName = string.IsNullOrWhiteSpace(CustomerKeyword) ? "신규 고객사" : CustomerKeyword.Trim();
            var commandResult = await _salesOrderCommandService.CreateOrderAsync(new CreateSalesOrderCommand
            {
                CustomerName = customerName,
                RequestedDeliveryDate = DateTime.Today.AddDays(2),
                Channel = SelectedChannel?.Code ?? "직판"
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

    [RelayCommand(CanExecute = nameof(CanConfirmOrder))]
    private async Task ConfirmOrderAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        try
        {
            SetBusy(true, "주문 확정 처리 중...");
            var commandResult = await _salesOrderCommandService.ConfirmOrderAsync(new ConfirmSalesOrderCommand
            {
                SalesOrderId = SelectedRow.Id
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

    [RelayCommand(CanExecute = nameof(CanPlanDelivery))]
    private async Task PlanDeliveryAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        try
        {
            SetBusy(true, "배차 계획 생성 중...");
            var result = await _salesOrderCommandService.CreateDeliveryPlanAsync(new CreateSalesDeliveryPlanCommand
            {
                SalesOrderId = SelectedRow.Id
            });

            SetSuccess(result.Message);
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

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SearchCommand.NotifyCanExecuteChanged();
        CreateOrderCommand.NotifyCanExecuteChanged();
        ConfirmOrderCommand.NotifyCanExecuteChanged();
        PlanDeliveryCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadAsync(Guid? preferredSelectionId, bool clearUserMessage)
    {
        try
        {
            if (clearUserMessage)
            {
                ClearUserMessage();
            }

            SetBusy(true, "주문 목록 조회 중...");
            var result = await _salesOrderQueryService.SearchSalesOrdersAsync(new SearchSalesOrdersQuery
            {
                CustomerKeyword = CustomerKeyword,
                OrderDate = OrderDateFilter,
                Channel = SelectedChannel?.Code,
                Status = SelectedStatus?.Code
            });

            Rows = new ObservableCollection<SalesOrderListDto>(result.Items);
            TodayReceivedCount = result.TodayReceivedCount;
            PendingShipmentCount = result.PendingShipmentCount;
            PartialShipmentCount = result.PartialShipmentCount;
            CreditRiskCount = result.CreditRiskCount;

            SelectedRow = preferredSelectionId is not null
                ? Rows.FirstOrDefault(x => x.Id == preferredSelectionId.Value) ?? Rows.FirstOrDefault()
                : Rows.FirstOrDefault();
            _preferredSelectionId = SelectedRow?.Id;

            if (Rows.Count == 0)
            {
                SetError("현재 조건과 일치하는 주문이 없습니다.");
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

    public sealed record SalesChannelOption(string? Code, string Name);
    public sealed record SalesOrderStatusOption(string? Code, string Name);
}
