namespace Erp.Application.DTOs;

public sealed record VerifyEmailVerificationCodeResult(
    bool Success,
    string? ErrorMessage,
    int? RemainingAttempts)
{
    public static VerifyEmailVerificationCodeResult Succeeded()
        => new(true, null, null);

    public static VerifyEmailVerificationCodeResult Failed(string errorMessage, int? remainingAttempts = null)
        => new(false, errorMessage, remainingAttempts);
}
