namespace Erp.Application.Commands;

public sealed class CreateSalesOrderCommand
{
    public string CustomerName { get; init; } = string.Empty;
    public DateTime? RequestedDeliveryDate { get; init; }
    public string Channel { get; init; } = "직판";
}
