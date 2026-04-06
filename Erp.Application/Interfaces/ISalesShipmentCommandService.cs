using Erp.Application.Commands;
using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface ISalesShipmentCommandService
{
    Task<DocumentCommandResultDto> CreateShipmentAsync(
        CreateSalesShipmentCommand command,
        CancellationToken cancellationToken = default);

    Task<DocumentCommandResultDto> ConfirmShipmentAsync(
        ConfirmSalesShipmentCommand command,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto> RegisterBulkTrackingAsync(
        RegisterBulkTrackingCommand command,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto> CloseShipmentDayAsync(
        CloseShipmentDayCommand command,
        CancellationToken cancellationToken = default);
}
