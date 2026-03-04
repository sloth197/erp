using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.InventoryStockRead)]
public sealed partial class InventoryOnHandViewModel : ViewModelBase
{
    private readonly IInventoryQueryService _inventoryQueryService;
    private readonly IItemQueryService _itemQueryService;

    [ObservableProperty]
    private ObservableCollection<WarehouseFilterOption> warehouses = new();

    [ObservableProperty]
    private WarehouseFilterOption? selectedWarehouse;

    [ObservableProperty]
    private ObservableCollection<CategoryFilterOption> categories = new();

    [ObservableProperty]
    private CategoryFilterOption? selectedCategory;

    [ObservableProperty]
    private string? keyword;

    [ObservableProperty]
    private bool includeLocations = true;

    [ObservableProperty]
    private string selectedSort = "itemcode";

    [ObservableProperty]
    private string selectedSortDirection = "asc";

    [ObservableProperty]
    private ObservableCollection<StockOnHandRow> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int page = 1;

    [ObservableProperty]
    private int pageSize = 50;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int totalCount;

    public ObservableCollection<int> PageSizes { get; } = new([20, 50, 100, 200]);
    public ObservableCollection<SortFieldOption> SortFields { get; } =
        new(
        [
            new SortFieldOption("itemcode", "Item Code"),
            new SortFieldOption("itemname", "Item Name"),
            new SortFieldOption("qtyonhand", "On Hand"),
            new SortFieldOption("warehousecode", "Warehouse"),
            new SortFieldOption("locationcode", "Location"),
            new SortFieldOption("updatedatutc", "Updated At")
        ]);

    public ObservableCollection<SortDirectionOption> SortDirections { get; } =
        new(
        [
            new SortDirectionOption("asc", "Ascending"),
            new SortDirectionOption("desc", "Descending")
        ]);

    public bool CanRead { get; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool ShowEmptyState => !IsBusy && Rows.Count == 0;

    public InventoryOnHandViewModel(
        IInventoryQueryService inventoryQueryService,
        IItemQueryService itemQueryService,
        ICurrentUserContext currentUserContext)
    {
        _inventoryQueryService = inventoryQueryService;
        _itemQueryService = itemQueryService;
        CanRead = currentUserContext.HasPermission(PermissionCodes.InventoryStockRead);
        _ = InitializeAsync();
    }

    partial void OnRowsChanged(ObservableCollection<StockOnHandRow> value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private bool CanSearch()
    {
        return !IsBusy && CanRead;
    }

    private bool CanGoPrevious()
    {
        return !IsBusy && CanRead && Page > 1;
    }

    private bool CanGoNext()
    {
        return !IsBusy && CanRead && Page < TotalPages;
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        await LoadInternalAsync(resetPage: true);
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task LoadAsync()
    {
        await LoadInternalAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousPageAsync()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadInternalAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        if (Page >= TotalPages)
        {
            return;
        }

        Page++;
        await LoadInternalAsync(resetPage: false);
    }

    partial void OnPageSizeChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));

        if (!IsBusy && value > 0)
        {
            _ = LoadInternalAsync(resetPage: true);
        }
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
    }

    private async Task InitializeAsync()
    {
        try
        {
            ClearUserMessage();
            SetBusy(true, "Loading inventory filters...");

            var warehouseTask = _inventoryQueryService.GetWarehouseOptionsAsync();
            var categoryTask = _itemQueryService.GetItemCategoryOptionsAsync();
            await Task.WhenAll(warehouseTask, categoryTask);

            var warehouseOptions = warehouseTask.Result
                .Select(x => new WarehouseFilterOption(x.Id, $"{x.Code} - {x.Name}"))
                .ToList();
            Warehouses = new ObservableCollection<WarehouseFilterOption>(warehouseOptions);
            SelectedWarehouse = Warehouses.FirstOrDefault();

            var categoryOptions = new List<CategoryFilterOption> { CategoryFilterOption.All };
            categoryOptions.AddRange(categoryTask.Result
                .Select(x => new CategoryFilterOption(x.Id, $"{x.CategoryCode} - {x.CategoryName}"))
                .OrderBy(x => x.DisplayName));
            Categories = new ObservableCollection<CategoryFilterOption>(categoryOptions);
            SelectedCategory = CategoryFilterOption.All;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        await LoadInternalAsync(resetPage: true);
    }

    private async Task LoadInternalAsync(bool resetPage)
    {
        if (!CanRead)
        {
            Rows = new ObservableCollection<StockOnHandRow>();
            TotalCount = 0;
            SetError("Read permission is required.");
            return;
        }

        if (SelectedWarehouse is null)
        {
            Rows = new ObservableCollection<StockOnHandRow>();
            TotalCount = 0;
            SetError("Please select a warehouse.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "Loading on-hand inventory...");

            if (resetPage)
            {
                Page = 1;
            }

            var query = new SearchStockOnHandQuery
            {
                WarehouseId = SelectedWarehouse.Id,
                Keyword = Keyword,
                CategoryId = SelectedCategory?.Id,
                IncludeLocations = IncludeLocations,
                Page = Page,
                PageSize = PageSize,
                Sort = $"{SelectedSort}:{SelectedSortDirection}"
            };

            var result = await _inventoryQueryService.SearchStockOnHandAsync(query);
            Rows = new ObservableCollection<StockOnHandRow>(result.Items.Select(MapRow));
            TotalCount = result.TotalCount;
            Page = result.Page;
            PageSize = result.PageSize;

            if (Rows.Count == 0)
            {
                SetError("No on-hand rows matched the current filter.");
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

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SearchCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private static StockOnHandRow MapRow(StockOnHandDto dto)
    {
        return new StockOnHandRow(
            dto.ItemCode,
            dto.ItemName,
            dto.WarehouseCode,
            dto.LocationCode,
            dto.QtyOnHand,
            dto.UpdatedAtUtc);
    }

    public sealed record WarehouseFilterOption(Guid Id, string DisplayName);
    public sealed record CategoryFilterOption(Guid? Id, string DisplayName)
    {
        public static CategoryFilterOption All { get; } = new(null, "All");
    }

    public sealed record SortFieldOption(string Key, string DisplayName);
    public sealed record SortDirectionOption(string Key, string DisplayName);

    public sealed record StockOnHandRow(
        string ItemCode,
        string ItemName,
        string WarehouseCode,
        string? LocationCode,
        decimal QtyOnHand,
        DateTime UpdatedAtUtc);
}
