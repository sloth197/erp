namespace Erp.Application.Queries;

public sealed class ListPendingUsersQuery
{
    public string? Keyword { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
