using Erp.Application.Interfaces;

namespace Erp.Infrastructure.Security;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly HashSet<string> _permissionCodes = new(StringComparer.OrdinalIgnoreCase);

    public Guid? CurrentUserId { get; private set; }
    public string? Username { get; private set; }
    public bool IsAuthenticated => CurrentUserId.HasValue;
    public IReadOnlyCollection<string> PermissionCodes => _permissionCodes;

    public event EventHandler? Changed;

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        return _permissionCodes.Contains(permissionCode);
    }

    public void SetAuthenticatedUser(Guid userId, string username, IEnumerable<string> permissionCodes)
    {
        CurrentUserId = userId;
        Username = username;

        _permissionCodes.Clear();
        foreach (var permission in permissionCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            _permissionCodes.Add(permission);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        CurrentUserId = null;
        Username = null;
        _permissionCodes.Clear();

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
