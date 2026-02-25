using Erp.Application.Exceptions;
using Erp.Application.Interfaces;

namespace Erp.Infrastructure.Security;

public sealed class AccessControlService : IAccessControl
{
    private readonly ICurrentUserContext _currentUser;

    public AccessControlService(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public void DemandAuthenticated()
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("Authentication is required.");
        }
    }

    public void DemandPermission(string permissionCode)
    {
        DemandAuthenticated();

        if (!_currentUser.HasPermission(permissionCode))
        {
            throw new ForbiddenException($"Permission '{permissionCode}' is required.");
        }
    }
}
