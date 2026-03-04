namespace Erp.Application.DTOs;

public sealed record WarehouseOptionDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);
