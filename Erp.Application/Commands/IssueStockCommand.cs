namespace Erp.Application.Commands;

public sealed class IssueStockCommand
{
    public string? TxNo { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public Guid WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public IReadOnlyList<IssueStockLineCommand> Lines { get; init; } = Array.Empty<IssueStockLineCommand>();
}

public sealed class IssueStockLineCommand
{
    public Guid ItemId { get; init; }
    public decimal Qty { get; init; }
    public string? LotNo { get; init; }
    public string? SerialNo { get; init; }
    public string? Note { get; init; }
}
