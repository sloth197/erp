namespace Erp.Application.DTOs;

public sealed record LocationOptionDto(
    Guid Id,
    Guid WarehouseId,
    string Code,
    string Name,
    bool IsActive);
