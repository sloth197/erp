namespace Erp.Application.Commands;

public sealed class ReceiveStockCommand
{
    public string? TxNo { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public Guid WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public IReadOnlyList<ReceiveStockLineCommand> Lines { get; init; } = Array.Empty<ReceiveStockLineCommand>();
}

public sealed class ReceiveStockLineCommand
{
    public Guid ItemId { get; init; }
    public decimal Qty { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Note { get; init; }
}
