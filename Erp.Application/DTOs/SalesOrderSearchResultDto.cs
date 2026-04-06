namespace Erp.Application.DTOs;

public sealed record SalesOrderSearchResultDto(
    IReadOnlyList<SalesOrderListDto> Items,
    int TodayReceivedCount,
    int PendingShipmentCount,
    int PartialShipmentCount,
    int CreditRiskCount);
