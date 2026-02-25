using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleSummaryDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<UserSummaryDto> CreateUserAsync(string username, string password, string roleName, CancellationToken cancellationToken = default);
    Task DisableUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task GrantPermissionToRoleAsync(string roleName, string permissionCode, CancellationToken cancellationToken = default);
}
