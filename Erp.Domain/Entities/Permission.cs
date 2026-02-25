namespace Erp.Domain.Entities;

public sealed class Permission
{
    private readonly List<RolePermission> _rolePermissions = new();

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    private Permission()
    {
    }

    public Permission(string code, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Permission code is required.", nameof(code));
        }

        Id = Guid.NewGuid();
        Code = code.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
