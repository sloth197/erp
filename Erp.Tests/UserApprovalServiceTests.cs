using Erp.Application.Authorization;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Security;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class UserApprovalServiceTests
{
    [Fact]
    public async Task ApproveAsync_ActivatesPendingUser_AndAllowsLogin()
    {
        var fixture = CreateFixture();
        var userId = await SeedPendingUserAsync(fixture, "pending-approve");

        await fixture.ApprovalService.ApproveAsync(new ApproveUserRequest(userId, AssignDefaultRole: true));

        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        var staffRole = await db.Roles.SingleAsync(x => x.Name == "Staff");
        var hasStaffRole = await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == staffRole.Id);
        var hasAudit = await db.AuditLogs.AnyAsync(x => x.Action == "User.Approved" && x.Target == user.Username);

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.IsActive);
        Assert.True(user.ApprovedAtUtc.HasValue);
        Assert.Equal(fixture.ActorUserId, user.ApprovedByUserId);
        Assert.True(hasStaffRole);
        Assert.True(hasAudit);

        var loginResult = await fixture.AuthService.LoginAsync("pending-approve", KnownPassword);
        Assert.True(loginResult.Success);
    }

    [Fact]
    public async Task RejectAsync_BlocksLogin_AndWritesAudit()
    {
        var fixture = CreateFixture();
        var userId = await SeedPendingUserAsync(fixture, "pending-reject");

        await fixture.ApprovalService.RejectAsync(new RejectUserRequest(userId, "policy"));

        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        var hasAudit = await db.AuditLogs.AnyAsync(x => x.Action == "User.Rejected" && x.Target == user.Username);

        Assert.Equal(UserStatus.Rejected, user.Status);
        Assert.False(user.IsActive);
        Assert.Equal("policy", user.RejectReason);
        Assert.True(hasAudit);

        var loginResult = await fixture.AuthService.LoginAsync("pending-reject", KnownPassword);
        Assert.False(loginResult.Success);
        Assert.Equal("가입이 거절되었습니다.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task DisableAsync_BlocksLogin_AndWritesAudit()
    {
        var fixture = CreateFixture();
        var userId = await SeedActiveUserAsync(fixture, "active-disable");

        await fixture.ApprovalService.DisableAsync(userId, "manual");

        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        var hasAudit = await db.AuditLogs.AnyAsync(x => x.Action == "User.Disabled" && x.Target == user.Username);

        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.False(user.IsActive);
        Assert.True(user.DisabledAtUtc.HasValue);
        Assert.Equal(fixture.ActorUserId, user.DisabledByUserId);
        Assert.True(hasAudit);

        var loginResult = await fixture.AuthService.LoginAsync("active-disable", KnownPassword);
        Assert.False(loginResult.Success);
        Assert.Equal("비활성화된 계정입니다.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task ListPendingUsersAsync_FiltersByStatusAndKeyword()
    {
        var fixture = CreateFixture();
        _ = await SeedPendingUserAsync(fixture, "pending-user");

        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            var activeUser = new User("active-user", fixture.PasswordHasher.Hash(KnownPassword), "active-user@erp.local");
            activeUser.Approve();
            db.Users.Add(activeUser);
            await db.SaveChangesAsync();
        }

        var result = await fixture.ApprovalService.ListPendingUsersAsync(new Erp.Application.Queries.ListPendingUsersQuery
        {
            Keyword = "pending",
            Status = UserStatus.Pending,
            Page = 1,
            PageSize = 50
        });

        Assert.Single(result.Items);
        Assert.Equal("pending-user", result.Items[0].Username);
        Assert.Equal(UserStatus.Pending, result.Items[0].Status);
    }

    private const string KnownPassword = "Password!1";

    private static async Task<Guid> SeedPendingUserAsync(TestFixture fixture, string username)
    {
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = new User(username, fixture.PasswordHasher.Hash(KnownPassword), $"{username}@erp.local");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> SeedActiveUserAsync(TestFixture fixture, string username)
    {
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = new User(username, fixture.PasswordHasher.Hash(KnownPassword), $"{username}@erp.local");
        user.Approve();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(options);
        IPasswordHasher passwordHasher = new Pbkdf2PasswordHasher();
        var currentUserContext = new CurrentUserContext();
        var actorUserId = Guid.NewGuid();
        currentUserContext.SetAuthenticatedUser(actorUserId, "admin", [PermissionCodes.MasterUsersWrite]);

        var accessControl = new AccessControlService(currentUserContext);
        var approvalService = new UserApprovalService(factory, accessControl, currentUserContext);
        var authService = new AuthService(factory, passwordHasher, currentUserContext);

        return new TestFixture(factory, approvalService, authService, passwordHasher, actorUserId);
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        UserApprovalService ApprovalService,
        AuthService AuthService,
        IPasswordHasher PasswordHasher,
        Guid ActorUserId);

    private sealed class TestDbContextFactory : IDbContextFactory<ErpDbContext>
    {
        private readonly DbContextOptions<ErpDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ErpDbContext> options)
        {
            _options = options;
        }

        public ErpDbContext CreateDbContext()
        {
            return new ErpDbContext(_options);
        }

        public ValueTask<ErpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ErpDbContext(_options));
        }
    }
}

