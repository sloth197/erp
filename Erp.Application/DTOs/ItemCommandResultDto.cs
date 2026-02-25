namespace Erp.Application.DTOs;

public sealed record ItemCommandResultDto(
    Guid ItemId,
    byte[] RowVersion,
    bool IsActive,
    DateTime UpdatedAtUtc);
