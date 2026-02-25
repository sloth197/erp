using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class SearchItemsQueryHandler : IItemQueryService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IAccessControl _accessControl;
    private readonly ICurrentUserContext _currentUserContext;

    public SearchItemsQueryHandler(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IAccessControl accessControl,
        ICurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _accessControl = accessControl;
        _currentUserContext = currentUserContext;
    }

    public async Task<IReadOnlyList<ItemCategoryOptionDto>> GetItemCategoryOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsRead);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ItemCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ItemCategoryOptionDto(x.Id, x.CategoryCode, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ItemListDto>> SearchItemsAsync(
        SearchItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsRead);

        query ??= new SearchItemsQuery();
        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = BuildFilteredItemsQuery(
            db,
            query.Keyword,
            query.CategoryId,
            query.IsActive,
            query.TrackingType);

        items = ApplySorting(items, query.SortBy, query.SortDirection);

        var totalCount = await items.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;

        var rows = await ProjectToItemListDtos(items)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemListDto>(rows, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<ItemListDto>> ExportItemsAsync(
        ExportItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        query ??= new ExportItemsQuery();
        DemandItemsExportPermission();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = BuildFilteredItemsQuery(
            db,
            query.Keyword,
            query.CategoryId,
            query.IsActive,
            query.TrackingType);

        items = ApplySorting(items, query.SortBy, query.SortDirection);

        return await ProjectToItemListDtos(items).ToListAsync(cancellationToken);
    }

    private void DemandItemsExportPermission()
    {
        var hasExportPermission = _currentUserContext.HasPermission(PermissionCodes.MasterItemsExport);
        var hasWritePermission = _currentUserContext.HasPermission(PermissionCodes.MasterItemsWrite);

        if (hasExportPermission || hasWritePermission)
        {
            return;
        }

        _accessControl.DemandPermission(PermissionCodes.MasterItemsExport);
    }

    private static IQueryable<Item> BuildFilteredItemsQuery(
        ErpDbContext db,
        string? keyword,
        Guid? categoryId,
        bool? isActive,
        TrackingType? trackingType)
    {
        IQueryable<Item> items = db.Items
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.UnitOfMeasure);

        var normalizedKeyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            items = items.Where(x =>
                x.ItemCode.Contains(normalizedKeyword) ||
                x.Name.Contains(normalizedKeyword) ||
                (x.Barcode != null && x.Barcode.Contains(normalizedKeyword)));
        }

        if (categoryId.HasValue)
        {
            items = items.Where(x => x.CategoryId == categoryId.Value);
        }

        if (isActive.HasValue)
        {
            items = items.Where(x => x.IsActive == isActive.Value);
        }

        if (trackingType.HasValue)
        {
            items = items.Where(x => x.TrackingType == trackingType.Value);
        }

        return items;
    }

    private static IQueryable<ItemListDto> ProjectToItemListDtos(IQueryable<Item> items)
    {
        return items.Select(x => new ItemListDto(
            x.Id,
            x.ItemCode,
            x.Barcode,
            x.Name,
            x.CategoryId,
            x.Category.CategoryCode,
            x.Category.Name,
            x.UnitOfMeasureId,
            x.UnitOfMeasure.UomCode,
            x.UnitOfMeasure.Name,
            x.TrackingType,
            x.IsActive,
            x.RowVersion,
            x.CreatedAtUtc,
            x.UpdatedAtUtc));
    }

    private static IQueryable<Item> ApplySorting(
        IQueryable<Item> query,
        string? sortBy,
        string? sortDirection)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return (normalizedSortBy, descending) switch
        {
            ("itemcode", false) => query.OrderBy(x => x.ItemCode),
            ("itemcode", true) => query.OrderByDescending(x => x.ItemCode),
            ("name", false) => query.OrderBy(x => x.Name),
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("barcode", false) => query.OrderBy(x => x.Barcode),
            ("barcode", true) => query.OrderByDescending(x => x.Barcode),
            ("category", false) => query.OrderBy(x => x.Category.Name).ThenBy(x => x.ItemCode),
            ("category", true) => query.OrderByDescending(x => x.Category.Name).ThenBy(x => x.ItemCode),
            ("trackingtype", false) => query.OrderBy(x => x.TrackingType).ThenBy(x => x.ItemCode),
            ("trackingtype", true) => query.OrderByDescending(x => x.TrackingType).ThenBy(x => x.ItemCode),
            ("isactive", false) => query.OrderBy(x => x.IsActive).ThenBy(x => x.ItemCode),
            ("isactive", true) => query.OrderByDescending(x => x.IsActive).ThenBy(x => x.ItemCode),
            ("createdatutc", false) => query.OrderBy(x => x.CreatedAtUtc),
            ("createdatutc", true) => query.OrderByDescending(x => x.CreatedAtUtc),
            ("updatedatutc", false) => query.OrderBy(x => x.UpdatedAtUtc),
            _ => query.OrderByDescending(x => x.UpdatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return string.Empty;
        }

        var normalized = new string(sortBy.Trim().Where(char.IsLetterOrDigit).ToArray());
        return normalized.ToLowerInvariant();
    }
}
