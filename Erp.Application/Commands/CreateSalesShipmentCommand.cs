namespace Erp.Application.Commands;

public sealed class CreateSalesShipmentCommand
{
    public string Warehouse { get; init; } = "본사 창고";
    public string ShippingType { get; init; } = "택배";
}
