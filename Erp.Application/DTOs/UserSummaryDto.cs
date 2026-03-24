namespace Erp.Application.DTOs;

public sealed record UserSummaryDto(
    Guid Id,
    string Username,
    Erp.Domain.Entities.UserStatus Status,
    bool IsActive,
    int FailedLoginCount,
    DateTime? LockoutEndUtc,
    IReadOnlyList<string> Roles);
