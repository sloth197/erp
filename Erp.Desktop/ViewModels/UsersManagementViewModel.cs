using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.MasterUsersRead)]
public sealed partial class UsersManagementViewModel : ObservableObject
{
    private readonly IUserService _userService;

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
    private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableSelectedUserCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignRoleCommand))]
    private bool isBusy;

    public UsersManagementViewModel(IUserService userService)
    {
        _userService = userService;
        _ = RefreshAsync();
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

            await _userService.DisableUserAsync(SelectedUser.Id);
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

    public sealed record UserRow(
        Guid Id,
        string Username,
        bool IsActive,
        int FailedLoginCount,
        DateTime? LockoutEndUtc,
        string RolesDisplay);
}
