using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IPurchaseOrderQueryService
{
    Task<PurchaseOrderSearchResultDto> SearchPurchaseOrdersAsync(
        SearchPurchaseOrdersQuery query,
        CancellationToken cancellationToken = default);
}
