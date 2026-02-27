using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IRegistrationService
{
    Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);
}
