namespace Erp.Application.DTOs;

public sealed record SalesShipmentSearchResultDto(
    IReadOnlyList<SalesShipmentListDto> Items,
    int TodayShipmentCount,
    int PickingWaitingCount,
    int PackedCount,
    int MissingTrackingCount);
