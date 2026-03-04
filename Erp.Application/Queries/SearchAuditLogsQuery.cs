namespace Erp.Application.Queries;

public sealed class SearchAuditLogsQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Action { get; init; }
    public string? Actor { get; init; }
    public string? Keyword { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
