using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.MasterItemsRead)]
public sealed partial class ItemsViewModel : ObservableObject
{
    private readonly IItemQueryService _itemQueryService;
    private bool _categoriesLoaded;

    [ObservableProperty]
    private ItemSearchCriteria searchCriteria = new();

    [ObservableProperty]
    private ObservableCollection<ItemCategoryFilterOption> categories = new();

    [ObservableProperty]
    private ObservableCollection<ActiveFilterOption> activeFilters = new();

    [ObservableProperty]
    private ObservableCollection<ItemRow> items = new();

    [ObservableProperty]
    private ItemRow? selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int page = 1;

    [ObservableProperty]
    private int pageSize = 50;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int totalCount;

    [ObservableProperty]
    private string? statusMessage;

    public ObservableCollection<int> PageSizes { get; } = new([20, 50, 100, 200]);

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public ItemsViewModel(IItemQueryService itemQueryService)
    {
        _itemQueryService = itemQueryService;

        Categories = new ObservableCollection<ItemCategoryFilterOption>
        {
            ItemCategoryFilterOption.All
        };

        ActiveFilters = new ObservableCollection<ActiveFilterOption>
        {
            ActiveFilterOption.All,
            ActiveFilterOption.ActiveOnly,
            ActiveFilterOption.InactiveOnly
        };

        SearchCriteria.SelectedCategory = ItemCategoryFilterOption.All;
        SearchCriteria.Active = ActiveFilterOption.All;

        _ = LoadAsync();
    }

    private bool CanLoad()
    {
        return !IsBusy;
    }

    private bool CanGoPreviousPage()
    {
        return !IsBusy && Page > 1;
    }

    private bool CanGoNextPage()
    {
        return !IsBusy && Page < TotalPages;
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync()
    {
        await LoadInternalAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task SearchAsync()
    {
        await LoadInternalAsync(resetPage: true);
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private async Task PreviousPageAsync()
    {
        if (Page <= 1)
        {
            return;
        }

        Page--;
        await LoadInternalAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private async Task NextPageAsync()
    {
        if (Page >= TotalPages)
        {
            return;
        }

        Page++;
        await LoadInternalAsync(resetPage: false);
    }

    [RelayCommand]
    private void SelectItemFromGrid(ItemRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedItem = row;
    }

    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
    }

    partial void OnPageSizeChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));

        if (value <= 0)
        {
            PageSize = 50;
            return;
        }

        if (!IsBusy)
        {
            _ = LoadInternalAsync(resetPage: true);
        }
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
    }

    private async Task LoadInternalAsync(bool resetPage)
    {
        try
        {
            IsBusy = true;
            StatusMessage = null;

            if (resetPage)
            {
                Page = 1;
            }

            await EnsureCategoryOptionsLoadedAsync();

            var query = new SearchItemsQuery
            {
                Keyword = SearchCriteria.Keyword,
                CategoryId = SearchCriteria.SelectedCategory?.Id,
                IsActive = SearchCriteria.Active?.IsActive,
                Page = Page,
                PageSize = PageSize,
                SortBy = "itemCode",
                SortDirection = "asc"
            };

            var result = await _itemQueryService.SearchItemsAsync(query);
            Items = new ObservableCollection<ItemRow>(result.Items.Select(MapRow));
            TotalCount = result.TotalCount;
            Page = result.Page;
            PageSize = result.PageSize;

            if (SelectedItem is not null)
            {
                SelectedItem = Items.FirstOrDefault(x => x.Id == SelectedItem.Id);
            }
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

    private async Task EnsureCategoryOptionsLoadedAsync()
    {
        if (_categoriesLoaded)
        {
            return;
        }

        var categoryDtos = await _itemQueryService.GetItemCategoryOptionsAsync();
        var options = categoryDtos
            .Select(x => new ItemCategoryFilterOption(x.Id, $"{x.CategoryCode} - {x.CategoryName}"))
            .OrderBy(x => x.DisplayName)
            .ToList();

        var categoryOptions = new List<ItemCategoryFilterOption> { ItemCategoryFilterOption.All };
        categoryOptions.AddRange(options);
        Categories = new ObservableCollection<ItemCategoryFilterOption>(categoryOptions);
        _categoriesLoaded = true;
    }

    private static ItemRow MapRow(Erp.Application.DTOs.ItemListDto dto)
    {
        return new ItemRow(
            dto.Id,
            dto.ItemCode,
            dto.Name,
            $"{dto.CategoryCode} - {dto.CategoryName}",
            dto.IsActive,
            dto.TrackingType.ToString(),
            dto.UnitOfMeasureCode,
            dto.Barcode,
            0m,
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc);
    }

    public sealed record ItemRow(
        Guid Id,
        string ItemCode,
        string Name,
        string CategoryDisplay,
        bool IsActive,
        string TrackingType,
        string UnitOfMeasureCode,
        string? Barcode,
        decimal Price,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    public sealed record ItemCategoryFilterOption(Guid? Id, string DisplayName)
    {
        public static ItemCategoryFilterOption All { get; } = new(null, "All");
    }

    public sealed record ActiveFilterOption(bool? IsActive, string DisplayName)
    {
        public static ActiveFilterOption All { get; } = new(null, "All");
        public static ActiveFilterOption ActiveOnly { get; } = new(true, "Active");
        public static ActiveFilterOption InactiveOnly { get; } = new(false, "Inactive");
    }

    public sealed partial class ItemSearchCriteria : ObservableObject
    {
        [ObservableProperty]
        private string? keyword;

        [ObservableProperty]
        private ItemCategoryFilterOption? selectedCategory;

        [ObservableProperty]
        private ActiveFilterOption? active;
    }
}
