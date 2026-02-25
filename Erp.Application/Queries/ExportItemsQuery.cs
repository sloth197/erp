using Erp.Domain.Entities;

namespace Erp.Application.Queries;

public sealed class ExportItemsQuery
{
    public string? Keyword { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public TrackingType? TrackingType { get; init; }
    public string? SortBy { get; init; } = "itemCode";
    public string? SortDirection { get; init; } = "asc";
}
