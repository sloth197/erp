using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[AllowAnonymousNavigation]
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly Erp.Application.Interfaces.IAuthService _authService;
    private readonly Navigation.INavigationService _navigationService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string username = "admin";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignUpCommand))]
    private bool isBusy;

    public LoginViewModel(
        Erp.Application.Interfaces.IAuthService authService,
        Navigation.INavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
    }

    private bool CanLogin()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
    }

    private bool CanSignUp() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var result = await _authService.LoginAsync(Username, Password);
            if (!result.Success)
            {
                if (string.Equals(result.ErrorMessage, "이메일 인증이 필요합니다.", StringComparison.Ordinal))
                {
                    ErrorMessage = "이메일 인증이 필요합니다. 회원가입 화면에서 인증을 먼저 완료하세요.";
                    return;
                }

                if (result.LockoutRemainingSeconds.HasValue)
                {
                    ErrorMessage = $"{result.ErrorMessage} 남은 시간: {result.LockoutRemainingSeconds.Value}초";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }

                return;
            }

            Password = string.Empty;
            _navigationService.NavigateTo<HomeViewModel>();
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

    [RelayCommand(CanExecute = nameof(CanSignUp))]
    private void SignUp()
    {
        ErrorMessage = null;
        Password = string.Empty;
        _navigationService.NavigateTo<SignupViewModel>();
    }
}
