using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IAccessControl _accessControl;

    public AuditLogQueryService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IAccessControl accessControl)
    {
        _dbContextFactory = dbContextFactory;
        _accessControl = accessControl;
    }

    public async Task<PagedResult<AuditLogListDto>> SearchAuditLogsAsync(
        SearchAuditLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.AuditRead);

        query ??= new SearchAuditLogsQuery();
        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var logs = db.AuditLogs
            .AsNoTracking()
            .Include(x => x.ActorUser)
            .AsQueryable();

        if (query.FromUtc.HasValue)
        {
            var fromUtc = NormalizeUtc(query.FromUtc.Value);
            logs = logs.Where(x => x.CreatedAtUtc >= fromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            var toUtc = NormalizeUtc(query.ToUtc.Value);
            logs = logs.Where(x => x.CreatedAtUtc <= toUtc);
        }

        var action = query.Action?.Trim();
        if (!string.IsNullOrWhiteSpace(action))
        {
            logs = logs.Where(x => x.Action.Contains(action));
        }

        var actor = query.Actor?.Trim();
        if (!string.IsNullOrWhiteSpace(actor))
        {
            logs = logs.Where(x => x.ActorUser != null && x.ActorUser.Username.Contains(actor));
        }

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            logs = logs.Where(x =>
                (x.Target != null && x.Target.Contains(keyword)) ||
                (x.DetailJson != null && x.DetailJson.Contains(keyword)));
        }

        var totalCount = await logs.CountAsync(cancellationToken);
        var rows = await logs
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogListDto(
                x.CreatedAtUtc,
                x.Action,
                x.ActorUserId,
                x.ActorUser != null ? x.ActorUser.Username : null,
                x.Target,
                x.DetailJson,
                x.Ip))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListDto>(rows, totalCount, page, pageSize);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }
}
