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
    private readonly IUserMessageService _userMessageService;
    private string _lastCheckedUsername = string.Empty;

    private const string PrivacyPolicyDetailText = """
개인정보처리방침 

본 서비스는 ERP 시스템 기능을 제공하는 포트폴리오 프로젝트입니다.

1. 목적

본 약관은 서비스 이용과 관련된 기본적인 사항을 규정합니다.

2. 서비스 내용

서비스는 다음 기능을 제공합니다:

- 회원 관리
- 고객 관리
- 데이터 조회 및 관리 기능

※ 본 서비스는 학습 및 포트폴리오 목적으로 제작되었습니다.

3. 회원가입

- 사용자는 이메일과 비밀번호를 통해 회원가입을 할 수 있습니다.
- 허위 정보 입력 시 서비스 이용이 제한될 수 있습니다.

4. 이용자의 책임

사용자는 다음 행위를 해서는 안 됩니다:

- 타인의 계정 사용
- 시스템에 악영향을 주는 행위
- 비정상적인 접근 시도

5. 서비스 제한

다음 경우 서비스 이용이 제한될 수 있습니다:

- 비정상적인 접근
- 보안 위협 발생

6. 면책 조항

본 서비스는 포트폴리오 목적으로 제공되며,

데이터의 정확성이나 지속적인 서비스 제공을 보장하지 않습니다.
""";

    private const string TermsOfServiceDetailText = """
본 서비스의 이용을 위해 아래 내용을 확인하고 동의해 주시기 바랍니다.

본 서비스는 ERP 기능을 제공하는 포트폴리오용 프로젝트로, 회원가입을 통해 기본적인 시스템 이용이 가능합니다.
회원은 정확한 정보를 입력해야 하며, 타인의 정보를 도용하거나 허위 정보를 입력할 경우 서비스 이용이 제한될 수 있습니다.

회원은 본인의 계정 정보를 안전하게 관리할 책임이 있으며, 계정의 부정 사용으로 발생하는 문제에 대해 서비스는 책임을 지지 않습니다.

또한, 서비스의 정상적인 운영을 방해하는 행위(비정상적인 접근, 시스템 공격 등)는 금지되며, 해당 행위가 확인될 경우 사전 통보 없이 이용이 제한될 수 있습니다.

본 서비스는 학습 및 포트폴리오 목적으로 제공되며, 서비스의 안정성, 지속성, 데이터 보존 등에 대해 별도의 보장을 하지 않습니다.

위 내용을 충분히 이해하였으며, 이에 동의합니다.
""";

    public IReadOnlyList<string> PhonePrefixes { get; } = ["010", "011", "016", "017", "018", "019"];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUsernameCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool hasCheckedUsername;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool isUsernameAvailable;

    [ObservableProperty]
    private string usernameCheckMessage = "ID 중복 확인이 필요합니다.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string company = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string selectedPhonePrefix = "010";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string phoneMiddle = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string phoneLast = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool agreePrivacyPolicy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool agreeTermsOfService;

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
        INavigationService navigationService,
        IUserMessageService userMessageService)
    {
        _authApiClient = authApiClient;
        _navigationService = navigationService;
        _userMessageService = userMessageService;
    }

    partial void OnEmailChanged(string? value)
    {
        IsCodeSent = false;
        IsEmailVerified = false;
        VerificationCode = string.Empty;
        CodeExpiresAtUtc = null;
    }

    partial void OnUsernameChanged(string value)
    {
        var normalized = value.Trim();
        if (string.Equals(normalized, _lastCheckedUsername, StringComparison.Ordinal))
        {
            return;
        }

        HasCheckedUsername = false;
        IsUsernameAvailable = false;
        UsernameCheckMessage = string.IsNullOrWhiteSpace(normalized)
            ? "ID를 입력해 주세요."
            : "ID 중복 확인이 필요합니다.";
    }

    partial void OnPhoneMiddleChanged(string value)
    {
        var normalized = NormalizePhonePart(value, maxLength: 4);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PhoneMiddle = normalized;
        }
    }

    partial void OnPhoneLastChanged(string value)
    {
        var normalized = NormalizePhonePart(value, maxLength: 4);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PhoneLast = normalized;
        }
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

    private bool CanCheckUsername()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && Username.Trim().Length >= 3;
    }

    private bool CanRegister()
    {
        return !IsBusy
            && IsEmailVerified
            && !string.IsNullOrWhiteSpace(Username)
            && HasCheckedUsername
            && IsUsernameAvailable
            && string.Equals(_lastCheckedUsername, Username.Trim(), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(Password)
            && !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Company)
            && !string.IsNullOrWhiteSpace(SelectedPhonePrefix)
            && !string.IsNullOrWhiteSpace(PhoneMiddle)
            && !string.IsNullOrWhiteSpace(PhoneLast)
            && AgreePrivacyPolicy
            && AgreeTermsOfService
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
            SetError("이메일을 확인해 주세요.");
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
            AddValidationError("인증번호를 입력해 주세요.");
        }

        if (HasValidationErrors)
        {
            SetError("입력값을 확인해 주세요.");
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
            SetSuccess("이메일 인증이 완료되었습니다. 회원가입을 진행해 주세요.");
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

    [RelayCommand(CanExecute = nameof(CanCheckUsername))]
    private async Task CheckUsernameAsync()
    {
        var normalizedUsername = Username.Trim();

        ClearUserMessage();
        HasCheckedUsername = false;
        IsUsernameAvailable = false;
        _lastCheckedUsername = string.Empty;

        if (normalizedUsername.Length < 3)
        {
            UsernameCheckMessage = "ID는 3자 이상이어야 합니다.";
            SetError(UsernameCheckMessage);
            return;
        }

        try
        {
            SetBusy(true, "ID 중복 확인 중...");

            var result = await _authApiClient.CheckUsernameAvailabilityAsync(normalizedUsername, CancellationToken.None);
            HasCheckedUsername = true;
            IsUsernameAvailable = result.Available;
            _lastCheckedUsername = normalizedUsername;
            UsernameCheckMessage = result.Message ?? (result.Available ? "사용 가능한 ID입니다." : "이미 사용 중인 ID입니다.");

            if (result.Available)
            {
                SetSuccess(UsernameCheckMessage);
            }
            else
            {
                SetError(UsernameCheckMessage);
            }
        }
        catch (Exception ex)
        {
            UsernameCheckMessage = "ID 중복 확인 중 오류가 발생했습니다.";
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
        var normalizedName = Name.Trim();
        var normalizedCompany = Company.Trim();

        ClearValidationErrors();
        ClearUserMessage();
        IsRegistrationSucceeded = false;

        if (normalizedUsername.Length < 3)
        {
            AddValidationError("ID는 3자 이상이어야 합니다.");
        }

        if (!HasCheckedUsername || !IsUsernameAvailable || !string.Equals(_lastCheckedUsername, normalizedUsername, StringComparison.Ordinal))
        {
            AddValidationError("ID 중복 확인을 완료해 주세요.");
        }

        if (Password.Length < 8)
        {
            AddValidationError("PW는 8자 이상이어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            AddValidationError("이름을 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCompany))
        {
            AddValidationError("회사를 입력해 주세요.");
        }

        if (!IsValidEmail(normalizedEmail))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
        }

        if (!IsEmailVerified)
        {
            AddValidationError("이메일 인증을 완료해 주세요.");
        }

        if (!AgreePrivacyPolicy)
        {
            AddValidationError("개인정보처리방침 동의가 필요합니다.");
        }

        if (!AgreeTermsOfService)
        {
            AddValidationError("이용약관 동의가 필요합니다.");
        }

        var normalizedPhone = BuildPhoneNumber();
        if (normalizedPhone is null)
        {
            AddValidationError("전화번호를 확인해 주세요. (예: 010-1234-5678)");
        }

        if (HasValidationErrors)
        {
            SetError("입력값을 확인해 주세요.");
            return;
        }

        try
        {
            SetBusy(true, "회원가입 처리 중...");

            var result = await _authApiClient.SignupAsync(
                normalizedUsername,
                Password,
                normalizedEmail!,
                normalizedName,
                normalizedPhone!,
                normalizedCompany,
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

    [RelayCommand]
    private void ShowPrivacyPolicyDetail()
    {
        _userMessageService.ShowInfo(PrivacyPolicyDetailText, "개인정보처리방침");
    }

    [RelayCommand]
    private void ShowTermsOfServiceDetail()
    {
        _userMessageService.ShowInfo(TermsOfServiceDetailText, "이용약관");
    }

    protected override void OnBusyStateChanged(bool isBusy)
    {
        SendVerificationCodeCommand.NotifyCanExecuteChanged();
        VerifyCodeCommand.NotifyCanExecuteChanged();
        CheckUsernameCommand.NotifyCanExecuteChanged();
        RegisterCommand.NotifyCanExecuteChanged();
        BackToLoginCommand.NotifyCanExecuteChanged();
    }

    private string? BuildPhoneNumber()
    {
        var prefixDigits = NormalizePhonePart(SelectedPhonePrefix, maxLength: 3);
        var middleDigits = NormalizePhonePart(PhoneMiddle, maxLength: 4);
        var lastDigits = NormalizePhonePart(PhoneLast, maxLength: 4);

        if (prefixDigits.Length != 3)
        {
            return null;
        }

        if (middleDigits.Length is < 3 or > 4)
        {
            return null;
        }

        if (lastDigits.Length != 4)
        {
            return null;
        }

        return $"{prefixDigits}-{middleDigits}-{lastDigits}";
    }

    private static string NormalizePhonePart(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= maxLength)
        {
            return digits;
        }

        return digits[..maxLength];
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
