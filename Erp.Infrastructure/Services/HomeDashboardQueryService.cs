using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class HomeDashboardQueryService : IHomeDashboardQueryService
{
    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;

    public HomeDashboardQueryService(IDbContextFactory<ErpDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<HomeDashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var last24HoursUtc = nowUtc.AddHours(-24);

        var totalItemsTask = db.Items.AsNoTracking().CountAsync(cancellationToken);
        var activeItemsTask = db.Items.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
        var warehouseCountTask = db.Warehouses.AsNoTracking().CountAsync(cancellationToken);
        var locationCountTask = db.Locations.AsNoTracking().CountAsync(cancellationToken);
        var totalOnHandQtyTask = db.InventoryBalances
            .AsNoTracking()
            .Select(x => (decimal?)x.QtyOnHand)
            .SumAsync(cancellationToken);

        var activeUserCountTask = db.Users
            .AsNoTracking()
            .CountAsync(x => x.Status == UserStatus.Active && x.IsActive, cancellationToken);
        var pendingUserCountTask = db.Users
            .AsNoTracking()
            .CountAsync(x => x.Status == UserStatus.Pending, cancellationToken);

        var stockTransactionsTodayTask = db.StockLedgerEntries
            .AsNoTracking()
            .CountAsync(x => x.OccurredAtUtc >= todayUtc, cancellationToken);
        var auditLogsLast24HoursTask = db.AuditLogs
            .AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= last24HoursUtc, cancellationToken);

        await Task.WhenAll(
            totalItemsTask,
            activeItemsTask,
            warehouseCountTask,
            locationCountTask,
            totalOnHandQtyTask,
            activeUserCountTask,
            pendingUserCountTask,
            stockTransactionsTodayTask,
            auditLogsLast24HoursTask);

        return new HomeDashboardSummaryDto(
            TotalItems: totalItemsTask.Result,
            ActiveItems: activeItemsTask.Result,
            WarehouseCount: warehouseCountTask.Result,
            LocationCount: locationCountTask.Result,
            TotalOnHandQty: totalOnHandQtyTask.Result ?? 0m,
            ActiveUserCount: activeUserCountTask.Result,
            PendingUserCount: pendingUserCountTask.Result,
            StockTransactionsToday: stockTransactionsTodayTask.Result,
            AuditLogsLast24Hours: auditLogsLast24HoursTask.Result,
            SnapshotUtc: nowUtc);
    }
}
