using Erp.Application.DTOs;

namespace Erp.Desktop.Services;

public interface IAuthApiClient
{
    Task<SendEmailVerificationCodeResult> SendVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<VerifyEmailVerificationCodeResult> VerifyCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task<CheckUsernameAvailabilityResult> CheckUsernameAvailabilityAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task<RegisterResult> SignupAsync(
        string username,
        string password,
        string email,
        string name,
        string phoneNumber,
        string company,
        CancellationToken cancellationToken = default);
}
