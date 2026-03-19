namespace Erp.Application.DTOs;

public sealed record VerifyEmailVerificationCodeRequest(
    string Email,
    string Code,
    string? Purpose = null);
