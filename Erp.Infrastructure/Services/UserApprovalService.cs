using System.Text.Json;
using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class UserApprovalService : IUserApprovalService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const string DefaultRoleName = "Staff";

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IAccessControl _accessControl;
    private readonly ICurrentUserContext _currentUserContext;

    public UserApprovalService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IAccessControl accessControl,
        ICurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _accessControl = accessControl;
        _currentUserContext = currentUserContext;
    }

    public async Task<PagedResult<PendingUserDto>> ListPendingUsersAsync(
        ListPendingUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        query ??= new ListPendingUsersQuery();
        var page = query.Page < 1 ? DefaultPage : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = db.Users.AsNoTracking();

        var status = query.Status;
        if (status.HasValue)
        {
            users = users.Where(x => x.Status == status.Value);
        }
        else
        {
            users = users.Where(x => x.Status == UserStatus.Pending);
        }

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            users = users.Where(x =>
                x.Username.Contains(keyword) ||
                (x.Email != null && x.Email.Contains(keyword)));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var rows = await users
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PendingUserDto(
                x.Id,
                x.Username,
                x.Email,
                x.CreatedAtUtc,
                x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<PendingUserDto>(rows, totalCount, page, pageSize);
    }

    public async Task ApproveAsync(
        ApproveUserRequest request,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (request is null || request.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("승인할 사용자가 올바르지 않습니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("사용자를 찾을 수 없습니다.");
        }

        if (user.Status != UserStatus.Pending)
        {
            throw new InvalidOperationException("승인 대기 상태의 계정만 승인할 수 있습니다.");
        }

        user.Approve(_currentUserContext.CurrentUserId);

        string? assignedRole = null;
        if (request.AssignDefaultRole)
        {
            var defaultRole = await db.Roles
                .FirstOrDefaultAsync(x => x.Name == DefaultRoleName, cancellationToken);

            var roleWasCreated = false;
            if (defaultRole is null)
            {
                defaultRole = new Role(DefaultRoleName);
                db.Roles.Add(defaultRole);
                roleWasCreated = true;
            }

            var hasRole = false;
            if (!roleWasCreated)
            {
                hasRole = await db.UserRoles
                    .AnyAsync(x => x.UserId == user.Id && x.RoleId == defaultRole.Id, cancellationToken);
            }

            if (!hasRole)
            {
                db.UserRoles.Add(new UserRole(user.Id, defaultRole.Id));
                assignedRole = defaultRole.Name;
            }
        }

        db.AuditLogs.Add(new AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "User.Approved",
            target: user.Username,
            detailJson: SerializeDetail(new
            {
                user.Status,
                assignedRole,
                request.AssignDefaultRole
            }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(
        RejectUserRequest request,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (request is null || request.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("거절할 사용자가 올바르지 않습니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("사용자를 찾을 수 없습니다.");
        }

        user.Reject(_currentUserContext.CurrentUserId, request.Reason);

        db.AuditLogs.Add(new AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "User.Rejected",
            target: user.Username,
            detailJson: SerializeDetail(new { request.Reason, user.Status }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterUsersWrite);

        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("비활성화할 사용자가 올바르지 않습니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("사용자를 찾을 수 없습니다.");
        }

        user.Disable(_currentUserContext.CurrentUserId);

        db.AuditLogs.Add(new AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "User.Disabled",
            target: user.Username,
            detailJson: SerializeDetail(new { reason, user.Status }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
