namespace Erp.Domain.Entities;

public sealed class Role
{
    private readonly List<UserRole> _userRoles = new();
    private readonly List<RolePermission> _rolePermissions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    private Role()
    {
    }

    public Role(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
    }
}
