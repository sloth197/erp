namespace Erp.Application.DTOs;

public sealed record OperationResultDto(
    string Message,
    int AffectedCount = 0);
