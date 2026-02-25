using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IItemQueryService
{
    Task<IReadOnlyList<ItemCategoryOptionDto>> GetItemCategoryOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResult<ItemListDto>> SearchItemsAsync(
        SearchItemsQuery query,
        CancellationToken cancellationToken = default);
}
