namespace Erp.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? CurrentUserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> PermissionCodes { get; }

    event EventHandler? Changed;

    bool HasPermission(string permissionCode);
}
