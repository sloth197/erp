using Erp.Application.Commands;
using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IInventoryCommandService
{
    Task<StockTransactionResultDto> AdjustStockByCountAsync(
        AdjustStockByCountCommand command,
        CancellationToken cancellationToken = default);

    Task<StockTransactionResultDto> ReceiveStockAsync(
        ReceiveStockCommand command,
        CancellationToken cancellationToken = default);

    Task<StockTransactionResultDto> IssueStockAsync(
        IssueStockCommand command,
        CancellationToken cancellationToken = default);
}
