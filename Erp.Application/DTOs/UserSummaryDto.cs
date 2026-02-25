namespace Erp.Application.DTOs;

public sealed record UserSummaryDto(
    Guid Id,
    string Username,
    bool IsActive,
    int FailedLoginCount,
    DateTime? LockoutEndUtc,
    IReadOnlyList<string> Roles);
