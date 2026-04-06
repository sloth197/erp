using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface ISalesShipmentQueryService
{
    Task<SalesShipmentSearchResultDto> SearchSalesShipmentsAsync(
        SearchSalesShipmentsQuery query,
        CancellationToken cancellationToken = default);
}
