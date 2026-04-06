namespace Erp.Application.Commands;

public sealed class RequestPurchaseOrderApprovalCommand
{
    public Guid PurchaseOrderId { get; init; }
}
