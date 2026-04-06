namespace Erp.Application.Queries;

public sealed class SearchPurchaseOrdersQuery
{
    public string? SupplierKeyword { get; init; }
    public string? ItemKeyword { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Status { get; init; }
}
