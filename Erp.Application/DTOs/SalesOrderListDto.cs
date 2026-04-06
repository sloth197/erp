namespace Erp.Application.DTOs;

public sealed record SalesOrderListDto(
    Guid Id,
    string SoNumber,
    string CustomerName,
    DateTime OrderDate,
    DateTime RequestedDeliveryDate,
    int ItemCount,
    decimal OrderAmount,
    string Priority,
    string Status,
    string Channel,
    bool IsCreditRisk);
