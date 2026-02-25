using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IItemQueryService
{
    Task<PagedResult<ItemListDto>> SearchItemsAsync(
        SearchItemsQuery query,
        CancellationToken cancellationToken = default);
}
