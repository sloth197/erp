namespace Erp.Application.DTOs;

public sealed record StockBalanceLookupDto(
    Guid ItemId,
    string ItemCode,
    decimal QtyOnHand);
