using System.Text.Json;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessControl _accessControl;
    private readonly ICurrentUserContext _currentUserContext;

    public UserService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        IAccessControl accessControl,
        ICurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _accessControl = accessControl;
        _currentUserContext = currentUserContext;
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersRead);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);

        return users.Select(MapUser).ToList();
    }

    public async Task<IReadOnlyList<RoleSummaryDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersRead);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var roles = await db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new RoleSummaryDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        return roles;
    }

    public async Task<UserSummaryDto> CreateUserAsync(
        string username,
        string password,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("사용자명을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("비밀번호는 8자 이상이어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new InvalidOperationException("기본 역할을 선택하세요.");
        }

        var normalizedUsername = username.Trim();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await db.Users.AnyAsync(x => x.Username == normalizedUsername, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("동일한 사용자명이 이미 존재합니다.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName.Trim(), cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException("선택한 역할을 찾을 수 없습니다.");
        }

        var user = new User(normalizedUsername, _passwordHasher.Hash(password));
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole(user.Id, role.Id));

        db.AuditLogs.Add(new AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "User.Created",
            target: user.Username,
            detailJson: SerializeDetail(new { role = role.Name }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);

        return MapUser(user, role.Name);
    }

    public async Task DisableUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("사용자를 찾을 수 없습니다.");
        }

        user.Disable();

        db.AuditLogs.Add(new AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "User.Disabled",
            target: user.Username,
            detailJson: null,
            ip: null));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new InvalidOperationException("역할을 선택하세요.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("사용자를 찾을 수 없습니다.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName.Trim(), cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException("역할을 찾을 수 없습니다.");
        }

        var exists = await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, cancellationToken);
        if (!exists)
        {
            db.UserRoles.Add(new UserRole(user.Id, role.Id));
            db.AuditLogs.Add(new AuditLog(
                actorUserId: _currentUserContext.CurrentUserId,
                action: "User.RoleAssigned",
                target: user.Username,
                detailJson: SerializeDetail(new { role = role.Name }),
                ip: null));

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task GrantPermissionToRoleAsync(
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(permissionCode))
        {
            throw new InvalidOperationException("역할 및 권한 코드는 필수입니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName.Trim(), cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException("역할을 찾을 수 없습니다.");
        }

        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == permissionCode.Trim(), cancellationToken);
        if (permission is null)
        {
            throw new InvalidOperationException("권한 코드를 찾을 수 없습니다.");
        }

        var exists = await db.RolePermissions.AnyAsync(
            x => x.RoleId == role.Id && x.PermissionId == permission.Id,
            cancellationToken);

        if (!exists)
        {
            db.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
            db.AuditLogs.Add(new AuditLog(
                actorUserId: _currentUserContext.CurrentUserId,
                action: "Role.PermissionGranted",
                target: role.Name,
                detailJson: SerializeDetail(new { permission = permission.Code }),
                ip: null));

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static UserSummaryDto MapUser(User user)
    {
        var roles = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToList();
        return new UserSummaryDto(user.Id, user.Username, user.IsActive, user.FailedLoginCount, user.LockoutEndUtc, roles);
    }

    private static UserSummaryDto MapUser(User user, string roleName)
    {
        return new UserSummaryDto(
            user.Id,
            user.Username,
            user.IsActive,
            user.FailedLoginCount,
            user.LockoutEndUtc,
            new List<string> { roleName });
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
