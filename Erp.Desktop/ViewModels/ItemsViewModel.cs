using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Desktop.Navigation;
using Erp.Desktop.Services;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.MasterItemsRead)]
public sealed partial class ItemsViewModel : ObservableObject
{
    private readonly IItemQueryService _itemQueryService;
    private readonly IItemCommandService _itemCommandService;
    private readonly IFileSaveDialogService _fileSaveDialogService;
    private readonly IItemCsvExportService _itemCsvExportService;
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
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    private ItemRow? selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
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
    public bool CanRead { get; }
    public bool CanWrite { get; }
    public bool CanExport { get; }
    public string ActiveToggleButtonText => SelectedItem?.IsActive == true ? "Deactivate" : "Activate";

    public ItemsViewModel(
        IItemQueryService itemQueryService,
        IItemCommandService itemCommandService,
        IFileSaveDialogService fileSaveDialogService,
        IItemCsvExportService itemCsvExportService,
        ICurrentUserContext currentUserContext)
    {
        _itemQueryService = itemQueryService;
        _itemCommandService = itemCommandService;
        _fileSaveDialogService = fileSaveDialogService;
        _itemCsvExportService = itemCsvExportService;

        CanRead = currentUserContext.HasPermission(PermissionCodes.MasterItemsRead);
        CanWrite = currentUserContext.HasPermission(PermissionCodes.MasterItemsWrite);
        CanExport = CanWrite || currentUserContext.HasPermission(PermissionCodes.MasterItemsExport);

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

    partial void OnSelectedItemChanged(ItemRow? value)
    {
        OnPropertyChanged(nameof(ActiveToggleButtonText));
    }

    private bool CanLoad()
    {
        return !IsBusy && CanRead;
    }

    private bool CanGoPreviousPage()
    {
        return !IsBusy && CanRead && Page > 1;
    }

    private bool CanGoNextPage()
    {
        return !IsBusy && CanRead && Page < TotalPages;
    }

    private bool CanAddItem()
    {
        return !IsBusy && CanWrite;
    }

    private bool CanSaveItem()
    {
        return !IsBusy && CanWrite && SelectedItem is not null;
    }

    private bool CanToggleActive()
    {
        return !IsBusy && CanWrite && SelectedItem is not null;
    }

    private bool CanExportItems()
    {
        return !IsBusy && CanRead && CanExport;
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

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        StatusMessage = "Add flow will be completed in the next step.";
    }

    [RelayCommand(CanExecute = nameof(CanSaveItem))]
    private async Task SaveItemAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _itemCommandService.UpdateItemAsync(new UpdateItemCommand
            {
                ItemId = SelectedItem.Id,
                RowVersion = SelectedItem.RowVersion.ToArray(),
                ItemCode = SelectedItem.ItemCode,
                Barcode = SelectedItem.Barcode,
                Name = SelectedItem.Name,
                CategoryId = SelectedItem.CategoryId,
                UnitOfMeasureId = SelectedItem.UnitOfMeasureId,
                TrackingType = SelectedItem.TrackingType
            });

            await LoadInternalAsync(resetPage: false);
            StatusMessage = "Item saved.";
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

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            if (SelectedItem.IsActive)
            {
                await _itemCommandService.DeactivateItemAsync(new DeactivateItemCommand
                {
                    ItemId = SelectedItem.Id
                });
            }
            else
            {
                await _itemCommandService.ActivateItemAsync(new ActivateItemCommand
                {
                    ItemId = SelectedItem.Id
                });
            }

            await LoadInternalAsync(resetPage: false);
            StatusMessage = "Item status updated.";
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

    [RelayCommand(CanExecute = nameof(CanExportItems))]
    private async Task ExportCsvAsync()
    {
        if (!CanRead)
        {
            StatusMessage = "Read permission is required.";
            return;
        }

        if (!CanExport)
        {
            StatusMessage = "Export permission is required.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            var exportQuery = new ExportItemsQuery
            {
                Keyword = SearchCriteria.Keyword,
                CategoryId = SearchCriteria.SelectedCategory?.Id,
                IsActive = SearchCriteria.Active?.IsActive,
                SortBy = "itemCode",
                SortDirection = "asc"
            };

            var rows = await _itemQueryService.ExportItemsAsync(exportQuery);
            if (rows.Count == 0)
            {
                StatusMessage = "No rows matched the current filter.";
                return;
            }

            var filePath = _fileSaveDialogService.ShowCsvSaveDialog($"items_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = "CSV export cancelled.";
                return;
            }

            var csvContent = _itemCsvExportService.BuildCsv(rows);
            await File.WriteAllTextAsync(filePath, csvContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            StatusMessage = $"CSV exported: {rows.Count:N0} rows.";
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
        if (!CanRead)
        {
            Items = new ObservableCollection<ItemRow>();
            TotalCount = 0;
            StatusMessage = "Read permission is required.";
            return;
        }

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
            dto.CategoryId,
            $"{dto.CategoryCode} - {dto.CategoryName}",
            dto.IsActive,
            dto.TrackingType,
            dto.UnitOfMeasureId,
            dto.UnitOfMeasureCode,
            dto.Barcode,
            0m,
            dto.RowVersion.ToArray(),
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc);
    }

    public sealed record ItemRow(
        Guid Id,
        string ItemCode,
        string Name,
        Guid CategoryId,
        string CategoryDisplay,
        bool IsActive,
        TrackingType TrackingType,
        Guid UnitOfMeasureId,
        string UnitOfMeasureCode,
        string? Barcode,
        decimal Price,
        byte[] RowVersion,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc)
    {
        public string TrackingTypeDisplay => TrackingType.ToString();
    }

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
