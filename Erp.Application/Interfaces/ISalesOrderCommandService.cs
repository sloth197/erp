using Erp.Application.Commands;
using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface ISalesOrderCommandService
{
    Task<DocumentCommandResultDto> CreateOrderAsync(
        CreateSalesOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<DocumentCommandResultDto> ConfirmOrderAsync(
        ConfirmSalesOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto> CreateDeliveryPlanAsync(
        CreateSalesDeliveryPlanCommand command,
        CancellationToken cancellationToken = default);
}
