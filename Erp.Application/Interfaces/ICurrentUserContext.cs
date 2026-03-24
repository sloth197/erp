namespace Erp.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid? CurrentUserId { get; }
    string? Username { get; }
    string? Email { get; }
    string? Name { get; }
    string? Company { get; }
    string? PhoneNumber { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> PermissionCodes { get; }

    event EventHandler? Changed;

    bool HasPermission(string permissionCode);
}
