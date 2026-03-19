using System.Net.Http;
using System.Net.Http.Json;
using Erp.Application.DTOs;

namespace Erp.Desktop.Services;

public sealed class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("AuthApi:BaseUrl 설정이 올바르지 않습니다.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<SendEmailVerificationCodeResult> SendVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/auth/email/send-code",
                new SendEmailVerificationCodeRequest(email, "signup"),
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<SendEmailVerificationCodeResult>(
                cancellationToken: cancellationToken);

            if (payload is not null)
            {
                return payload;
            }

            return SendEmailVerificationCodeResult.Failed("인증번호 요청에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return SendEmailVerificationCodeResult.Failed($"인증번호 요청 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    public async Task<VerifyEmailVerificationCodeResult> VerifyCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/auth/email/verify-code",
                new VerifyEmailVerificationCodeRequest(email, code, "signup"),
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<VerifyEmailVerificationCodeResult>(
                cancellationToken: cancellationToken);

            if (payload is not null)
            {
                return payload;
            }

            return VerifyEmailVerificationCodeResult.Failed("인증 확인에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return VerifyEmailVerificationCodeResult.Failed($"인증 확인 중 오류가 발생했습니다: {ex.Message}");
        }
    }

    public async Task<RegisterResult> SignupAsync(
        string username,
        string password,
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/auth/signup",
                new RegisterRequest(username, password, email),
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<RegisterResult>(
                cancellationToken: cancellationToken);

            if (payload is not null)
            {
                return payload;
            }

            return RegisterResult.Failed("회원가입 요청에 실패했습니다.");
        }
        catch (Exception ex)
        {
            return RegisterResult.Failed($"회원가입 중 오류가 발생했습니다: {ex.Message}");
        }
    }
}
