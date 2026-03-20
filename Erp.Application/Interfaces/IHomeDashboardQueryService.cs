using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IHomeDashboardQueryService
{
    Task<HomeDashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
