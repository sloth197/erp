using Erp.Domain.Entities;

namespace Erp.Application.Queries;

public sealed class SearchItemsQuery
{
    public string? Keyword { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public TrackingType? TrackingType { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; } = "updatedAtUtc";
    public string? SortDirection { get; init; } = "desc";
}
