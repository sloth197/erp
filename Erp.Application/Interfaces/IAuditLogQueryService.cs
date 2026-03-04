using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogListDto>> SearchAuditLogsAsync(
        SearchAuditLogsQuery query,
        CancellationToken cancellationToken = default);
}
