using Erp.Domain.Entities;

namespace Erp.Application.Commands;

public sealed class CreateItemCommand
{
    public string ItemCode { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public Guid UnitOfMeasureId { get; init; }
    public TrackingType TrackingType { get; init; }
}
