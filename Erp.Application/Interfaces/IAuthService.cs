using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password, string? ip = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? ip = null, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
