namespace Erp.Application.DTOs;

public sealed record HomeDashboardSummaryDto(
    int TotalItems,
    int ActiveItems,
    int WarehouseCount,
    int LocationCount,
    decimal TotalOnHandQty,
    int ActiveUserCount,
    int PendingUserCount,
    int StockTransactionsToday,
    int AuditLogsLast24Hours,
    DateTime SnapshotUtc);
