using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;
using Erp.Domain.Entities;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.NoticeRead)]
public sealed partial class NoticesViewModel : ViewModelBase
{
    private readonly string _currentUsername;
    private readonly List<NoticeItem> _noticeStore = [];

    [ObservableProperty]
    private NoticeSearchCriteria searchCriteria = new();

    [ObservableProperty]
    private ObservableCollection<NoticeStatusFilterOption> statusFilters = [];

    [ObservableProperty]
    private ObservableCollection<NoticePriorityFilterOption> priorityFilters = [];

    [ObservableProperty]
    private ObservableCollection<NoticePriorityOption> priorityEditOptions = [];

    [ObservableProperty]
    private ObservableCollection<NoticeRow> notices = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishSelectedNoticeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPublishSelectedNoticeCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkSelectedAsReadCommand))]
    private NoticeRow? selectedNotice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveNoticeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    private NoticeEditor? editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailHeader))]
    private bool isCreateMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int page = 1;

    [ObservableProperty]
    private int pageSize = 20;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int totalCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarkAllAsReadCommand))]
    private int unreadCount;

    public ObservableCollection<int> PageSizes { get; } = new([10, 20, 50, 100]);
    public bool CanRead { get; }
    public bool CanWrite { get; }
    public bool CanPublish { get; }
    public string DetailHeader => IsCreateMode ? "공지 작성" : "공지 상세";
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public string UnreadSummary => $"미읽음 {UnreadCount:N0}";
    public string PermissionSummary => $"Read: {CanRead} / Write: {CanWrite} / Publish: {CanPublish}";

    public NoticesViewModel(ICurrentUserContext currentUserContext)
    {
        _currentUsername = string.IsNullOrWhiteSpace(currentUserContext.Username)
            ? "current-user"
            : currentUserContext.Username.Trim();

        var currentJobGrade = currentUserContext.JobGrade ?? UserJobGrade.Staff;
        var canComposeByGrade = currentJobGrade >= UserJobGrade.Manager;

        CanRead = currentUserContext.HasPermission(PermissionCodes.NoticeRead);
        CanWrite = canComposeByGrade;
        CanPublish = currentUserContext.HasPermission(PermissionCodes.NoticePublish) && canComposeByGrade;

        StatusFilters = new ObservableCollection<NoticeStatusFilterOption>
        {
            NoticeStatusFilterOption.All,
            new NoticeStatusFilterOption(NoticeStatus.Draft, "임시저장"),
            new NoticeStatusFilterOption(NoticeStatus.Published, "게시중"),
            new NoticeStatusFilterOption(NoticeStatus.Ended, "게시종료")
        };

        PriorityFilters = new ObservableCollection<NoticePriorityFilterOption>
        {
            NoticePriorityFilterOption.All,
            new NoticePriorityFilterOption(NoticePriority.Normal, "일반"),
            new NoticePriorityFilterOption(NoticePriority.Important, "중요"),
            new NoticePriorityFilterOption(NoticePriority.Critical, "긴급")
        };

        PriorityEditOptions = new ObservableCollection<NoticePriorityOption>
        {
            new NoticePriorityOption(NoticePriority.Normal, "일반"),
            new NoticePriorityOption(NoticePriority.Important, "중요"),
            new NoticePriorityOption(NoticePriority.Critical, "긴급")
        };

        SearchCriteria.SelectedStatus = StatusFilters.FirstOrDefault() ?? NoticeStatusFilterOption.All;
        SearchCriteria.SelectedPriority = PriorityFilters.FirstOrDefault() ?? NoticePriorityFilterOption.All;

        SeedSampleData();
        _ = LoadInternalAsync(resetPage: true);
    }

    partial void OnSelectedNoticeChanged(NoticeRow? value)
    {
        if (value is null)
        {
            if (!IsCreateMode)
            {
                Editor = null;
            }

            return;
        }

        if (!IsCreateMode)
        {
            LoadEditorFromRow(value);
        }
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(UnreadSummary));
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
            PageSize = 20;
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

    private bool CanSearch()
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

    private bool CanAddNotice()
    {
        return !IsBusy && CanWrite;
    }

    private bool CanSaveNotice()
    {
        return !IsBusy && CanWrite && Editor is not null;
    }

    private bool CanCancelEdit()
    {
        return !IsBusy && CanWrite && Editor is not null;
    }

    private bool CanPublishSelectedNotice()
    {
        return !IsBusy &&
               CanPublish &&
               SelectedNotice is not null &&
               SelectedNotice.Status != NoticeStatus.Published;
    }

    private bool CanStopPublishSelectedNotice()
    {
        return !IsBusy &&
               CanPublish &&
               SelectedNotice is not null &&
               SelectedNotice.Status == NoticeStatus.Published;
    }

    private bool CanMarkSelectedAsRead()
    {
        return !IsBusy &&
               CanRead &&
               SelectedNotice is not null &&
               !SelectedNotice.IsRead;
    }

    private bool CanMarkAllAsRead()
    {
        return !IsBusy && CanRead && UnreadCount > 0;
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

    [RelayCommand(CanExecute = nameof(CanAddNotice))]
    private Task AddNoticeAsync()
    {
        ClearUserMessage();
        ClearValidationErrors();

        IsCreateMode = true;
        SelectedNotice = null;
        Editor = new NoticeEditor
        {
            NoticeId = null,
            Title = string.Empty,
            Body = string.Empty,
            SelectedPriority = PriorityEditOptions.FirstOrDefault(),
            IsPinned = false,
            PublishFromDate = DateTime.Today,
            PublishToDate = DateTime.Today.AddDays(7)
        };

        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanSaveNotice))]
    private async Task SaveNoticeAsync()
    {
        if (Editor is null)
        {
            return;
        }

        ClearUserMessage();
        ClearValidationErrors();

        if (!ValidateEditor(Editor))
        {
            SetError("필수값 또는 입력 형식을 확인해 주세요.");
            return;
        }

        try
        {
            SetBusy(true, "공지를 저장하는 중...");
            await Task.Delay(80);

            var now = DateTime.Now;
            if (IsCreateMode)
            {
                var created = new NoticeItem
                {
                    Id = Guid.NewGuid(),
                    Title = Editor.Title.Trim(),
                    Body = Editor.Body.Trim(),
                    Priority = Editor.SelectedPriority!.Priority,
                    Status = NoticeStatus.Draft,
                    IsPinned = Editor.IsPinned,
                    PublishFromDate = Editor.PublishFromDate?.Date,
                    PublishToDate = Editor.PublishToDate?.Date,
                    CreatedBy = _currentUsername,
                    CreatedAtLocal = now,
                    UpdatedAtLocal = now
                };

                created.ReadByUsers.Add(_currentUsername);
                _noticeStore.Add(created);

                IsCreateMode = false;
                await LoadInternalAsync(resetPage: false, preferredNoticeId: created.Id);
                SetSuccess("공지 초안을 저장했습니다.");
                return;
            }

            var target = _noticeStore.FirstOrDefault(x => x.Id == Editor.NoticeId);
            if (target is null)
            {
                SetError("선택한 공지를 찾을 수 없습니다. 다시 조회해 주세요.");
                return;
            }

            target.Title = Editor.Title.Trim();
            target.Body = Editor.Body.Trim();
            target.Priority = Editor.SelectedPriority!.Priority;
            target.IsPinned = Editor.IsPinned;
            target.PublishFromDate = Editor.PublishFromDate?.Date;
            target.PublishToDate = Editor.PublishToDate?.Date;
            target.UpdatedAtLocal = now;

            await LoadInternalAsync(resetPage: false, preferredNoticeId: target.Id);
            SetSuccess("공지 내용을 저장했습니다.");
        }
        catch (Exception ex)
        {
            SetError($"공지 저장 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelEdit))]
    private void CancelEdit()
    {
        ClearUserMessage();
        ClearValidationErrors();

        if (IsCreateMode)
        {
            IsCreateMode = false;

            var first = Notices.FirstOrDefault();
            if (first is not null)
            {
                SelectedNotice = first;
            }
            else
            {
                Editor = null;
            }

            return;
        }

        if (SelectedNotice is not null)
        {
            LoadEditorFromRow(SelectedNotice);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPublishSelectedNotice))]
    private async Task PublishSelectedNoticeAsync()
    {
        if (SelectedNotice is null)
        {
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "공지를 게시하는 중...");
            await Task.Delay(80);

            var target = _noticeStore.FirstOrDefault(x => x.Id == SelectedNotice.Id);
            if (target is null)
            {
                SetError("선택한 공지를 찾을 수 없습니다. 다시 조회해 주세요.");
                return;
            }

            if (target.PublishFromDate.HasValue && target.PublishToDate.HasValue && target.PublishFromDate > target.PublishToDate)
            {
                SetError("게시 시작일은 종료일보다 늦을 수 없습니다.");
                return;
            }

            target.Status = NoticeStatus.Published;
            target.PublishFromDate ??= DateTime.Today;
            target.UpdatedAtLocal = DateTime.Now;

            await LoadInternalAsync(resetPage: false, preferredNoticeId: target.Id);
            SetSuccess("공지를 게시했습니다.");
        }
        catch (Exception ex)
        {
            SetError($"게시 처리 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopPublishSelectedNotice))]
    private async Task StopPublishSelectedNoticeAsync()
    {
        if (SelectedNotice is null)
        {
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "게시를 중지하는 중...");
            await Task.Delay(80);

            var target = _noticeStore.FirstOrDefault(x => x.Id == SelectedNotice.Id);
            if (target is null)
            {
                SetError("선택한 공지를 찾을 수 없습니다. 다시 조회해 주세요.");
                return;
            }

            target.Status = NoticeStatus.Ended;
            target.UpdatedAtLocal = DateTime.Now;

            await LoadInternalAsync(resetPage: false, preferredNoticeId: target.Id);
            SetSuccess("게시를 중지했습니다.");
        }
        catch (Exception ex)
        {
            SetError($"게시중지 처리 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMarkSelectedAsRead))]
    private async Task MarkSelectedAsReadAsync()
    {
        if (SelectedNotice is null)
        {
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "읽음 상태를 반영하는 중...");
            await Task.Delay(80);

            var target = _noticeStore.FirstOrDefault(x => x.Id == SelectedNotice.Id);
            if (target is null)
            {
                SetError("선택한 공지를 찾을 수 없습니다. 다시 조회해 주세요.");
                return;
            }

            target.ReadByUsers.Add(_currentUsername);
            await LoadInternalAsync(resetPage: false, preferredNoticeId: target.Id);
            SetSuccess("선택 공지를 읽음 처리했습니다.");
        }
        catch (Exception ex)
        {
            SetError($"읽음 처리 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMarkAllAsRead))]
    private async Task MarkAllAsReadAsync()
    {
        try
        {
            ClearUserMessage();
            SetBusy(true, "전체 읽음 처리 중...");
            await Task.Delay(80);

            var changed = 0;
            foreach (var notice in _noticeStore.Where(x => x.Status == NoticeStatus.Published))
            {
                if (notice.ReadByUsers.Add(_currentUsername))
                {
                    changed++;
                }
            }

            await LoadInternalAsync(resetPage: false);

            if (changed == 0)
            {
                SetSuccess("이미 모든 공지를 읽었습니다.");
            }
            else
            {
                SetSuccess($"{changed:N0}건을 읽음 처리했습니다.");
            }
        }
        catch (Exception ex)
        {
            SetError($"전체 읽음 처리 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadInternalAsync(bool resetPage, Guid? preferredNoticeId = null)
    {
        if (!CanRead)
        {
            Notices = [];
            TotalCount = 0;
            Editor = null;
            SetError("알림/공지 조회 권한이 필요합니다.");
            return;
        }

        try
        {
            ClearUserMessage();
            SetBusy(true, "알림/공지 데이터를 불러오는 중...");
            await Task.Delay(80);

            if (resetPage)
            {
                Page = 1;
            }

            var filtered = ApplyFilter();
            TotalCount = filtered.Count;

            if (Page < 1)
            {
                Page = 1;
            }

            if (TotalPages > 0 && Page > TotalPages)
            {
                Page = TotalPages;
            }

            var pageRows = filtered
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .Select(MapRow)
                .ToList();

            Notices = new ObservableCollection<NoticeRow>(pageRows);

            if (!IsCreateMode)
            {
                var selectedId = preferredNoticeId ?? SelectedNotice?.Id;
                if (selectedId.HasValue)
                {
                    var matched = Notices.FirstOrDefault(x => x.Id == selectedId.Value);
                    if (matched is not null)
                    {
                        SelectedNotice = matched;
                    }
                }

                if (SelectedNotice is null)
                {
                    var first = Notices.FirstOrDefault();
                    if (first is not null)
                    {
                        SelectedNotice = first;
                    }
                    else
                    {
                        Editor = null;
                    }
                }
            }

            UpdateUnreadCount();
        }
        catch (Exception ex)
        {
            SetError($"알림/공지 로딩 실패: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private List<NoticeItem> ApplyFilter()
    {
        var keyword = SearchCriteria.Keyword?.Trim();
        var selectedStatus = SearchCriteria.SelectedStatus?.Status;
        var selectedPriority = SearchCriteria.SelectedPriority?.Priority;

        return _noticeStore
            .Where(x => string.IsNullOrWhiteSpace(keyword) ||
                        x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        x.Body.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Where(x => selectedStatus is null || x.Status == selectedStatus.Value)
            .Where(x => selectedPriority is null || x.Priority == selectedPriority.Value)
            .Where(x => !SearchCriteria.UnreadOnly || !x.ReadByUsers.Contains(_currentUsername))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.Status == NoticeStatus.Published)
            .ThenByDescending(x => x.UpdatedAtLocal)
            .ToList();
    }

    private bool ValidateEditor(NoticeEditor editor)
    {
        if (string.IsNullOrWhiteSpace(editor.Title))
        {
            AddValidationError("제목은 필수입니다.");
        }
        else if (editor.Title.Trim().Length > 200)
        {
            AddValidationError("제목은 200자 이하여야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(editor.Body))
        {
            AddValidationError("본문은 필수입니다.");
        }
        else if (editor.Body.Trim().Length > 4000)
        {
            AddValidationError("본문은 4000자 이하여야 합니다.");
        }

        if (editor.SelectedPriority is null)
        {
            AddValidationError("우선순위를 선택해 주세요.");
        }

        if (editor.PublishFromDate.HasValue &&
            editor.PublishToDate.HasValue &&
            editor.PublishFromDate.Value.Date > editor.PublishToDate.Value.Date)
        {
            AddValidationError("게시 시작일은 종료일보다 늦을 수 없습니다.");
        }

        return !HasValidationErrors;
    }

    private void LoadEditorFromRow(NoticeRow row)
    {
        var priority = PriorityEditOptions.FirstOrDefault(x => x.Priority == row.Priority);
        Editor = new NoticeEditor
        {
            NoticeId = row.Id,
            Title = row.Title,
            Body = row.Body,
            SelectedPriority = priority ?? PriorityEditOptions.FirstOrDefault(),
            IsPinned = row.IsPinned,
            PublishFromDate = row.PublishFromDate,
            PublishToDate = row.PublishToDate
        };
    }

    private void UpdateUnreadCount()
    {
        UnreadCount = _noticeStore.Count(x =>
            x.Status == NoticeStatus.Published &&
            !x.ReadByUsers.Contains(_currentUsername));
    }

    private NoticeRow MapRow(NoticeItem item)
    {
        return new NoticeRow(
            item.Id,
            item.Title,
            item.Body,
            item.Status,
            GetStatusDisplay(item.Status),
            item.Priority,
            GetPriorityDisplay(item.Priority),
            item.IsPinned,
            item.PublishFromDate,
            item.PublishToDate,
            item.CreatedBy,
            item.CreatedAtLocal,
            item.UpdatedAtLocal,
            item.ReadByUsers.Contains(_currentUsername));
    }

    private static string GetStatusDisplay(NoticeStatus status)
    {
        return status switch
        {
            NoticeStatus.Draft => "임시저장",
            NoticeStatus.Published => "게시중",
            NoticeStatus.Ended => "게시종료",
            _ => "알수없음"
        };
    }

    private static string GetPriorityDisplay(NoticePriority priority)
    {
        return priority switch
        {
            NoticePriority.Normal => "일반",
            NoticePriority.Important => "중요",
            NoticePriority.Critical => "긴급",
            _ => "일반"
        };
    }

    private void SeedSampleData()
    {
        var now = DateTime.Now;
        _noticeStore.Clear();

        _noticeStore.Add(new NoticeItem
        {
            Id = Guid.NewGuid(),
            Title = "3월 말 재고실사 일정 안내",
            Body = "3월 29일 18:00부터 재고실사를 진행합니다. 당일 출고 처리 마감은 16:00입니다.",
            Status = NoticeStatus.Published,
            Priority = NoticePriority.Important,
            IsPinned = true,
            PublishFromDate = now.Date.AddDays(-2),
            PublishToDate = now.Date.AddDays(7),
            CreatedBy = "admin",
            CreatedAtLocal = now.AddDays(-3),
            UpdatedAtLocal = now.AddDays(-1)
        });

        _noticeStore.Add(new NoticeItem
        {
            Id = Guid.NewGuid(),
            Title = "납품 라벨 포맷 변경",
            Body = "4월 1일부터 납품 라벨에 주문번호와 로케이션 코드가 추가됩니다.",
            Status = NoticeStatus.Published,
            Priority = NoticePriority.Normal,
            IsPinned = false,
            PublishFromDate = now.Date.AddDays(-1),
            PublishToDate = now.Date.AddDays(10),
            CreatedBy = "admin",
            CreatedAtLocal = now.AddDays(-2),
            UpdatedAtLocal = now.AddHours(-8)
        });

        _noticeStore.Add(new NoticeItem
        {
            Id = Guid.NewGuid(),
            Title = "월말 마감 체크리스트 초안",
            Body = "마감 체크리스트 초안입니다. 부서별 확인 후 게시 예정입니다.",
            Status = NoticeStatus.Draft,
            Priority = NoticePriority.Critical,
            IsPinned = true,
            PublishFromDate = now.Date,
            PublishToDate = now.Date.AddDays(14),
            CreatedBy = _currentUsername,
            CreatedAtLocal = now.AddHours(-6),
            UpdatedAtLocal = now.AddHours(-2)
        });

        _noticeStore.Add(new NoticeItem
        {
            Id = Guid.NewGuid(),
            Title = "시스템 점검 완료",
            Body = "정기 점검이 완료되었습니다. 로그인 세션이 만료된 경우 재로그인해 주세요.",
            Status = NoticeStatus.Ended,
            Priority = NoticePriority.Normal,
            IsPinned = false,
            PublishFromDate = now.Date.AddDays(-10),
            PublishToDate = now.Date.AddDays(-7),
            CreatedBy = "admin",
            CreatedAtLocal = now.AddDays(-10),
            UpdatedAtLocal = now.AddDays(-7)
        });

        _noticeStore.Add(new NoticeItem
        {
            Id = Guid.NewGuid(),
            Title = "신규 거래처 등록 기준 안내",
            Body = "거래처 등록 시 사업자등록증 첨부는 필수입니다. 담당자 연락처도 함께 입력해 주세요.",
            Status = NoticeStatus.Published,
            Priority = NoticePriority.Important,
            IsPinned = false,
            PublishFromDate = now.Date.AddDays(-5),
            PublishToDate = now.Date.AddDays(5),
            CreatedBy = "admin",
            CreatedAtLocal = now.AddDays(-6),
            UpdatedAtLocal = now.AddDays(-5)
        });

        foreach (var notice in _noticeStore.Where(x => x.CreatedBy.Equals(_currentUsername, StringComparison.OrdinalIgnoreCase)))
        {
            notice.ReadByUsers.Add(_currentUsername);
        }

        var firstPublished = _noticeStore.FirstOrDefault(x => x.Status == NoticeStatus.Published);
        firstPublished?.ReadByUsers.Add(_currentUsername);
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SearchCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        AddNoticeCommand.NotifyCanExecuteChanged();
        SaveNoticeCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
        PublishSelectedNoticeCommand.NotifyCanExecuteChanged();
        StopPublishSelectedNoticeCommand.NotifyCanExecuteChanged();
        MarkSelectedAsReadCommand.NotifyCanExecuteChanged();
        MarkAllAsReadCommand.NotifyCanExecuteChanged();
    }

    public sealed partial class NoticeSearchCriteria : ObservableObject
    {
        [ObservableProperty]
        private string? keyword;

        [ObservableProperty]
        private NoticeStatusFilterOption? selectedStatus;

        [ObservableProperty]
        private NoticePriorityFilterOption? selectedPriority;

        [ObservableProperty]
        private bool unreadOnly;
    }

    public sealed partial class NoticeEditor : ObservableObject
    {
        [ObservableProperty]
        private Guid? noticeId;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string body = string.Empty;

        [ObservableProperty]
        private NoticePriorityOption? selectedPriority;

        [ObservableProperty]
        private bool isPinned;

        [ObservableProperty]
        private DateTime? publishFromDate;

        [ObservableProperty]
        private DateTime? publishToDate;
    }

    public sealed record NoticeRow(
        Guid Id,
        string Title,
        string Body,
        NoticeStatus Status,
        string StatusDisplay,
        NoticePriority Priority,
        string PriorityDisplay,
        bool IsPinned,
        DateTime? PublishFromDate,
        DateTime? PublishToDate,
        string CreatedBy,
        DateTime CreatedAtLocal,
        DateTime UpdatedAtLocal,
        bool IsRead)
    {
        public string ReadStateDisplay => IsRead ? "읽음" : "미읽음";
    }

    public sealed record NoticeStatusFilterOption(NoticeStatus? Status, string DisplayName)
    {
        public static NoticeStatusFilterOption All { get; } = new(null, "전체 상태");
    }

    public sealed record NoticePriorityFilterOption(NoticePriority? Priority, string DisplayName)
    {
        public static NoticePriorityFilterOption All { get; } = new(null, "전체 우선순위");
    }

    public sealed record NoticePriorityOption(NoticePriority Priority, string DisplayName);

    public enum NoticeStatus
    {
        Draft,
        Published,
        Ended
    }

    public enum NoticePriority
    {
        Normal,
        Important,
        Critical
    }

    private sealed class NoticeItem
    {
        public Guid Id { get; init; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public NoticeStatus Status { get; set; }
        public NoticePriority Priority { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PublishFromDate { get; set; }
        public DateTime? PublishToDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAtLocal { get; set; }
        public DateTime UpdatedAtLocal { get; set; }
        public HashSet<string> ReadByUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
