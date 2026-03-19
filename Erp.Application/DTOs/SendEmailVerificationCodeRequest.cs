namespace Erp.Application.DTOs;

public sealed record SendEmailVerificationCodeRequest(
    string Email,
    string? Purpose = null);
