using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IInventoryQueryService
{
    Task<PagedResult<StockOnHandDto>> SearchStockOnHandAsync(
        SearchStockOnHandQuery query,
        CancellationToken cancellationToken = default);
}
