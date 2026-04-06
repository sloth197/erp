namespace Erp.Application.DTOs;

public sealed record PurchaseOrderSearchResultDto(
    IReadOnlyList<PurchaseOrderListDto> Items,
    int WeekOrderCount,
    int PendingApprovalCount,
    int DelayedCount,
    decimal WeekOrderAmount);
