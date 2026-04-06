using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.SystemSettingsRead)]
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAccessControl _accessControl;
    private SettingsSnapshot _savedSnapshot = SettingsSnapshot.Default;
    private bool _isApplyingSnapshot;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    private bool enableDetailedErrors;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    private bool enableAuditTrail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    private string sessionTimeoutMinutesInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultsCommand))]
    private string dateDisplayFormat = string.Empty;

    [ObservableProperty]
    private string lastSavedText = "저장 이력 없음";

    public bool CanRead { get; }
    public bool CanWrite { get; }
    public string PermissionSummary => CanWrite ? "권한: 조회/수정 가능" : "권한: 조회 전용";
    public string ApplyModeGuide => "적용 방식: 일반 옵션은 즉시 반영되며, 보안성 옵션은 재로그인 후 반영됩니다.";
    public string UnsavedChangesSummary => HasUnsavedChanges
        ? "저장되지 않은 변경 사항이 있습니다."
        : "모든 변경 사항이 저장되었습니다.";

    public SettingsViewModel(ICurrentUserContext currentUserContext, IAccessControl accessControl)
    {
        _accessControl = accessControl;
        CanRead = currentUserContext.HasPermission(PermissionCodes.SystemSettingsRead);
        CanWrite = currentUserContext.HasPermission(PermissionCodes.SystemSettingsWrite);

        ApplySnapshot(SettingsSnapshot.Default, isSavedSnapshot: true);
    }

    partial void OnHasUnsavedChangesChanged(bool value)
    {
        OnPropertyChanged(nameof(UnsavedChangesSummary));
    }

    partial void OnEnableDetailedErrorsChanged(bool value) => OnEditorChanged();
    partial void OnEnableAuditTrailChanged(bool value) => OnEditorChanged();
    partial void OnSessionTimeoutMinutesInputChanged(string value) => OnEditorChanged();
    partial void OnDateDisplayFormatChanged(string value) => OnEditorChanged();

    private void OnEditorChanged()
    {
        if (_isApplyingSnapshot)
        {
            return;
        }

        RecalculateState(validate: true);
    }

    private bool CanSave()
    {
        return !IsBusy && CanWrite && HasUnsavedChanges;
    }

    private bool CanCancel()
    {
        return !IsBusy && HasUnsavedChanges;
    }

    private bool CanRestoreDefaults()
    {
        return !IsBusy && CanWrite;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            ClearUserMessage();
            SetBusy(true, "환경설정을 저장하는 중...");
            await Task.Delay(80);

            _accessControl.DemandPermission(PermissionCodes.SystemSettingsWrite);
            if (!Validate())
            {
                SetError("입력값을 확인해 주세요.");
                return;
            }

            _savedSnapshot = CaptureSnapshot();
            HasUnsavedChanges = false;
            LastSavedText = $"마지막 저장: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            SetSuccess("환경설정을 저장했습니다.");
        }
        catch (ForbiddenException)
        {
            SetError("설정을 저장할 권한이 없습니다.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        ClearUserMessage();
        ClearValidationErrors();
        ApplySnapshot(_savedSnapshot, isSavedSnapshot: false);
        SetSuccess("변경 사항을 취소했습니다.");
    }

    [RelayCommand(CanExecute = nameof(CanRestoreDefaults))]
    private void RestoreDefaults()
    {
        try
        {
            ClearUserMessage();
            _accessControl.DemandPermission(PermissionCodes.SystemSettingsWrite);
            ApplySnapshot(SettingsSnapshot.Default, isSavedSnapshot: false);
            SetSuccess("기본값을 불러왔습니다. 저장 버튼으로 반영해 주세요.");
        }
        catch (ForbiddenException)
        {
            SetError("기본값 복원을 수행할 권한이 없습니다.");
        }
    }

    private void ApplySnapshot(SettingsSnapshot snapshot, bool isSavedSnapshot)
    {
        _isApplyingSnapshot = true;
        EnableDetailedErrors = snapshot.EnableDetailedErrors;
        EnableAuditTrail = snapshot.EnableAuditTrail;
        SessionTimeoutMinutesInput = snapshot.SessionTimeoutMinutesInput;
        DateDisplayFormat = snapshot.DateDisplayFormat;
        _isApplyingSnapshot = false;

        if (isSavedSnapshot)
        {
            _savedSnapshot = snapshot;
            LastSavedText = "기본 설정 로드됨";
        }

        RecalculateState(validate: true);
    }

    private void RecalculateState(bool validate)
    {
        if (validate)
        {
            Validate();
        }

        var current = CaptureSnapshot();
        HasUnsavedChanges = current != _savedSnapshot;
    }

    private bool Validate()
    {
        ClearValidationErrors();

        if (!int.TryParse(SessionTimeoutMinutesInput, out var sessionTimeoutMinutes))
        {
            AddValidationError("세션 만료 시간은 숫자로 입력해 주세요.");
        }
        else if (sessionTimeoutMinutes < 5 || sessionTimeoutMinutes > 240)
        {
            AddValidationError("세션 만료 시간은 5~240분 범위여야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(DateDisplayFormat))
        {
            AddValidationError("날짜 표시 형식은 필수입니다.");
        }
        else if (DateDisplayFormat.Trim().Length > 20)
        {
            AddValidationError("날짜 표시 형식은 20자 이하여야 합니다.");
        }

        return !HasValidationErrors;
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RestoreDefaultsCommand.NotifyCanExecuteChanged();
    }

    private SettingsSnapshot CaptureSnapshot()
    {
        return new SettingsSnapshot(
            EnableDetailedErrors,
            EnableAuditTrail,
            (SessionTimeoutMinutesInput ?? string.Empty).Trim(),
            (DateDisplayFormat ?? string.Empty).Trim());
    }

    private readonly record struct SettingsSnapshot(
        bool EnableDetailedErrors,
        bool EnableAuditTrail,
        string SessionTimeoutMinutesInput,
        string DateDisplayFormat)
    {
        public static SettingsSnapshot Default { get; } =
            new(
                EnableDetailedErrors: true,
                EnableAuditTrail: true,
                SessionTimeoutMinutesInput: "60",
                DateDisplayFormat: "yyyy-MM-dd");
    }
}
