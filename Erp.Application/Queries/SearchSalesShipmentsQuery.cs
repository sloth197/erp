namespace Erp.Application.Queries;

public sealed class SearchSalesShipmentsQuery
{
    public string? Warehouse { get; init; }
    public string? ShippingType { get; init; }
    public DateTime? ShipmentDate { get; init; }
    public string? Status { get; init; }
}
