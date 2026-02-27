namespace Erp.Application.DTOs;

public sealed record StockTransactionResultDto(
    string TxNo,
    int LineCount,
    DateTime OccurredAtUtc,
    DateTime ProcessedAtUtc);
