namespace Erp.Application.DTOs;

public sealed record PurchaseOrderListDto(
    Guid Id,
    string PoNumber,
    string SupplierName,
    string ItemSummary,
    DateTime OrderDate,
    DateTime DueDate,
    int ItemCount,
    decimal OrderAmount,
    string ReceiptStatus,
    string Owner,
    string Status);
