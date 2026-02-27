using Erp.Domain.Entities;

namespace Erp.Application.Queries;

public sealed class SearchStockOnHandQuery
{
    public string? Keyword { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public TrackingType? TrackingType { get; init; }
    public bool IncludeLocations { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? Sort { get; init; } = "itemCode:asc";
}
