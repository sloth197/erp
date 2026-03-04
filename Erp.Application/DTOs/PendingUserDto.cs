namespace Erp.Application.DTOs;

public sealed record PendingUserDto(
    Guid UserId,
    string Username,
    string? Email,
    DateTime CreatedAtUtc,
    Erp.Domain.Entities.UserStatus Status);
