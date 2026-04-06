namespace Erp.Application.DTOs;

public sealed record DocumentCommandResultDto(
    Guid Id,
    string DocumentNo,
    string Status,
    string Message);
