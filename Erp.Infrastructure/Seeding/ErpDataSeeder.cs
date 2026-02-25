using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Seeding;

public sealed class ErpDataSeeder : IDataSeeder
{
    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;

    public ErpDataSeeder(IDbContextFactory<ErpDbContext> dbContextFactory, IPasswordHasher passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var adminRole = await EnsureRoleAsync(db, "Admin", cancellationToken);
        var staffRole = await EnsureRoleAsync(db, "Staff", cancellationToken);

        var permissionMap = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in PermissionCodes.All)
        {
            var permission = await EnsurePermissionAsync(db, code, cancellationToken);
            permissionMap[code] = permission;
        }

        await EnsureRolePermissionsAsync(db, adminRole, permissionMap.Values, cancellationToken);

        var staffPermissions = new[]
        {
            PermissionCodes.MasterItemsRead,
            PermissionCodes.MasterPartnersRead,
            PermissionCodes.InventoryStockRead,
            PermissionCodes.PurchaseOrdersRead,
            PermissionCodes.SalesOrdersRead
        };

        await EnsureRolePermissionsAsync(
            db,
            staffRole,
            staffPermissions.Select(code => permissionMap[code]),
            cancellationToken);

        await EnsureUserAsync(db, "admin", "${ERP_SEED_ADMIN_PASSWORD}", adminRole, cancellationToken);
        await EnsureUserAsync(db, "staff", "${ERP_SEED_STAFF_PASSWORD}", staffRole, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(ErpDbContext db, string roleName, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role(roleName);
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static async Task<Permission> EnsurePermissionAsync(ErpDbContext db, string code, CancellationToken cancellationToken)
    {
        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (permission is not null)
        {
            return permission;
        }

        permission = new Permission(code, $"Permission for {code}");
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(cancellationToken);
        return permission;
    }

    private static async Task EnsureRolePermissionsAsync(
        ErpDbContext db,
        Role role,
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            var exists = await db.RolePermissions.AnyAsync(
                x => x.RoleId == role.Id && x.PermissionId == permission.Id,
                cancellationToken);

            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
            }
        }
    }

    private async Task EnsureUserAsync(
        ErpDbContext db,
        string username,
        string initialPassword,
        Role role,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (user is null)
        {
            user = new User(username, _passwordHasher.Hash(initialPassword));
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var hasRole = await db.UserRoles.AnyAsync(
            x => x.UserId == user.Id && x.RoleId == role.Id,
            cancellationToken);

        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole(user.Id, role.Id));
        }
    }
}
