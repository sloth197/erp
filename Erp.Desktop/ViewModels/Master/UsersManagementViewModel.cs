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
    private ObservableCollection<string> roles = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisableSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    private UserRow? selectedUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateUserCommand))]
    private string newUsername = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateUserCommand))]
    private string newPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateUserCommand))]
    private string selectedRoleForCreate = "Staff";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    private string selectedRoleForAssign = "Staff";

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
    [NotifyCanExecuteChangedFor(nameof(CreateUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
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

        _ = RefreshAsync();
    }

    public bool CanManageApprovals => _currentUserContext.HasPermission(PermissionCodes.MasterUsersWrite);

    partial void OnSelectedPendingStatusChanged(PendingStatusFilterOption? value)
    {
        if (!IsBusy && CanManageApprovals)
        {
            _ = RefreshPendingUsersAsync();
        }
    }

    private bool CanCreateUser()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(NewUsername)
            && !string.IsNullOrWhiteSpace(NewPassword)
            && !string.IsNullOrWhiteSpace(SelectedRoleForCreate);
    }

    private bool CanDisableSelectedUser()
    {
        return !IsBusy && SelectedUser is not null;
    }

    private bool CanAssignRole()
    {
        return !IsBusy
            && SelectedUser is not null
            && !string.IsNullOrWhiteSpace(SelectedRoleForAssign);
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
            Roles = new ObservableCollection<string>(roleDtos.Select(x => x.Name));

            if (Roles.Count > 0)
            {
                if (!Roles.Contains(SelectedRoleForCreate))
                {
                    SelectedRoleForCreate = Roles[0];
                }

                if (!Roles.Contains(SelectedRoleForAssign))
                {
                    SelectedRoleForAssign = Roles[0];
                }
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

    [RelayCommand(CanExecute = nameof(CanCreateUser))]
    private async Task CreateUserAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = null;

            await _userService.CreateUserAsync(NewUsername, NewPassword, SelectedRoleForCreate);

            NewUsername = string.Empty;
            NewPassword = string.Empty;

            await RefreshAsync();
            StatusMessage = "사용자를 생성했습니다.";
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

    private static UserRow MapRow(UserSummaryDto dto)
    {
        return new UserRow(
            dto.Id,
            dto.Username,
            dto.IsActive,
            dto.FailedLoginCount,
            dto.LockoutEndUtc,
            string.Join(", ", dto.Roles));
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
        bool IsActive,
        int FailedLoginCount,
        DateTime? LockoutEndUtc,
        string RolesDisplay);

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
}
