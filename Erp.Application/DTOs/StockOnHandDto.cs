namespace Erp.Application.DTOs;

public sealed record StockOnHandDto(
    string ItemCode,
    string ItemName,
    string WarehouseCode,
    string? LocationCode,
    decimal QtyOnHand,
    DateTime UpdatedAtUtc);
