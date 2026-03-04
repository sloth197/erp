namespace Erp.Application.DTOs;

public sealed record AuditLogListDto(
    DateTime CreatedAtUtc,
    string Action,
    Guid? ActorUserId,
    string? ActorUsername,
    string? Target,
    string? DetailJson,
    string? Ip);
