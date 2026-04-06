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

[RequiredPermission(PermissionCodes.SalesOrdersWrite)]
public sealed partial class SalesRevenueViewModel : ViewModelBase
{
    private readonly ISalesShipmentQueryService _salesShipmentQueryService;
    private readonly ISalesShipmentCommandService _salesShipmentCommandService;
    private Guid? _preferredSelectionId;

    [ObservableProperty]
    private string title = "출고";

    [ObservableProperty]
    private string description = "출고 작업(피킹/포장/송장) 흐름을 점검하는 시안 화면";

    [ObservableProperty]
    private ObservableCollection<WarehouseOption> warehouseOptions = new();

    [ObservableProperty]
    private WarehouseOption? selectedWarehouse;

    [ObservableProperty]
    private ObservableCollection<ShippingTypeOption> shippingTypeOptions = new();

    [ObservableProperty]
    private ShippingTypeOption? selectedShippingType;

    [ObservableProperty]
    private DateTime? shipmentDateFilter;

    [ObservableProperty]
    private ObservableCollection<ShipmentStatusOption> progressOptions = new();

    [ObservableProperty]
    private ShipmentStatusOption? selectedProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterBulkTrackingCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseShipmentDayCommand))]
    private ObservableCollection<SalesShipmentListDto> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmShipmentCommand))]
    private SalesShipmentListDto? selectedRow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmShipmentCommand))]
    private bool isPickingCompleted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmShipmentCommand))]
    private bool isPackingCompleted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmShipmentCommand))]
    private bool isTrackingCompleted;

    [ObservableProperty]
    private string? workNote;

    [ObservableProperty]
    private int todayShipmentCount;

    [ObservableProperty]
    private int pickingWaitingCount;

    [ObservableProperty]
    private int packedCount;

    [ObservableProperty]
    private int missingTrackingCount;

    public SalesRevenueViewModel(
        ISalesShipmentQueryService salesShipmentQueryService,
        ISalesShipmentCommandService salesShipmentCommandService)
    {
        _salesShipmentQueryService = salesShipmentQueryService;
        _salesShipmentCommandService = salesShipmentCommandService;

        WarehouseOptions = new ObservableCollection<WarehouseOption>(
        [
            new WarehouseOption(null, "전체"),
            new WarehouseOption("본사 창고", "본사 창고"),
            new WarehouseOption("동부 센터", "동부 센터"),
            new WarehouseOption("서부 센터", "서부 센터")
        ]);
        ShippingTypeOptions = new ObservableCollection<ShippingTypeOption>(
        [
            new ShippingTypeOption(null, "전체"),
            new ShippingTypeOption("택배", "택배"),
            new ShippingTypeOption("직배송", "직배송"),
            new ShippingTypeOption("퀵배송", "퀵배송")
        ]);
        ProgressOptions = new ObservableCollection<ShipmentStatusOption>(
        [
            new ShipmentStatusOption(null, "전체"),
            new ShipmentStatusOption("피킹대기", "피킹대기"),
            new ShipmentStatusOption("포장완료", "포장완료"),
            new ShipmentStatusOption("출고완료", "출고완료")
        ]);

        SelectedWarehouse = WarehouseOptions.FirstOrDefault();
        SelectedShippingType = ShippingTypeOptions.FirstOrDefault();
        SelectedProgress = ProgressOptions.FirstOrDefault();

        _ = SearchAsync();
    }

    partial void OnSelectedRowChanged(SalesShipmentListDto? value)
    {
        _preferredSelectionId = value?.Id;

        if (value is null)
        {
            IsPickingCompleted = false;
            IsPackingCompleted = false;
            IsTrackingCompleted = false;
            WorkNote = null;
            return;
        }

        IsPickingCompleted = value.Status is "포장완료" or "출고완료";
        IsPackingCompleted = value.Status is "포장완료" or "출고완료";
        IsTrackingCompleted = !string.IsNullOrWhiteSpace(value.TrackingNumber);
        WorkNote = $"{value.CustomerName} / {value.SalesOrderNumber}";
    }

    private bool CanSearch()
    {
        return !IsBusy;
    }

    private bool CanCreateShipment()
    {
        return !IsBusy;
    }

    private bool CanConfirmShipment()
    {
        return !IsBusy &&
            SelectedRow is { Status: not "출고완료" } &&
            IsPickingCompleted &&
            IsPackingCompleted &&
            IsTrackingCompleted;
    }

    private bool CanRegisterBulkTracking()
    {
        return !IsBusy && Rows.Count > 0;
    }

    private bool CanCloseShipmentDay()
    {
        return !IsBusy && Rows.Any(x => x.Status == "출고완료");
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        await ReloadAsync(_preferredSelectionId, clearUserMessage: true);
    }

    [RelayCommand(CanExecute = nameof(CanCreateShipment))]
    private async Task CreateShipmentAsync()
    {
        try
        {
            SetBusy(true, "출고 생성 중...");
            var result = await _salesShipmentCommandService.CreateShipmentAsync(new CreateSalesShipmentCommand
            {
                Warehouse = SelectedWarehouse?.Code ?? "본사 창고",
                ShippingType = SelectedShippingType?.Code ?? "택배"
            });

            await ReloadAsync(result.Id, clearUserMessage: false);
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

    [RelayCommand(CanExecute = nameof(CanConfirmShipment))]
    private async Task ConfirmShipmentAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        try
        {
            SetBusy(true, "출고 확정 처리 중...");
            var result = await _salesShipmentCommandService.ConfirmShipmentAsync(new ConfirmSalesShipmentCommand
            {
                ShipmentId = SelectedRow.Id,
                IsPickingCompleted = IsPickingCompleted,
                IsPackingCompleted = IsPackingCompleted,
                IsTrackingCompleted = IsTrackingCompleted,
                WorkNote = WorkNote
            });

            await ReloadAsync(result.Id, clearUserMessage: false);
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

    [RelayCommand(CanExecute = nameof(CanRegisterBulkTracking))]
    private async Task RegisterBulkTrackingAsync()
    {
        try
        {
            SetBusy(true, "송장 일괄 등록 중...");
            var targetIds = Rows.Select(x => x.Id).ToArray();
            var result = await _salesShipmentCommandService.RegisterBulkTrackingAsync(new RegisterBulkTrackingCommand
            {
                ShipmentIds = targetIds
            });

            await ReloadAsync(SelectedRow?.Id, clearUserMessage: false);
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

    [RelayCommand(CanExecute = nameof(CanCloseShipmentDay))]
    private async Task CloseShipmentDayAsync()
    {
        try
        {
            SetBusy(true, "출고 마감 처리 중...");
            var result = await _salesShipmentCommandService.CloseShipmentDayAsync(new CloseShipmentDayCommand
            {
                Date = (ShipmentDateFilter ?? DateTime.Today).Date
            });

            await ReloadAsync(SelectedRow?.Id, clearUserMessage: false);
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
        CreateShipmentCommand.NotifyCanExecuteChanged();
        ConfirmShipmentCommand.NotifyCanExecuteChanged();
        RegisterBulkTrackingCommand.NotifyCanExecuteChanged();
        CloseShipmentDayCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadAsync(Guid? preferredSelectionId, bool clearUserMessage)
    {
        try
        {
            if (clearUserMessage)
            {
                ClearUserMessage();
            }

            SetBusy(true, "출고 목록 조회 중...");
            var result = await _salesShipmentQueryService.SearchSalesShipmentsAsync(new SearchSalesShipmentsQuery
            {
                Warehouse = SelectedWarehouse?.Code,
                ShippingType = SelectedShippingType?.Code,
                ShipmentDate = ShipmentDateFilter,
                Status = SelectedProgress?.Code
            });

            Rows = new ObservableCollection<SalesShipmentListDto>(result.Items);
            TodayShipmentCount = result.TodayShipmentCount;
            PickingWaitingCount = result.PickingWaitingCount;
            PackedCount = result.PackedCount;
            MissingTrackingCount = result.MissingTrackingCount;

            SelectedRow = preferredSelectionId is not null
                ? Rows.FirstOrDefault(x => x.Id == preferredSelectionId.Value) ?? Rows.FirstOrDefault()
                : Rows.FirstOrDefault();
            _preferredSelectionId = SelectedRow?.Id;

            if (Rows.Count == 0)
            {
                SetError("현재 조건과 일치하는 출고 건이 없습니다.");
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

    public sealed record WarehouseOption(string? Code, string Name);
    public sealed record ShippingTypeOption(string? Code, string Name);
    public sealed record ShipmentStatusOption(string? Code, string Name);
}
