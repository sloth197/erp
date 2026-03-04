using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.InventoryStockWrite)]
public sealed partial class StockAdjustViewModel : ObservableObject
{
    private readonly IInventoryCommandService _inventoryCommandService;
    private readonly IInventoryQueryService _inventoryQueryService;

    [ObservableProperty]
    private ObservableCollection<WarehouseOption> warehouses = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private WarehouseOption? selectedWarehouse;

    [ObservableProperty]
    private ObservableCollection<LocationOption> locations = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private LocationOption? selectedLocation;

    [ObservableProperty]
    private DateTime occurredAt = DateTime.Now;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchItemsCommand))]
    private string? itemKeyword;

    [ObservableProperty]
    private ObservableCollection<ItemOption> itemOptions = new();

    [ObservableProperty]
    private ObservableCollection<AdjustLineViewModel> lines = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveLineCommand))]
    private AdjustLineViewModel? selectedLine;

    [ObservableProperty]
    private ObservableCollection<AdjustDiffRow> lastDiffRows = new();

    [ObservableProperty]
    private string? lastTxNo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchItemsCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddLineCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveLineCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    public bool CanAdjust { get; }

    public StockAdjustViewModel(
        IInventoryCommandService inventoryCommandService,
        IInventoryQueryService inventoryQueryService,
        ICurrentUserContext currentUserContext)
    {
        _inventoryCommandService = inventoryCommandService;
        _inventoryQueryService = inventoryQueryService;

        CanAdjust = currentUserContext.HasPermission(PermissionCodes.InventoryStockWrite)
            || currentUserContext.HasPermission(PermissionCodes.InventoryStockAdjust);

        Lines.Add(new AdjustLineViewModel());
        _ = InitializeAsync();
    }

    private bool CanSearchItems()
    {
        return !IsBusy;
    }

    private bool CanAddLine()
    {
        return !IsBusy;
    }

    private bool CanRemoveLine()
    {
        return !IsBusy && SelectedLine is not null;
    }

    private bool CanSave()
    {
        return !IsBusy && CanAdjust && SelectedWarehouse is not null;
    }

    [RelayCommand(CanExecute = nameof(CanSearchItems))]
    private async Task SearchItemsAsync()
    {
        await LoadItemOptionsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private void AddLine()
    {
        var line = new AdjustLineViewModel();
        if (ItemOptions.Count > 0)
        {
            line.SelectedItem = ItemOptions[0];
        }

        Lines.Add(line);
        SelectedLine = line;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveLine))]
    private void RemoveLine()
    {
        if (SelectedLine is null)
        {
            return;
        }

        _ = Lines.Remove(SelectedLine);
        if (Lines.Count == 0)
        {
            Lines.Add(new AdjustLineViewModel());
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanAdjust)
        {
            StatusMessage = "재고조정 권한이 없습니다.";
            return;
        }

        if (!ValidateLines())
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;
            LastTxNo = null;
            LastDiffRows = new ObservableCollection<AdjustDiffRow>();

            var itemIds = Lines
                .Where(x => x.SelectedItem is not null)
                .Select(x => x.SelectedItem!.Id)
                .Distinct()
                .ToArray();

            var currentBalances = await _inventoryQueryService.GetOnHandByItemsAsync(
                SelectedWarehouse!.Id,
                SelectedLocation?.Id,
                itemIds);
            var currentMap = currentBalances.ToDictionary(x => x.ItemId, x => x.QtyOnHand);

            var command = new AdjustStockByCountCommand
            {
                WarehouseId = SelectedWarehouse.Id,
                LocationId = SelectedLocation?.Id,
                OccurredAtUtc = OccurredAt.ToUniversalTime(),
                Lines = Lines.Select(line => new AdjustStockByCountLineCommand
                {
                    ItemId = line.SelectedItem!.Id,
                    CountedQty = line.CountedQty,
                    Note = line.Note
                }).ToArray()
            };

            var result = await _inventoryCommandService.AdjustStockByCountAsync(command);
            LastTxNo = result.TxNo;

            var diffRows = Lines
                .Where(x => x.SelectedItem is not null)
                .Select(x =>
                {
                    var currentQty = currentMap.TryGetValue(x.SelectedItem!.Id, out var qty) ? qty : 0m;
                    var diffQty = x.CountedQty - currentQty;
                    return new AdjustDiffRow(
                        x.SelectedItem.ItemCode,
                        x.SelectedItem.Name,
                        currentQty,
                        x.CountedQty,
                        diffQty);
                })
                .Where(x => x.DiffQty != 0m)
                .ToList();

            LastDiffRows = new ObservableCollection<AdjustDiffRow>(diffRows);
            StatusMessage = $"재고조정 저장 완료 (TxNo: {result.TxNo}, 반영 {result.LineCount}건)";

            Lines = new ObservableCollection<AdjustLineViewModel> { new() };
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedWarehouseChanged(WarehouseOption? value)
    {
        _ = LoadLocationOptionsAsync(value?.Id ?? Guid.Empty);
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;

            var warehouses = await _inventoryQueryService.GetWarehouseOptionsAsync();
            Warehouses = new ObservableCollection<WarehouseOption>(warehouses
                .Select(x => new WarehouseOption(x.Id, $"{x.Code} - {x.Name}")));

            SelectedWarehouse = Warehouses.FirstOrDefault();
            await LoadLocationOptionsAsync(SelectedWarehouse?.Id ?? Guid.Empty);
            await LoadItemOptionsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadLocationOptionsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            Locations = new ObservableCollection<LocationOption>();
            SelectedLocation = null;
            return;
        }

        var locations = await _inventoryQueryService.GetLocationOptionsAsync(warehouseId);
        Locations = new ObservableCollection<LocationOption>(locations
            .Select(x => new LocationOption(x.Id, $"{x.Code} - {x.Name}")));
        SelectedLocation = null;
    }

    private async Task LoadItemOptionsAsync()
    {
        var options = await _inventoryQueryService.SearchItemOptionsAsync(ItemKeyword, take: 80);
        ItemOptions = new ObservableCollection<ItemOption>(options.Select(x =>
            new ItemOption(x.Id, x.ItemCode, x.Name, x.TrackingType.ToString())));

        foreach (var line in Lines)
        {
            if (line.SelectedItem is null)
            {
                continue;
            }

            var mapped = ItemOptions.FirstOrDefault(x => x.Id == line.SelectedItem.Id);
            if (mapped is not null)
            {
                line.SelectedItem = mapped;
            }
        }
    }

    private bool ValidateLines()
    {
        if (SelectedWarehouse is null)
        {
            StatusMessage = "창고를 선택하세요.";
            return false;
        }

        if (Lines.Count == 0)
        {
            StatusMessage = "조정 라인을 추가하세요.";
            return false;
        }

        var hasError = false;
        foreach (var line in Lines)
        {
            line.ErrorMessage = null;

            if (line.SelectedItem is null)
            {
                line.ErrorMessage = "품목을 선택하세요.";
                hasError = true;
            }
            else if (line.CountedQty < 0m)
            {
                line.ErrorMessage = "실사수량은 0 이상이어야 합니다.";
                hasError = true;
            }
        }

        if (hasError)
        {
            StatusMessage = "라인 입력값을 확인하세요.";
            return false;
        }

        return true;
    }

    public sealed record WarehouseOption(Guid Id, string DisplayName);
    public sealed record LocationOption(Guid Id, string DisplayName);

    public sealed record ItemOption(Guid Id, string ItemCode, string Name, string TrackingType)
    {
        public string DisplayName => $"{ItemCode} - {Name}";
    }

    public sealed record AdjustDiffRow(
        string ItemCode,
        string ItemName,
        decimal CurrentQty,
        decimal CountedQty,
        decimal DiffQty);

    public sealed partial class AdjustLineViewModel : ObservableObject
    {
        [ObservableProperty]
        private ItemOption? selectedItem;

        [ObservableProperty]
        private decimal countedQty;

        [ObservableProperty]
        private string? note;

        [ObservableProperty]
        private string? errorMessage;
    }
}
