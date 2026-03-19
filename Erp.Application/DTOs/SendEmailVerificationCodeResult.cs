namespace Erp.Application.DTOs;

public sealed record SendEmailVerificationCodeResult(bool Success, string? ErrorMessage, DateTime? ExpiresAtUtc)
{
    public static SendEmailVerificationCodeResult Succeeded(DateTime expiresAtUtc)
        => new(true, null, expiresAtUtc);

    public static SendEmailVerificationCodeResult Failed(string errorMessage)
        => new(false, errorMessage, null);
}
