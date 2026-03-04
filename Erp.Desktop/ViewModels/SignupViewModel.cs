using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[AllowAnonymousNavigation]
public sealed partial class SignupViewModel : ViewModelBase
{
    private readonly IRegistrationService _registrationService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? email;

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

        ClearValidationErrors();
        ClearUserMessage();
        IsRegistrationSucceeded = false;

        if (normalizedUsername.Length < 3)
        {
            AddValidationError("아이디는 3자 이상이어야 합니다.");
        }

        if (Password.Length < 8)
        {
            AddValidationError("비밀번호는 8자 이상이어야 합니다.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && !IsValidEmail(normalizedEmail))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
        }

        if (HasValidationErrors)
        {
            SetError("입력값을 확인하세요.");
            return;
        }

        try
        {
            SetBusy(true, "회원가입 처리 중...");

            var result = await _registrationService.RegisterAsync(
                new RegisterRequest(normalizedUsername, Password, normalizedEmail));

            if (!result.Success)
            {
                SetError(result.ErrorMessage ?? "회원가입에 실패했습니다.");
                return;
            }

            IsRegistrationSucceeded = true;
            Password = string.Empty;
            SetSuccess("회원가입이 완료되었습니다. 승인 대기 중입니다.");
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

    [RelayCommand(CanExecute = nameof(CanBackToLogin))]
    private void BackToLogin()
    {
        _navigationService.NavigateTo<LoginViewModel>();
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        RegisterCommand.NotifyCanExecuteChanged();
        BackToLoginCommand.NotifyCanExecuteChanged();
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
