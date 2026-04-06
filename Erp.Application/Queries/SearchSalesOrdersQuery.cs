namespace Erp.Application.Queries;

public sealed class SearchSalesOrdersQuery
{
    public string? CustomerKeyword { get; init; }
    public DateTime? OrderDate { get; init; }
    public string? Channel { get; init; }
    public string? Status { get; init; }
}
