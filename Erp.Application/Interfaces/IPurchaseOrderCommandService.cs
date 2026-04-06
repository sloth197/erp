using Erp.Application.Commands;
using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IPurchaseOrderCommandService
{
    Task<DocumentCommandResultDto> CreateDraftAsync(
        CreatePurchaseOrderDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<DocumentCommandResultDto> RequestApprovalAsync(
        RequestPurchaseOrderApprovalCommand command,
        CancellationToken cancellationToken = default);
}
