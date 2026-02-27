using Erp.Application.DTOs;
using Erp.Application.Queries;

namespace Erp.Application.Interfaces;

public interface IUserApprovalService
{
    Task<PagedResult<PendingUserDto>> ListPendingUsersAsync(
        ListPendingUsersQuery query,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(
        ApproveUserRequest request,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        RejectUserRequest request,
        CancellationToken cancellationToken = default);

    Task DisableAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
