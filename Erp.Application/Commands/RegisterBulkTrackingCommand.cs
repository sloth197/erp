namespace Erp.Application.Commands;

public sealed class RegisterBulkTrackingCommand
{
    public IReadOnlyCollection<Guid>? ShipmentIds { get; init; }
}
