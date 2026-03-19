using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IEmailVerificationService
{
    Task<SendEmailVerificationCodeResult> SendCodeAsync(
        SendEmailVerificationCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<VerifyEmailVerificationCodeResult> VerifyCodeAsync(
        VerifyEmailVerificationCodeRequest request,
        CancellationToken cancellationToken = default);
}
