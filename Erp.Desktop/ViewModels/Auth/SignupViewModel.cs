using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Desktop.Navigation;
using Erp.Desktop.Services;

namespace Erp.Desktop.ViewModels;

[AllowAnonymousNavigation]
public sealed partial class SignupViewModel : ViewModelBase
{
    private readonly IAuthApiClient _authApiClient;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendVerificationCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string? email;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCodeCommand))]
    private string verificationCode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendVerificationCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool isEmailVerified;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCodeCommand))]
    private bool isCodeSent;

    [ObservableProperty]
    private DateTime? codeExpiresAtUtc;

    [ObservableProperty]
    private bool isRegistrationSucceeded;

    public SignupViewModel(
        IAuthApiClient authApiClient,
        INavigationService navigationService)
    {
        _authApiClient = authApiClient;
        _navigationService = navigationService;
    }

    partial void OnEmailChanged(string? value)
    {
        IsCodeSent = false;
        IsEmailVerified = false;
        VerificationCode = string.Empty;
        CodeExpiresAtUtc = null;
    }

    private bool CanSendVerificationCode()
    {
        return !IsBusy
            && !IsEmailVerified
            && IsValidEmail(Email?.Trim());
    }

    private bool CanVerifyCode()
    {
        return !IsBusy
            && IsCodeSent
            && !IsEmailVerified
            && !string.IsNullOrWhiteSpace(VerificationCode)
            && IsValidEmail(Email?.Trim());
    }

    private bool CanRegister()
    {
        return !IsBusy
            && IsEmailVerified
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password)
            && IsValidEmail(Email?.Trim());
    }

    private bool CanBackToLogin() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSendVerificationCode))]
    private async Task SendVerificationCodeAsync()
    {
        var normalizedEmail = Email?.Trim();

        ClearValidationErrors();
        ClearUserMessage();

        if (!IsValidEmail(normalizedEmail))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
            SetError("이메일을 확인하세요.");
            return;
        }

        try
        {
            SetBusy(true, "인증번호 전송 중...");

            var result = await _authApiClient.SendVerificationCodeAsync(normalizedEmail!, CancellationToken.None);
            if (!result.Success)
            {
                SetError(result.ErrorMessage ?? "인증번호 전송에 실패했습니다.");
                return;
            }

            IsCodeSent = true;
            IsEmailVerified = false;
            CodeExpiresAtUtc = result.ExpiresAtUtc;
            SetSuccess("인증번호를 이메일로 보냈습니다.");
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

    [RelayCommand(CanExecute = nameof(CanVerifyCode))]
    private async Task VerifyCodeAsync()
    {
        var normalizedEmail = Email?.Trim();
        var normalizedCode = VerificationCode.Trim();

        ClearValidationErrors();
        ClearUserMessage();

        if (!IsValidEmail(normalizedEmail))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            AddValidationError("인증번호를 입력하세요.");
        }

        if (HasValidationErrors)
        {
            SetError("입력값을 확인하세요.");
            return;
        }

        try
        {
            SetBusy(true, "인증번호 확인 중...");

            var result = await _authApiClient.VerifyCodeAsync(normalizedEmail!, normalizedCode, CancellationToken.None);
            if (!result.Success)
            {
                if (result.RemainingAttempts.HasValue)
                {
                    SetError($"{result.ErrorMessage} (남은 시도: {result.RemainingAttempts.Value})");
                }
                else
                {
                    SetError(result.ErrorMessage ?? "인증번호 확인에 실패했습니다.");
                }

                return;
            }

            IsEmailVerified = true;
            SetSuccess("이메일 인증이 완료되었습니다. 회원가입을 진행하세요.");
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

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        var normalizedUsername = Username.Trim();
        var normalizedEmail = Email?.Trim();

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

        if (!IsValidEmail(normalizedEmail))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
        }

        if (!IsEmailVerified)
        {
            AddValidationError("이메일 인증을 완료하세요.");
        }

        if (HasValidationErrors)
        {
            SetError("입력값을 확인하세요.");
            return;
        }

        try
        {
            SetBusy(true, "회원가입 처리 중...");

            var result = await _authApiClient.SignupAsync(
                normalizedUsername,
                Password,
                normalizedEmail!,
                CancellationToken.None);

            if (!result.Success)
            {
                SetError(result.ErrorMessage ?? "회원가입에 실패했습니다.");
                return;
            }

            IsRegistrationSucceeded = true;
            Password = string.Empty;
            VerificationCode = string.Empty;
            SetSuccess("회원가입이 완료되었습니다. 바로 로그인할 수 있습니다.");
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
        SendVerificationCodeCommand.NotifyCanExecuteChanged();
        VerifyCodeCommand.NotifyCanExecuteChanged();
        RegisterCommand.NotifyCanExecuteChanged();
        BackToLoginCommand.NotifyCanExecuteChanged();
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

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
