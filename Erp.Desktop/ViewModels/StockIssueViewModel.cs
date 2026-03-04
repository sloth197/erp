using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.InventoryStockWrite)]
public sealed partial class StockIssueViewModel : ObservableObject
{
    private static readonly Regex ItemIdRegex = new("item '\\s*(?<id>[0-9a-fA-F-]{36})\\s*'", RegexOptions.Compiled);
    private static readonly Regex LotRegex = new("lot '\\s*(?<lot>[^']+)\\s*'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SerialRegex = new("serial '\\s*(?<serial>[^']+)\\s*'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
    private ObservableCollection<IssueLineViewModel> lines = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveLineCommand))]
    private IssueLineViewModel? selectedLine;

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

    public bool CanWrite { get; }

    public StockIssueViewModel(
        IInventoryCommandService inventoryCommandService,
        IInventoryQueryService inventoryQueryService,
        ICurrentUserContext currentUserContext)
    {
        _inventoryCommandService = inventoryCommandService;
        _inventoryQueryService = inventoryQueryService;

        CanWrite = currentUserContext.HasPermission(PermissionCodes.InventoryStockWrite);
        Lines.Add(new IssueLineViewModel());
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
        return !IsBusy && CanWrite && SelectedWarehouse is not null;
    }

    [RelayCommand(CanExecute = nameof(CanSearchItems))]
    private async Task SearchItemsAsync()
    {
        await LoadItemOptionsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private void AddLine()
    {
        var line = new IssueLineViewModel();
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
            Lines.Add(new IssueLineViewModel());
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanWrite)
        {
            StatusMessage = "출고 등록 권한이 없습니다.";
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

            var command = new IssueStockCommand
            {
                WarehouseId = SelectedWarehouse!.Id,
                LocationId = SelectedLocation?.Id,
                OccurredAtUtc = OccurredAt.ToUniversalTime(),
                Lines = Lines.Select(line => new IssueStockLineCommand
                {
                    ItemId = line.SelectedItem!.Id,
                    Qty = line.Qty,
                    LotNo = line.LotNo,
                    SerialNo = line.SerialNo,
                    Note = line.Note
                }).ToArray()
            };

            var result = await _inventoryCommandService.IssueStockAsync(command);
            LastTxNo = result.TxNo;
            StatusMessage = $"출고 저장 완료 (TxNo: {result.TxNo})";

            Lines = new ObservableCollection<IssueLineViewModel> { new() };
        }
        catch (Exception ex)
        {
            TryAssignLineError(ex.Message);
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
            StatusMessage = "출고 라인을 추가하세요.";
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
            else if (line.Qty <= 0m)
            {
                line.ErrorMessage = "수량은 0보다 커야 합니다.";
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

    private void TryAssignLineError(string message)
    {
        foreach (var line in Lines)
        {
            line.ErrorMessage = null;
        }

        var itemMatch = ItemIdRegex.Match(message);
        if (itemMatch.Success && Guid.TryParse(itemMatch.Groups["id"].Value, out var itemId))
        {
            var line = Lines.FirstOrDefault(x => x.SelectedItem?.Id == itemId);
            if (line is not null)
            {
                line.ErrorMessage = message;
                return;
            }
        }

        var lotMatch = LotRegex.Match(message);
        if (lotMatch.Success)
        {
            var lotNo = lotMatch.Groups["lot"].Value.Trim();
            var line = Lines.FirstOrDefault(x => string.Equals(x.LotNo?.Trim(), lotNo, StringComparison.OrdinalIgnoreCase));
            if (line is not null)
            {
                line.ErrorMessage = message;
                return;
            }
        }

        var serialMatch = SerialRegex.Match(message);
        if (serialMatch.Success)
        {
            var serialNo = serialMatch.Groups["serial"].Value.Trim();
            var line = Lines.FirstOrDefault(x => string.Equals(x.SerialNo?.Trim(), serialNo, StringComparison.OrdinalIgnoreCase));
            if (line is not null)
            {
                line.ErrorMessage = message;
            }
        }
    }

    public sealed record WarehouseOption(Guid Id, string DisplayName);
    public sealed record LocationOption(Guid Id, string DisplayName);

    public sealed record ItemOption(Guid Id, string ItemCode, string Name, string TrackingType)
    {
        public string DisplayName => $"{ItemCode} - {Name}";
    }

    public sealed partial class IssueLineViewModel : ObservableObject
    {
        [ObservableProperty]
        private ItemOption? selectedItem;

        [ObservableProperty]
        private decimal qty = 1m;

        [ObservableProperty]
        private string? lotNo;

        [ObservableProperty]
        private string? serialNo;

        [ObservableProperty]
        private string? note;

        [ObservableProperty]
        private string? errorMessage;
    }
}
