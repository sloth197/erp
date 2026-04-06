using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.InventoryStockRead)]
public sealed partial class InventoryOnHandViewModel : ViewModelBase
{
    private readonly IInventoryQueryService _inventoryQueryService;
    private readonly IInventoryCommandService _inventoryCommandService;
    private readonly IItemQueryService _itemQueryService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<WarehouseFilterOption> warehouses = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCountAdjustmentCommand))]
    private WarehouseFilterOption? selectedWarehouse;

    [ObservableProperty]
    private ObservableCollection<CategoryFilterOption> categories = new();

    [ObservableProperty]
    private CategoryFilterOption? selectedCategory;

    [ObservableProperty]
    private string? keyword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCountAdjustmentCommand))]
    private bool includeLocations = true;

    [ObservableProperty]
    private string selectedSort = "itemcode";

    [ObservableProperty]
    private string selectedSortDirection = "asc";

    [ObservableProperty]
    private ObservableCollection<StockOnHandRow> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCountAdjustmentCommand))]
    private StockOnHandRow? selectedRow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCountAdjustmentCommand))]
    private string countedQtyInput = string.Empty;

    [ObservableProperty]
    private string adjustNote = string.Empty;

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
            new SortFieldOption("itemname", "품목명"),
            new SortFieldOption("qtyonhand", "재고수량"),
            new SortFieldOption("warehousecode", "창고"),
            new SortFieldOption("locationcode", "로케이션"),
            new SortFieldOption("updatedatutc", "수정일시")
        ]);

    public ObservableCollection<SortDirectionOption> SortDirections { get; } =
        new(
        [
            new SortDirectionOption("asc", "오름차순"),
            new SortDirectionOption("desc", "내림차순")
        ]);

    public bool CanRead { get; }
    public bool CanReceiptManage { get; }
    public bool CanIssueManage { get; }
    public bool CanAdjustManage { get; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool ShowEmptyState => !IsBusy && Rows.Count == 0;
    public string SelectedRowSummary => SelectedRow is null
        ? "선택된 재고 행이 없습니다."
        : $"{SelectedRow.ItemCode} / {SelectedRow.ItemName} / 로케이션: {SelectedRow.LocationCode ?? "(공통)"} / 현재수량: {SelectedRow.QtyOnHand:N4}";

    public InventoryOnHandViewModel(
        IInventoryQueryService inventoryQueryService,
        IInventoryCommandService inventoryCommandService,
        IItemQueryService itemQueryService,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext)
    {
        _inventoryQueryService = inventoryQueryService;
        _inventoryCommandService = inventoryCommandService;
        _itemQueryService = itemQueryService;
        _navigationService = navigationService;
        CanRead = currentUserContext.HasPermission(PermissionCodes.InventoryStockRead);
        CanReceiptManage = currentUserContext.HasPermission(PermissionCodes.InventoryStockReceipt);
        CanIssueManage = currentUserContext.HasPermission(PermissionCodes.InventoryStockIssue);
        CanAdjustManage = currentUserContext.HasPermission(PermissionCodes.InventoryStockAdjust);
        _ = InitializeAsync();
    }

    partial void OnRowsChanged(ObservableCollection<StockOnHandRow> value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSelectedRowChanged(StockOnHandRow? value)
    {
        OnPropertyChanged(nameof(SelectedRowSummary));

        if (value is null)
        {
            CountedQtyInput = string.Empty;
            return;
        }

        CountedQtyInput = value.QtyOnHand.ToString("0.####", CultureInfo.CurrentCulture);
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

    private bool CanOpenStockReceipt()
    {
        return !IsBusy && CanReceiptManage;
    }

    private bool CanOpenStockIssue()
    {
        return !IsBusy && CanIssueManage;
    }

    private bool CanApplyCountAdjustment()
    {
        if (IsBusy || !CanAdjustManage || SelectedWarehouse is null || SelectedRow is null || !IncludeLocations)
        {
            return false;
        }

        return TryParseNonNegativeQty(CountedQtyInput, out _);
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

    [RelayCommand(CanExecute = nameof(CanOpenStockReceipt))]
    private void OpenStockReceipt()
    {
        _navigationService.NavigateTo<StockReceiptViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanOpenStockIssue))]
    private void OpenStockIssue()
    {
        _navigationService.NavigateTo<StockIssueViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanApplyCountAdjustment))]
    private async Task ApplyCountAdjustmentAsync()
    {
        if (SelectedWarehouse is null || SelectedRow is null)
        {
            return;
        }

        if (!IncludeLocations)
        {
            SetError("실사 조정은 '로케이션 포함' 조회에서만 지원됩니다.");
            return;
        }

        if (!TryParseNonNegativeQty(CountedQtyInput, out var countedQty))
        {
            SetError("실사수량은 0 이상의 숫자여야 합니다.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "실사 조정 반영 중...");

            var itemId = await ResolveItemIdAsync(SelectedRow.ItemCode);
            var locationId = await ResolveLocationIdAsync(SelectedWarehouse.Id, SelectedRow.LocationCode);
            var note = string.IsNullOrWhiteSpace(AdjustNote)
                ? $"재고조회 화면 실사 조정 ({SelectedRow.ItemCode})"
                : AdjustNote.Trim();

            var result = await _inventoryCommandService.AdjustStockByCountAsync(new AdjustStockByCountCommand
            {
                WarehouseId = SelectedWarehouse.Id,
                LocationId = locationId,
                Lines =
                [
                    new AdjustStockByCountLineCommand
                    {
                        ItemId = itemId,
                        CountedQty = countedQty,
                        Note = note
                    }
                ]
            });

            SetSuccess($"실사 조정 완료: {result.TxNo}");
            await LoadInternalAsync(resetPage: false);
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
            SetBusy(true, "재고 필터 로딩 중...");

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
            SelectedRow = null;
            SetError("조회 권한이 필요합니다.");
            return;
        }

        if (SelectedWarehouse is null)
        {
            Rows = new ObservableCollection<StockOnHandRow>();
            TotalCount = 0;
            SelectedRow = null;
            SetError("창고를 선택해 주세요.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "재고 조회 중...");

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
            SelectedRow = Rows.FirstOrDefault();

            if (Rows.Count == 0)
            {
                SetError("현재 검색 조건과 일치하는 재고가 없습니다.");
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

    private async Task<Guid> ResolveItemIdAsync(string itemCode)
    {
        var candidates = await _inventoryQueryService.SearchItemOptionsAsync(
            keyword: itemCode,
            take: 50,
            activeOnly: false);

        var matches = candidates
            .Where(x => string.Equals(x.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"품목 '{itemCode}'를 찾을 수 없습니다.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"품목 코드 '{itemCode}'가 중복되어 실사 조정을 진행할 수 없습니다.");
        }

        return matches[0].Id;
    }

    private async Task<Guid?> ResolveLocationIdAsync(Guid warehouseId, string? locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            return null;
        }

        var locations = await _inventoryQueryService.GetLocationOptionsAsync(
            warehouseId: warehouseId,
            activeOnly: false);

        var match = locations.FirstOrDefault(x =>
            string.Equals(x.Code, locationCode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new InvalidOperationException($"로케이션 '{locationCode}'를 찾을 수 없습니다.");
        }

        return match.Id;
    }

    private static bool TryParseNonNegativeQty(string? input, out decimal qty)
    {
        var hasValue = decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out qty) ||
                       decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out qty);

        return hasValue && qty >= 0m;
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SearchCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        OpenStockReceiptCommand.NotifyCanExecuteChanged();
        OpenStockIssueCommand.NotifyCanExecuteChanged();
        ApplyCountAdjustmentCommand.NotifyCanExecuteChanged();
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
        public static CategoryFilterOption All { get; } = new(null, "전체");
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
