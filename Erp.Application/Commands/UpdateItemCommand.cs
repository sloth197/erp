using Erp.Domain.Entities;

namespace Erp.Application.Commands;

public sealed class UpdateItemCommand
{
    public Guid ItemId { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public string ItemCode { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public Guid UnitOfMeasureId { get; init; }
    public TrackingType TrackingType { get; init; }
}
