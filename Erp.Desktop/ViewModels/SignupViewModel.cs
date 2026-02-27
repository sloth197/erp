using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[AllowAnonymousNavigation]
public sealed partial class SignupViewModel : ObservableObject
{
    private readonly IRegistrationService _registrationService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string? email;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackToLoginCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool isRegistrationSucceeded;

    public SignupViewModel(
        IRegistrationService registrationService,
        INavigationService navigationService)
    {
        _registrationService = registrationService;
        _navigationService = navigationService;
    }

    private bool CanRegister()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
    }

    private bool CanBackToLogin() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        var normalizedUsername = Username.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

        if (normalizedUsername.Length < 3)
        {
            ErrorMessage = "아이디는 3자 이상이어야 합니다.";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "비밀번호는 8자 이상이어야 합니다.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && !IsValidEmail(normalizedEmail))
        {
            ErrorMessage = "이메일 형식이 올바르지 않습니다.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;

            var result = await _registrationService.RegisterAsync(
                new RegisterRequest(normalizedUsername, Password, normalizedEmail));

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "회원가입에 실패했습니다.";
                return;
            }

            IsRegistrationSucceeded = true;
            Password = string.Empty;
            StatusMessage = "회원가입이 완료되었습니다. 승인 대기 중입니다.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBackToLogin))]
    private void BackToLogin()
    {
        _navigationService.NavigateTo<LoginViewModel>();
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
