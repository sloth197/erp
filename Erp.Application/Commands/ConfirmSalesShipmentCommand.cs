namespace Erp.Application.Commands;

public sealed class ConfirmSalesShipmentCommand
{
    public Guid ShipmentId { get; init; }
    public bool IsPickingCompleted { get; init; }
    public bool IsPackingCompleted { get; init; }
    public bool IsTrackingCompleted { get; init; }
    public string? WorkNote { get; init; }
}
