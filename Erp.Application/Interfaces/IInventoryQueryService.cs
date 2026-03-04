using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IInventoryQueryService
{
    Task<IReadOnlyList<WarehouseOptionDto>> GetWarehouseOptionsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationOptionDto>> GetLocationOptionsAsync(
        Guid warehouseId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItemOptionDto>> SearchItemOptionsAsync(
        string? keyword,
        int take = 30,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockBalanceLookupDto>> GetOnHandByItemsAsync(
        Guid warehouseId,
        Guid? locationId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockOnHandDto>> SearchStockOnHandAsync(
        SearchStockOnHandQuery query,
        CancellationToken cancellationToken = default);
}
