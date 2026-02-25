namespace Erp.Domain.Entities;

public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    private UserRole()
    {
    }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }
}
