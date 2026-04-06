namespace Erp.Application.Commands;

public sealed class CreatePurchaseOrderDraftCommand
{
    public string SupplierName { get; init; } = string.Empty;
    public string ItemSummary { get; init; } = "신규 품목 1건";
    public DateTime? DueDate { get; init; }
}
