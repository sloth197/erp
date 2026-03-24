using System.Collections.ObjectModel;
using System.ComponentModel;
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
public sealed partial class ItemsViewModel : ViewModelBase
{
    private readonly IItemQueryService _itemQueryService;
    private readonly IItemCommandService _itemCommandService;
    private readonly IFileSaveDialogService _fileSaveDialogService;
    private readonly IItemCsvExportService _itemCsvExportService;
    private bool _lookupLoaded;

    [ObservableProperty]
    private ItemSearchCriteria searchCriteria = new();

    [ObservableProperty]
    private ObservableCollection<ItemCategoryFilterOption> categories = new();

    [ObservableProperty]
    private ObservableCollection<ItemCategoryFilterOption> editableCategories = new();

    [ObservableProperty]
    private ObservableCollection<ActiveFilterOption> activeFilters = new();

    [ObservableProperty]
    private ObservableCollection<TrackingTypeFilterOption> trackingFilters = new();

    [ObservableProperty]
    private ObservableCollection<UnitOfMeasureOption> unitOfMeasures = new();

    [ObservableProperty]
    private ObservableCollection<ItemRow> items = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    private ItemRow? selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    private ItemEditor? editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailHeader))]
    private bool isCreateMode;

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
    private string sortBy = "itemCode";

    [ObservableProperty]
    private string sortDirection = "asc";

    public ObservableCollection<int> PageSizes { get; } = new([20, 50, 100, 200]);
    public ObservableCollection<TrackingTypeEditOption> TrackingTypeEditOptions { get; } =
        new(
        [
            new TrackingTypeEditOption(TrackingType.None, "없음"),
            new TrackingTypeEditOption(TrackingType.Lot, "로트"),
            new TrackingTypeEditOption(TrackingType.Serial, "시리얼"),
            new TrackingTypeEditOption(TrackingType.Expiry, "유통기한")
        ]);

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool CanRead { get; }
    public bool CanWrite { get; }
    public bool CanExport { get; }
    public bool CanEditDetail => CanWrite && Editor is not null;
    public string DetailHeader => IsCreateMode ? "품목 상세 - 신규" : "품목 상세";
    public string ActiveToggleButtonText => SelectedItem?.IsActive == true ? "비활성화" : "활성화";
    public bool ShowEmptyState => !IsBusy && Items.Count == 0;

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

        TrackingFilters = new ObservableCollection<TrackingTypeFilterOption>
        {
            TrackingTypeFilterOption.All,
            new TrackingTypeFilterOption(TrackingType.None, "없음"),
            new TrackingTypeFilterOption(TrackingType.Lot, "로트"),
            new TrackingTypeFilterOption(TrackingType.Serial, "시리얼"),
            new TrackingTypeFilterOption(TrackingType.Expiry, "유통기한")
        };

        SearchCriteria.SelectedCategory = ItemCategoryFilterOption.All;
        SearchCriteria.Active = ActiveFilterOption.All;
        SearchCriteria.SelectedTrackingType = TrackingTypeFilterOption.All;

        _ = InitializeAsync();
    }

    partial void OnSelectedItemChanged(ItemRow? value)
    {
        OnPropertyChanged(nameof(ActiveToggleButtonText));

        if (value is not null && !IsCreateMode)
        {
            LoadEditorFromRow(value);
        }
    }

    partial void OnEditorChanged(ItemEditor? value)
    {
        OnPropertyChanged(nameof(CanEditDetail));
    }

    partial void OnItemsChanged(ObservableCollection<ItemRow> value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
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
        return !IsBusy && CanWrite && Editor is not null;
    }

    private bool CanCancelEdit()
    {
        return !IsBusy && CanWrite && Editor is not null;
    }

    private bool CanToggleActive()
    {
        return !IsBusy && CanWrite && !IsCreateMode && SelectedItem is not null;
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
    private async Task AddItemAsync()
    {
        await EnsureLookupOptionsLoadedAsync();

        var defaultCategory = EditableCategories.FirstOrDefault();
        var defaultUom = UnitOfMeasures.FirstOrDefault();

        if (defaultCategory is null || defaultUom is null)
        {
            SetError("기준 정보가 없습니다. 카테고리와 단위를 먼저 확인하세요.");
            return;
        }

        ClearValidationErrors();
        ClearUserMessage();

        IsCreateMode = true;
        SelectedItem = null;
        Editor = new ItemEditor
        {
            ItemCode = string.Empty,
            Name = string.Empty,
            Barcode = null,
            IsActive = true,
            SelectedCategory = defaultCategory,
            SelectedUnitOfMeasure = defaultUom,
            SelectedTrackingType = TrackingType.None,
            RowVersion = Array.Empty<byte>()
        };
    }

    [RelayCommand(CanExecute = nameof(CanSaveItem))]
    private async Task SaveItemAsync()
    {
        if (Editor is null)
        {
            return;
        }

        ClearValidationErrors();
        ClearUserMessage();

        if (!ValidateEditor(Editor))
        {
            SetError("입력 값을 확인해 주세요.");
            return;
        }

        try
        {
            SetBusy(true, "품목 저장 중...");

            if (IsCreateMode)
            {
                var createResult = await _itemCommandService.CreateItemAsync(new CreateItemCommand
                {
                    ItemCode = Editor.ItemCode.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(Editor.Barcode) ? null : Editor.Barcode.Trim(),
                    Name = Editor.Name.Trim(),
                    CategoryId = Editor.SelectedCategory!.Id!.Value,
                    UnitOfMeasureId = Editor.SelectedUnitOfMeasure!.Id,
                    TrackingType = Editor.SelectedTrackingType
                });

                IsCreateMode = false;
                await LoadInternalAsync(resetPage: false, preferredItemId: createResult.ItemId);
                SetSuccess("품목이 등록되었습니다.");
                return;
            }

            if (Editor.ItemId is null || Editor.RowVersion.Length == 0)
            {
                throw new InvalidOperationException("편집 상태가 올바르지 않습니다. 새로고침 후 다시 시도해 주세요.");
            }

            var updateResult = await _itemCommandService.UpdateItemAsync(new UpdateItemCommand
            {
                ItemId = Editor.ItemId.Value,
                RowVersion = Editor.RowVersion.ToArray(),
                ItemCode = Editor.ItemCode.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Editor.Barcode) ? null : Editor.Barcode.Trim(),
                Name = Editor.Name.Trim(),
                CategoryId = Editor.SelectedCategory!.Id!.Value,
                UnitOfMeasureId = Editor.SelectedUnitOfMeasure!.Id,
                TrackingType = Editor.SelectedTrackingType
            });

            await LoadInternalAsync(resetPage: false, preferredItemId: updateResult.ItemId);
            SetSuccess("품목이 저장되었습니다.");
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

    [RelayCommand(CanExecute = nameof(CanCancelEdit))]
    private void CancelEdit()
    {
        ClearValidationErrors();
        ClearUserMessage();

        if (IsCreateMode)
        {
            IsCreateMode = false;

            if (SelectedItem is not null)
            {
                LoadEditorFromRow(SelectedItem);
                return;
            }

            var firstRow = Items.FirstOrDefault();
            if (firstRow is not null)
            {
                SelectedItem = firstRow;
                return;
            }

            Editor = null;
            return;
        }

        if (SelectedItem is not null)
        {
            LoadEditorFromRow(SelectedItem);
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
            ClearUserMessage();
            SetBusy(true, "품목 상태 변경 중...");

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

            await LoadInternalAsync(resetPage: false, preferredItemId: SelectedItem.Id);
            SetSuccess("품목 상태가 변경되었습니다.");
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

    [RelayCommand(CanExecute = nameof(CanExportItems))]
    private async Task ExportCsvAsync()
    {
        if (!CanRead)
        {
            SetError("조회 권한이 필요합니다.");
            return;
        }

        if (!CanExport)
        {
            SetError("내보내기 권한이 필요합니다.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "CSV 내보내는 중...");

            var exportQuery = new ExportItemsQuery
            {
                Keyword = SearchCriteria.Keyword,
                CategoryId = SearchCriteria.SelectedCategory?.Id,
                IsActive = SearchCriteria.Active?.IsActive,
                TrackingType = SearchCriteria.SelectedTrackingType?.TrackingType,
                SortBy = SortBy,
                SortDirection = SortDirection
            };

            var rows = await _itemQueryService.ExportItemsAsync(exportQuery);
            if (rows.Count == 0)
            {
                SetError("현재 검색 조건과 일치하는 데이터가 없습니다.");
                return;
            }

            var filePath = _fileSaveDialogService.ShowCsvSaveDialog($"items_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                SetError("CSV 내보내기가 취소되었습니다.");
                return;
            }

            var csvContent = _itemCsvExportService.BuildCsv(rows);
            await File.WriteAllTextAsync(filePath, csvContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            SetSuccess($"CSV 내보내기 완료: {rows.Count:N0}건");
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

    [RelayCommand]
    private void SelectItemFromGrid(ItemRow? row)
    {
        if (row is null)
        {
            return;
        }

        IsCreateMode = false;
        SelectedItem = row;
    }

    public async Task ApplyGridSortAsync(string? sortMemberPath, ListSortDirection direction)
    {
        if (IsBusy || !CanRead || string.IsNullOrWhiteSpace(sortMemberPath))
        {
            return;
        }

        SortBy = sortMemberPath;
        SortDirection = direction == ListSortDirection.Descending ? "desc" : "asc";
        await LoadInternalAsync(resetPage: true);
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

    private async Task InitializeAsync()
    {
        await LoadInternalAsync(resetPage: true);
    }

    private async Task LoadInternalAsync(bool resetPage, Guid? preferredItemId = null)
    {
        if (!CanRead)
        {
            Items = new ObservableCollection<ItemRow>();
            TotalCount = 0;
            SetError("조회 권한이 필요합니다.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "품목 조회 중...");

            if (resetPage)
            {
                Page = 1;
            }

            await EnsureLookupOptionsLoadedAsync();

            var query = new SearchItemsQuery
            {
                Keyword = SearchCriteria.Keyword,
                CategoryId = SearchCriteria.SelectedCategory?.Id,
                IsActive = SearchCriteria.Active?.IsActive,
                TrackingType = SearchCriteria.SelectedTrackingType?.TrackingType,
                Page = Page,
                PageSize = PageSize,
                SortBy = SortBy,
                SortDirection = SortDirection
            };

            var result = await _itemQueryService.SearchItemsAsync(query);
            Items = new ObservableCollection<ItemRow>(result.Items.Select(MapRow));
            TotalCount = result.TotalCount;
            Page = result.Page;
            PageSize = result.PageSize;

            if (IsCreateMode)
            {
                return;
            }

            var selectionId = preferredItemId ?? SelectedItem?.Id;
            if (selectionId.HasValue)
            {
                var matched = Items.FirstOrDefault(x => x.Id == selectionId.Value);
                if (matched is not null)
                {
                    SelectedItem = matched;
                }
            }

            if (SelectedItem is null)
            {
                var firstRow = Items.FirstOrDefault();
                if (firstRow is not null)
                {
                    SelectedItem = firstRow;
                }
                else
                {
                    Editor = null;
                }
            }

            if (Items.Count == 0)
            {
                SetError("현재 검색 조건과 일치하는 품목이 없습니다.");
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

    private async Task EnsureLookupOptionsLoadedAsync()
    {
        if (_lookupLoaded)
        {
            return;
        }

        var categoryTask = _itemQueryService.GetItemCategoryOptionsAsync();
        var uomTask = _itemQueryService.GetUnitOfMeasureOptionsAsync();
        await Task.WhenAll(categoryTask, uomTask);

        var categoryRows = categoryTask.Result
            .Select(x => new ItemCategoryFilterOption(x.Id, $"{x.CategoryCode} - {x.CategoryName}"))
            .OrderBy(x => x.DisplayName)
            .ToList();

        var categoryOptions = new List<ItemCategoryFilterOption> { ItemCategoryFilterOption.All };
        categoryOptions.AddRange(categoryRows);
        Categories = new ObservableCollection<ItemCategoryFilterOption>(categoryOptions);
        EditableCategories = new ObservableCollection<ItemCategoryFilterOption>(categoryRows);

        UnitOfMeasures = new ObservableCollection<UnitOfMeasureOption>(uomTask.Result
            .Select(x => new UnitOfMeasureOption(x.Id, $"{x.UomCode} - {x.UomName}"))
            .OrderBy(x => x.DisplayName));

        SearchCriteria.SelectedCategory = Categories.FirstOrDefault() ?? ItemCategoryFilterOption.All;
        SearchCriteria.Active ??= ActiveFilterOption.All;
        SearchCriteria.SelectedTrackingType ??= TrackingTypeFilterOption.All;

        _lookupLoaded = true;
    }

    private void LoadEditorFromRow(ItemRow row)
    {
        var category = EditableCategories.FirstOrDefault(x => x.Id == row.CategoryId)
            ?? new ItemCategoryFilterOption(row.CategoryId, row.CategoryDisplay);

        var uom = UnitOfMeasures.FirstOrDefault(x => x.Id == row.UnitOfMeasureId)
            ?? new UnitOfMeasureOption(row.UnitOfMeasureId, $"{row.UnitOfMeasureCode} - {row.UnitOfMeasureName}");

        Editor = new ItemEditor
        {
            ItemId = row.Id,
            RowVersion = row.RowVersion.ToArray(),
            ItemCode = row.ItemCode,
            Name = row.Name,
            Barcode = row.Barcode,
            IsActive = row.IsActive,
            SelectedCategory = category,
            SelectedUnitOfMeasure = uom,
            SelectedTrackingType = row.TrackingType
        };
    }

    private bool ValidateEditor(ItemEditor editor)
    {
        if (editor is null)
        {
            AddValidationError("품목 편집 정보가 초기화되지 않았습니다.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(editor.ItemCode))
        {
            AddValidationError("Item Code는 필수입니다.");
        }
        else if (editor.ItemCode.Trim().Length > 50)
        {
            AddValidationError("Item Code는 50자 이하여야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(editor.Name))
        {
            AddValidationError("품목명은 필수입니다.");
        }
        else if (editor.Name.Trim().Length > 200)
        {
            AddValidationError("품목명은 200자 이하여야 합니다.");
        }

        if (!string.IsNullOrWhiteSpace(editor.Barcode) && editor.Barcode.Trim().Length > 100)
        {
            AddValidationError("바코드는 100자 이하여야 합니다.");
        }

        if (editor.SelectedCategory?.Id is null)
        {
            AddValidationError("카테고리는 필수입니다.");
        }

        if (editor.SelectedUnitOfMeasure is null)
        {
            AddValidationError("단위는 필수입니다.");
        }

        return !HasValidationErrors;
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        LoadCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        AddItemCommand.NotifyCanExecuteChanged();
        SaveItemCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState));
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
            dto.UnitOfMeasureName,
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
        string UnitOfMeasureName,
        string? Barcode,
        decimal Price,
        byte[] RowVersion,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc)
    {
        public string TrackingTypeDisplay => TrackingType switch
        {
            TrackingType.None => "없음",
            TrackingType.Lot => "로트",
            TrackingType.Serial => "시리얼",
            TrackingType.Expiry => "유통기한",
            _ => TrackingType.ToString()
        };
    }

    public sealed record ItemCategoryFilterOption(Guid? Id, string DisplayName)
    {
        public static ItemCategoryFilterOption All { get; } = new(null, "전체");
    }

    public sealed record UnitOfMeasureOption(Guid Id, string DisplayName);

    public sealed record ActiveFilterOption(bool? IsActive, string DisplayName)
    {
        public static ActiveFilterOption All { get; } = new(null, "전체");
        public static ActiveFilterOption ActiveOnly { get; } = new(true, "활성");
        public static ActiveFilterOption InactiveOnly { get; } = new(false, "비활성");
    }

    public sealed record TrackingTypeFilterOption(TrackingType? TrackingType, string DisplayName)
    {
        public static TrackingTypeFilterOption All { get; } = new(null, "전체");
    }

    public sealed record TrackingTypeEditOption(TrackingType Value, string DisplayName);

    public sealed partial class ItemSearchCriteria : ObservableObject
    {
        [ObservableProperty]
        private string? keyword;

        [ObservableProperty]
        private ItemCategoryFilterOption? selectedCategory;

        [ObservableProperty]
        private ActiveFilterOption? active;

        [ObservableProperty]
        private TrackingTypeFilterOption? selectedTrackingType;
    }

    public sealed partial class ItemEditor : ObservableObject
    {
        [ObservableProperty]
        private Guid? itemId;

        [ObservableProperty]
        private byte[] rowVersion = Array.Empty<byte>();

        [ObservableProperty]
        private string itemCode = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string? barcode;

        [ObservableProperty]
        private bool isActive = true;

        [ObservableProperty]
        private ItemCategoryFilterOption? selectedCategory;

        [ObservableProperty]
        private UnitOfMeasureOption? selectedUnitOfMeasure;

        [ObservableProperty]
        private TrackingType selectedTrackingType = TrackingType.None;
    }
}
