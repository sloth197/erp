using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Desktop.Navigation;
using Erp.Domain.Entities;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.MasterUsersWrite)]
public sealed partial class UsersManagementViewModel : ObservableObject
{
    private const int PendingPageSize = 200;

    private readonly IUserService _userService;
    private readonly IUserApprovalService _userApprovalService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty]
    private ObservableCollection<UserRow> users = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisableSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignJobGradeCommand))]
    private UserRow? selectedUser;

    [ObservableProperty]
    private ObservableCollection<string> assignableRoles = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    private string selectedRoleForAssign = "Staff";

    [ObservableProperty]
    private ObservableCollection<JobGradeOption> assignableJobGrades = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignJobGradeCommand))]
    private JobGradeOption? selectedJobGradeForAssign;

    [ObservableProperty]
    private ObservableCollection<PendingUserRow> pendingUsers = new();

    [ObservableProperty]
    private ObservableCollection<PendingStatusFilterOption> pendingStatusFilters = new();

    [ObservableProperty]
    private PendingStatusFilterOption? selectedPendingStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApprovePendingUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRejectDialogCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisablePendingUserCommand))]
    private PendingUserRow? selectedPendingUser;

    [ObservableProperty]
    private string pendingKeyword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRejectCommand))]
    private bool isRejectDialogOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRejectCommand))]
    private PendingUserRow? rejectTargetUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRejectCommand))]
    private string rejectReasonInput = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisableSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignJobGradeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApprovePendingUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRejectDialogCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRejectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisablePendingUserCommand))]
    private bool isBusy;

    public UsersManagementViewModel(
        IUserService userService,
        IUserApprovalService userApprovalService,
        ICurrentUserContext currentUserContext)
    {
        _userService = userService;
        _userApprovalService = userApprovalService;
        _currentUserContext = currentUserContext;

        PendingStatusFilters = new ObservableCollection<PendingStatusFilterOption>
        {
            new(UserStatus.Pending, "Pending"),
            new(UserStatus.Active, "Active"),
            new(UserStatus.Disabled, "Disabled"),
            new(UserStatus.Rejected, "Rejected")
        };
        SelectedPendingStatus = PendingStatusFilters.FirstOrDefault();

        AssignableJobGrades = new ObservableCollection<JobGradeOption>(BuildJobGradeOptions());
        SelectedJobGradeForAssign = AssignableJobGrades.FirstOrDefault();

        _ = RefreshAsync();
    }

    public bool CanManageApprovals => _currentUserContext.HasPermission(PermissionCodes.MasterUsersWrite);

    partial void OnSelectedUserChanged(UserRow? value)
    {
        if (value is null)
        {
            return;
        }

        if (AssignableRoles.Count > 0)
        {
            var preferredRole = AssignableRoles.FirstOrDefault(role =>
                value.Roles.Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrWhiteSpace(preferredRole))
            {
                SelectedRoleForAssign = preferredRole;
            }
        }

        if (AssignableJobGrades.Count > 0)
        {
            var preferredGrade = AssignableJobGrades.FirstOrDefault(x => x.Grade == value.JobGrade);
            if (preferredGrade is not null)
            {
                SelectedJobGradeForAssign = preferredGrade;
            }
        }
    }

    partial void OnSelectedPendingStatusChanged(PendingStatusFilterOption? value)
    {
        if (!IsBusy && CanManageApprovals)
        {
            _ = RefreshPendingUsersAsync();
        }
    }

    private bool CanDisableSelectedUser()
    {
        return !IsBusy && SelectedUser is { IsActive: true };
    }

    private bool CanActivateSelectedUser()
    {
        return !IsBusy && SelectedUser is { Status: UserStatus.Disabled, IsActive: false };
    }

    private bool CanAssignRole()
    {
        if (IsBusy || SelectedUser is null || string.IsNullOrWhiteSpace(SelectedRoleForAssign))
        {
            return false;
        }

        if (!IsAssignableRole(SelectedRoleForAssign))
        {
            return false;
        }

        return SelectedUser.Roles.Count != 1 ||
               !string.Equals(SelectedUser.Roles[0], SelectedRoleForAssign, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAssignJobGrade()
    {
        if (IsBusy || SelectedUser is null || SelectedJobGradeForAssign is null)
        {
            return false;
        }

        return SelectedUser.JobGrade != SelectedJobGradeForAssign.Grade;
    }

    private bool CanApprovePendingUser(PendingUserRow? row)
    {
        return !IsBusy && CanManageApprovals && row is not null;
    }

    private bool CanOpenRejectDialog(PendingUserRow? row)
    {
        return !IsBusy && CanManageApprovals && row is not null;
    }

    private bool CanDisablePendingUser(PendingUserRow? row)
    {
        return !IsBusy && CanManageApprovals && row is not null;
    }

    private bool CanConfirmReject()
    {
        return !IsBusy && IsRejectDialogOpen && RejectTargetUser is not null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = null;

            var userDtos = await _userService.GetUsersAsync();
            var roleDtos = await _userService.GetRolesAsync();

            Users = new ObservableCollection<UserRow>(userDtos.Select(MapRow));
            var assignable = ResolveAssignableRoles(roleDtos.Select(x => x.Name));
            AssignableRoles = new ObservableCollection<string>(assignable);

            if (AssignableRoles.Count == 0)
            {
                StatusMessage = "역할 부여 대상이 없습니다. Admin/Staff 역할을 먼저 확인해 주세요.";
            }
            else if (!AssignableRoles.Any(x => string.Equals(x, SelectedRoleForAssign, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedRoleForAssign = AssignableRoles[0];
            }

            if (SelectedUser is not null)
            {
                var preferredGrade = AssignableJobGrades.FirstOrDefault(x => x.Grade == SelectedUser.JobGrade);
                SelectedJobGradeForAssign = preferredGrade ?? AssignableJobGrades.FirstOrDefault();
            }
            else if (SelectedJobGradeForAssign is null)
            {
                SelectedJobGradeForAssign = AssignableJobGrades.FirstOrDefault();
            }

            await LoadPendingUsersAsync();
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
    private async Task RefreshPendingUsersAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = null;

            await LoadPendingUsersAsync();
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

    [RelayCommand(CanExecute = nameof(CanDisableSelectedUser))]
    private async Task DisableSelectedUserAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userApprovalService.DisableAsync(SelectedUser.Id);
            await RefreshAsync();
            StatusMessage = "사용자를 비활성화했습니다.";
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

    [RelayCommand(CanExecute = nameof(CanActivateSelectedUser))]
    private async Task ActivateSelectedUserAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userApprovalService.EnableAsync(SelectedUser.Id);
            await RefreshAsync();
            StatusMessage = "사용자를 활성화했습니다.";
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

    [RelayCommand(CanExecute = nameof(CanAssignRole))]
    private async Task AssignRoleAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            if (!IsAssignableRole(SelectedRoleForAssign))
            {
                throw new InvalidOperationException("역할은 Staff 또는 Admin만 부여할 수 있습니다.");
            }

            await _userService.AssignRoleAsync(SelectedUser.Id, SelectedRoleForAssign);
            await RefreshAsync();
            StatusMessage = "역할을 부여했습니다.";
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

    [RelayCommand(CanExecute = nameof(CanAssignJobGrade))]
    private async Task AssignJobGradeAsync()
    {
        if (SelectedUser is null || SelectedJobGradeForAssign is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userService.AssignJobGradeAsync(SelectedUser.Id, SelectedJobGradeForAssign.Grade);
            await RefreshAsync();
            StatusMessage = $"직급을 '{SelectedJobGradeForAssign.DisplayName}'(으)로 변경했습니다.";
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

    [RelayCommand(CanExecute = nameof(CanApprovePendingUser))]
    private async Task ApprovePendingUserAsync(PendingUserRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userApprovalService.ApproveAsync(new ApproveUserRequest(row.Id));

            await LoadPendingUsersAsync();
            StatusMessage = $"승인 완료: {row.Username}";
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

    [RelayCommand(CanExecute = nameof(CanOpenRejectDialog))]
    private void OpenRejectDialog(PendingUserRow? row)
    {
        if (row is null)
        {
            return;
        }

        RejectTargetUser = row;
        RejectReasonInput = string.Empty;
        IsRejectDialogOpen = true;
        StatusMessage = null;
    }

    [RelayCommand]
    private void CloseRejectDialog()
    {
        IsRejectDialogOpen = false;
        RejectTargetUser = null;
        RejectReasonInput = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmReject))]
    private async Task ConfirmRejectAsync()
    {
        if (RejectTargetUser is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            var reason = string.IsNullOrWhiteSpace(RejectReasonInput)
                ? null
                : RejectReasonInput.Trim();

            await _userApprovalService.RejectAsync(new RejectUserRequest(RejectTargetUser.Id, reason));

            var username = RejectTargetUser.Username;
            CloseRejectDialog();
            await LoadPendingUsersAsync();
            StatusMessage = $"거절 완료: {username}";
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

    [RelayCommand(CanExecute = nameof(CanDisablePendingUser))]
    private async Task DisablePendingUserAsync(PendingUserRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userApprovalService.DisableAsync(row.Id, "Disabled by admin from approval grid.");

            await LoadPendingUsersAsync();
            StatusMessage = $"비활성 완료: {row.Username}";
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

    private async Task LoadPendingUsersAsync()
    {
        if (!CanManageApprovals)
        {
            PendingUsers = new ObservableCollection<PendingUserRow>();
            return;
        }

        var status = SelectedPendingStatus?.Status ?? UserStatus.Pending;
        var result = await _userApprovalService.ListPendingUsersAsync(new ListPendingUsersQuery
        {
            Keyword = string.IsNullOrWhiteSpace(PendingKeyword) ? null : PendingKeyword.Trim(),
            Status = status,
            Page = 1,
            PageSize = PendingPageSize
        });

        PendingUsers = new ObservableCollection<PendingUserRow>(result.Items.Select(MapPendingRow));
        SelectedPendingUser = PendingUsers.FirstOrDefault();
    }

    private static IReadOnlyList<string> ResolveAssignableRoles(IEnumerable<string> roleNames)
    {
        var names = roleNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolved = new List<string>();
        var staff = names.FirstOrDefault(x => string.Equals(x, "Staff", StringComparison.OrdinalIgnoreCase));
        var admin = names.FirstOrDefault(x => string.Equals(x, "Admin", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(staff))
        {
            resolved.Add(staff);
        }

        if (!string.IsNullOrWhiteSpace(admin))
        {
            resolved.Add(admin);
        }

        return resolved;
    }

    private bool IsAssignableRole(string roleName)
    {
        return AssignableRoles.Any(x => string.Equals(x, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private static UserRow MapRow(UserSummaryDto dto)
    {
        return new UserRow(
            dto.Id,
            dto.Username,
            dto.Status,
            dto.IsActive,
            dto.FailedLoginCount,
            dto.LockoutEndUtc,
            dto.JobGrade,
            dto.Roles.ToList());
    }

    private static PendingUserRow MapPendingRow(PendingUserDto dto)
    {
        return new PendingUserRow(
            dto.UserId,
            dto.Username,
            dto.Email,
            dto.CreatedAtUtc,
            dto.Status);
    }

    public sealed record UserRow(
        Guid Id,
        string Username,
        UserStatus Status,
        bool IsActive,
        int FailedLoginCount,
        DateTime? LockoutEndUtc,
        UserJobGrade JobGrade,
        IReadOnlyList<string> Roles)
    {
        public string StatusDisplay => Status.ToString();
        public string JobGradeDisplay => UsersManagementViewModel.ToJobGradeDisplayName(JobGrade);
        public string RolesDisplay => string.Join(", ", Roles);
    }

    public sealed record PendingUserRow(
        Guid Id,
        string Username,
        string? Email,
        DateTime CreatedAtUtc,
        UserStatus Status)
    {
        public string StatusText => Status.ToString();
    }

    public sealed record PendingStatusFilterOption(UserStatus Status, string DisplayName);
    public sealed record JobGradeOption(UserJobGrade Grade, string DisplayName);

    private static IReadOnlyList<JobGradeOption> BuildJobGradeOptions()
    {
        return
        [
            new JobGradeOption(UserJobGrade.Staff, "사원"),
            new JobGradeOption(UserJobGrade.AssistantManager, "대리"),
            new JobGradeOption(UserJobGrade.Manager, "과장"),
            new JobGradeOption(UserJobGrade.DeputyGeneralManager, "차장"),
            new JobGradeOption(UserJobGrade.GeneralManager, "부장"),
            new JobGradeOption(UserJobGrade.President, "사장")
        ];
    }

    private static string ToJobGradeDisplayName(UserJobGrade jobGrade)
    {
        return jobGrade switch
        {
            UserJobGrade.Staff => "사원",
            UserJobGrade.AssistantManager => "대리",
            UserJobGrade.Manager => "과장",
            UserJobGrade.DeputyGeneralManager => "차장",
            UserJobGrade.GeneralManager => "부장",
            UserJobGrade.President => "사장",
            _ => "사원"
        };
    }
}
