using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface ISalesOrderQueryService
{
    Task<SalesOrderSearchResultDto> SearchSalesOrdersAsync(
        SearchSalesOrdersQuery query,
        CancellationToken cancellationToken = default);
}
