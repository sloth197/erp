using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class SearchStockOnHandQueryHandler : IInventoryQueryService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;
    private const int MaxItemOptionTake = 200;

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IAccessControl _accessControl;
    private readonly ICurrentUserContext _currentUserContext;

    public SearchStockOnHandQueryHandler(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IAccessControl accessControl,
        ICurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _accessControl = accessControl;
        _currentUserContext = currentUserContext;
    }

    public async Task<IReadOnlyList<WarehouseOptionDto>> GetWarehouseOptionsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        DemandInventoryLookupPermission();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Warehouses.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Code)
            .Select(x => new WarehouseOptionDto(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocationOptionDto>> GetLocationOptionsAsync(
        Guid warehouseId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        DemandInventoryLookupPermission();

        if (warehouseId == Guid.Empty)
        {
            return Array.Empty<LocationOptionDto>();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Locations
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId);

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Code)
            .Select(x => new LocationOptionDto(x.Id, x.WarehouseId, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItemOptionDto>> SearchItemOptionsAsync(
        string? keyword,
        int take = 30,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        DemandInventoryLookupPermission();

        var normalizedTake = take < 1 ? 30 : Math.Min(take, MaxItemOptionTake);
        var normalizedKeyword = keyword?.Trim();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Items.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(x =>
                x.ItemCode.Contains(normalizedKeyword) ||
                x.Name.Contains(normalizedKeyword) ||
                (x.Barcode != null && x.Barcode.Contains(normalizedKeyword)));
        }

        return await query
            .OrderBy(x => x.ItemCode)
            .Take(normalizedTake)
            .Select(x => new InventoryItemOptionDto(
                x.Id,
                x.ItemCode,
                x.Name,
                x.TrackingType,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockBalanceLookupDto>> GetOnHandByItemsAsync(
        Guid warehouseId,
        Guid? locationId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken = default)
    {
        DemandInventoryLookupPermission();

        if (warehouseId == Guid.Empty || itemIds.Count == 0)
        {
            return Array.Empty<StockBalanceLookupDto>();
        }

        var normalizedItemIds = itemIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedItemIds.Length == 0)
        {
            return Array.Empty<StockBalanceLookupDto>();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.InventoryBalances
            .AsNoTracking()
            .Where(x =>
                x.WarehouseId == warehouseId &&
                x.LocationId == locationId &&
                normalizedItemIds.Contains(x.ItemId));

        return await query
            .GroupBy(x => new { x.ItemId, x.Item.ItemCode })
            .Select(g => new StockBalanceLookupDto(
                g.Key.ItemId,
                g.Key.ItemCode,
                g.Sum(x => x.QtyOnHand)))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockOnHandDto>> SearchStockOnHandAsync(
        SearchStockOnHandQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.InventoryStockRead);

        query ??= new SearchStockOnHandQuery();
        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var filtered = BuildFilteredQuery(db, query);
        IQueryable<StockOnHandRow> onHandQuery = query.IncludeLocations
            ? filtered.Select(x => new StockOnHandRow
            {
                ItemCode = x.ItemCode,
                ItemName = x.ItemName,
                WarehouseCode = x.WarehouseCode,
                LocationCode = x.LocationCode,
                QtyOnHand = x.QtyOnHand,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            : filtered
                .GroupBy(x => new { x.ItemCode, x.ItemName, x.WarehouseCode })
                .Select(g => new StockOnHandRow
                {
                    ItemCode = g.Key.ItemCode,
                    ItemName = g.Key.ItemName,
                    WarehouseCode = g.Key.WarehouseCode,
                    LocationCode = null,
                    QtyOnHand = g.Sum(x => x.QtyOnHand),
                    UpdatedAtUtc = g.Max(x => x.UpdatedAtUtc)
                });

        onHandQuery = ApplySorting(onHandQuery, query.Sort);

        var totalCount = await onHandQuery.CountAsync(cancellationToken);
        var rows = await onHandQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StockOnHandDto(
                x.ItemCode,
                x.ItemName,
                x.WarehouseCode,
                x.LocationCode,
                x.QtyOnHand,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockOnHandDto>(rows, totalCount, page, pageSize);
    }

    private static IQueryable<StockOnHandProjection> BuildFilteredQuery(
        ErpDbContext db,
        SearchStockOnHandQuery query)
    {
        var rows = db.InventoryBalances
            .AsNoTracking()
            .Select(x => new StockOnHandProjection
            {
                ItemCode = x.Item.ItemCode,
                ItemName = x.Item.Name,
                CategoryId = x.Item.CategoryId,
                ItemIsActive = x.Item.IsActive,
                TrackingType = x.Item.TrackingType,
                WarehouseId = x.WarehouseId,
                WarehouseCode = x.Warehouse.Code,
                LocationCode = x.Location != null ? x.Location.Code : null,
                QtyOnHand = x.QtyOnHand,
                UpdatedAtUtc = x.UpdatedAtUtc
            });

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            rows = rows.Where(x =>
                x.ItemCode.Contains(keyword) ||
                x.ItemName.Contains(keyword));
        }

        if (query.WarehouseId.HasValue)
        {
            rows = rows.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            rows = rows.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.IsActive.HasValue)
        {
            rows = rows.Where(x => x.ItemIsActive == query.IsActive.Value);
        }

        if (query.TrackingType.HasValue)
        {
            rows = rows.Where(x => x.TrackingType == query.TrackingType.Value);
        }

        return rows;
    }

    private static IQueryable<StockOnHandRow> ApplySorting(
        IQueryable<StockOnHandRow> query,
        string? sort)
    {
        var (sortBy, descending) = ParseSort(sort);

        return (sortBy, descending) switch
        {
            ("itemcode", false) => query.OrderBy(x => x.ItemCode),
            ("itemcode", true) => query.OrderByDescending(x => x.ItemCode),
            ("itemname", false) => query.OrderBy(x => x.ItemName),
            ("itemname", true) => query.OrderByDescending(x => x.ItemName),
            ("warehousecode", false) => query.OrderBy(x => x.WarehouseCode).ThenBy(x => x.ItemCode),
            ("warehousecode", true) => query.OrderByDescending(x => x.WarehouseCode).ThenBy(x => x.ItemCode),
            ("locationcode", false) => query.OrderBy(x => x.LocationCode).ThenBy(x => x.ItemCode),
            ("locationcode", true) => query.OrderByDescending(x => x.LocationCode).ThenBy(x => x.ItemCode),
            ("qtyonhand", false) => query.OrderBy(x => x.QtyOnHand).ThenBy(x => x.ItemCode),
            ("qtyonhand", true) => query.OrderByDescending(x => x.QtyOnHand).ThenBy(x => x.ItemCode),
            ("updatedat", false) => query.OrderBy(x => x.UpdatedAtUtc),
            ("updatedatutc", false) => query.OrderBy(x => x.UpdatedAtUtc),
            ("updatedat", true) => query.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedatutc", true) => query.OrderByDescending(x => x.UpdatedAtUtc),
            _ => query.OrderBy(x => x.ItemCode)
        };
    }

    private static (string SortBy, bool Descending) ParseSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return ("itemcode", false);
        }

        var raw = sort.Trim();
        var descending = false;

        if (raw.StartsWith("-", StringComparison.Ordinal))
        {
            descending = true;
            raw = raw[1..];
        }
        else if (raw.Contains(':', StringComparison.Ordinal))
        {
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            raw = parts[0];
            if (parts.Length > 1)
            {
                descending = string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
            }
        }

        var normalized = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "itemcode";
        }

        return (normalized, descending);
    }

    private void DemandInventoryLookupPermission()
    {
        if (_currentUserContext.HasPermission(PermissionCodes.InventoryStockRead) ||
            _currentUserContext.HasPermission(PermissionCodes.InventoryStockReceipt) ||
            _currentUserContext.HasPermission(PermissionCodes.InventoryStockIssue) ||
            _currentUserContext.HasPermission(PermissionCodes.InventoryStockAdjust))
        {
            return;
        }

        _accessControl.DemandPermission(PermissionCodes.InventoryStockRead);
    }

    private sealed class StockOnHandProjection
    {
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public Guid CategoryId { get; init; }
        public bool ItemIsActive { get; init; }
        public TrackingType TrackingType { get; init; }
        public Guid WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = string.Empty;
        public string? LocationCode { get; init; }
        public decimal QtyOnHand { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class StockOnHandRow
    {
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string WarehouseCode { get; init; } = string.Empty;
        public string? LocationCode { get; init; }
        public decimal QtyOnHand { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
