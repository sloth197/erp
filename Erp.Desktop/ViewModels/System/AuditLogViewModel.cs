using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.AuditRead)]
public sealed partial class AuditLogViewModel : ObservableObject
{
    private readonly IAuditLogQueryService _auditLogQueryService;

    [ObservableProperty]
    private DateTime? fromDate = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? toDate = DateTime.Today;

    [ObservableProperty]
    private string? actionKeyword;

    [ObservableProperty]
    private string? actorKeyword;

    [ObservableProperty]
    private string? txNoKeyword;

    [ObservableProperty]
    private ObservableCollection<AuditLogRow> rows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
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
    public bool CanRead { get; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public AuditLogViewModel(
        IAuditLogQueryService auditLogQueryService,
        ICurrentUserContext currentUserContext)
    {
        _auditLogQueryService = auditLogQueryService;
        CanRead = currentUserContext.HasPermission(PermissionCodes.AuditRead);
        _ = LoadInternalAsync(resetPage: true);
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

    private async Task LoadInternalAsync(bool resetPage)
    {
        if (!CanRead)
        {
            Rows = new ObservableCollection<AuditLogRow>();
            TotalCount = 0;
            StatusMessage = "Audit log read permission is required.";
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

            var fromUtc = FromDate?.Date.ToUniversalTime();
            var toUtc = ToDate?.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            {
                StatusMessage = "Invalid date range.";
                Rows = new ObservableCollection<AuditLogRow>();
                TotalCount = 0;
                return;
            }

            var query = new SearchAuditLogsQuery
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Action = ActionKeyword,
                Actor = ActorKeyword,
                Keyword = TxNoKeyword,
                Page = Page,
                PageSize = PageSize
            };

            var result = await _auditLogQueryService.SearchAuditLogsAsync(query);
            Rows = new ObservableCollection<AuditLogRow>(result.Items.Select(MapRow));
            TotalCount = result.TotalCount;
            Page = result.Page;
            PageSize = result.PageSize;
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

    private static AuditLogRow MapRow(AuditLogListDto dto)
    {
        return new AuditLogRow(
            dto.CreatedAtUtc,
            dto.Action,
            dto.ActorUsername,
            dto.Target,
            dto.DetailJson,
            dto.Ip);
    }

    public sealed record AuditLogRow(
        DateTime CreatedAtUtc,
        string Action,
        string? ActorUsername,
        string? Target,
        string? DetailJson,
        string? Ip);
}
