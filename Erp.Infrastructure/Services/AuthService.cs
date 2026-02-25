using System.Text.Json;
using Erp.Application.DTOs;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedLoginCount = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly CurrentUserContext _currentUserContext;

    public AuthService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        CurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
    }

    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        string? ip = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failed("아이디와 비밀번호를 입력하세요.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                    .ThenInclude(x => x.RolePermissions)
                        .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

        if (user is null)
        {
            db.AuditLogs.Add(new AuditLog(
                actorUserId: null,
                action: "Auth.LoginFailed",
                target: normalizedUsername,
                detailJson: SerializeDetail(new { reason = "user_not_found" }),
                ip: ip));
            await db.SaveChangesAsync(cancellationToken);

            return LoginResult.Failed("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        var now = DateTime.UtcNow;

        if (!user.IsActive)
        {
            db.AuditLogs.Add(new AuditLog(
                actorUserId: user.Id,
                action: "Auth.LoginFailed",
                target: user.Username,
                detailJson: SerializeDetail(new { reason = "inactive_user" }),
                ip: ip));
            await db.SaveChangesAsync(cancellationToken);

            return LoginResult.Failed("비활성화된 계정입니다.");
        }

        if (user.IsLockedOut(now))
        {
            var remaining = user.GetLockoutRemainingSeconds(now);
            db.AuditLogs.Add(new AuditLog(
                actorUserId: user.Id,
                action: "Auth.LoginFailed",
                target: user.Username,
                detailJson: SerializeDetail(new { reason = "locked_out", remainingSeconds = remaining }),
                ip: ip));
            await db.SaveChangesAsync(cancellationToken);

            return LoginResult.Failed("계정이 잠겨 있습니다.", remaining);
        }

        if (!_passwordHasher.Verify(user.PasswordHash, password))
        {
            user.RegisterLoginFailure(now, MaxFailedLoginCount, LockoutDuration);

            db.AuditLogs.Add(new AuditLog(
                actorUserId: user.Id,
                action: "Auth.LoginFailed",
                target: user.Username,
                detailJson: SerializeDetail(new { reason = "invalid_password" }),
                ip: ip));

            if (user.IsLockedOut(now))
            {
                db.AuditLogs.Add(new AuditLog(
                    actorUserId: user.Id,
                    action: "Auth.LockoutTriggered",
                    target: user.Username,
                    detailJson: SerializeDetail(new { durationSeconds = (int)LockoutDuration.TotalSeconds }),
                    ip: ip));
            }

            await db.SaveChangesAsync(cancellationToken);

            if (user.IsLockedOut(now))
            {
                return LoginResult.Failed("로그인 실패가 누적되어 계정이 잠겼습니다.", user.GetLockoutRemainingSeconds(now));
            }

            return LoginResult.Failed("아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        user.RegisterLoginSuccess();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _currentUserContext.SetAuthenticatedUser(user.Id, user.Username, permissions);

        db.AuditLogs.Add(new AuditLog(
            actorUserId: user.Id,
            action: "Auth.LoginSucceeded",
            target: user.Username,
            detailJson: SerializeDetail(new { permissions = permissions.Length }),
            ip: ip));

        await db.SaveChangesAsync(cancellationToken);

        return LoginResult.Succeeded();
    }

    public async Task LogoutAsync(string? ip = null, CancellationToken cancellationToken = default)
    {
        if (_currentUserContext.IsAuthenticated)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLog(
                actorUserId: _currentUserContext.CurrentUserId,
                action: "Auth.Logout",
                target: _currentUserContext.Username,
                detailJson: null,
                ip: ip));
            await db.SaveChangesAsync(cancellationToken);
        }

        _currentUserContext.Clear();
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.CurrentUserId.HasValue)
        {
            throw new UnauthorizedException("로그인이 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new InvalidOperationException("새 비밀번호는 8자 이상이어야 합니다.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Id == _currentUserContext.CurrentUserId.Value,
            cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException("현재 사용자 정보를 찾을 수 없습니다.");
        }

        if (!_passwordHasher.Verify(user.PasswordHash, currentPassword))
        {
            throw new InvalidOperationException("현재 비밀번호가 올바르지 않습니다.");
        }

        user.SetPasswordHash(_passwordHasher.Hash(newPassword));

        db.AuditLogs.Add(new AuditLog(
            actorUserId: user.Id,
            action: "Auth.PasswordChanged",
            target: user.Username,
            detailJson: null,
            ip: null));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
