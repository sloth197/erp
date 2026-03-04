namespace Erp.Application.Commands;

public sealed class AdjustStockByCountCommand
{
    public string? TxNo { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public Guid WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public IReadOnlyList<AdjustStockByCountLineCommand> Lines { get; init; } = Array.Empty<AdjustStockByCountLineCommand>();
}

public sealed class AdjustStockByCountLineCommand
{
    public Guid ItemId { get; init; }
    public decimal CountedQty { get; init; }
    public string? Note { get; init; }
}
