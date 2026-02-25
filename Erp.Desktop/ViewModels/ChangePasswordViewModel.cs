using CommunityToolkit.Mvvm.ComponentModel;
using Erp.Application.Interfaces;

namespace Erp.Desktop.ViewModels;

public sealed partial class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string currentPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string newPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private bool isBusy;

    public ChangePasswordViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    private bool CanChangePassword()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(CurrentPassword)
            && !string.IsNullOrWhiteSpace(NewPassword)
            && !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePasswordAsync()
    {
        StatusMessage = null;

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            StatusMessage = "새 비밀번호 확인이 일치하지 않습니다.";
            return;
        }

        try
        {
            IsBusy = true;
            await _authService.ChangePasswordAsync(CurrentPassword, NewPassword);
            StatusMessage = "비밀번호가 변경되었습니다.";
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
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
}
