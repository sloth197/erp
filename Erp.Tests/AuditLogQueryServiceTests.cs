using Erp.Application.Authorization;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class AuditLogQueryServiceTests
{
    [Fact]
    public async Task SearchAuditLogsAsync_FiltersByPeriodActionActorAndKeyword()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var now = DateTime.UtcNow;

        await using (var db = new ErpDbContext(options))
        {
            var admin = new User("admin", "hash");
            var staff = new User("staff", "hash");

            db.Users.AddRange(admin, staff);
            db.AuditLogs.AddRange(
                new AuditLog(admin.Id, "Stock.Receipt", "RCPT-20260304-0001", "{\"txNo\":\"RCPT-20260304-0001\"}"),
                new AuditLog(staff.Id, "Stock.Issue", "ISS-20260304-0001", "{\"txNo\":\"ISS-20260304-0001\"}"),
                new AuditLog(admin.Id, "Auth.LoginSucceeded", "admin", null));
            await db.SaveChangesAsync();
        }

        var accessControl = new RecordingAccessControl();
        var service = new AuditLogQueryService(new TestDbContextFactory(options), accessControl);

        var result = await service.SearchAuditLogsAsync(new SearchAuditLogsQuery
        {
            FromUtc = now.AddDays(-1),
            ToUtc = now.AddDays(1),
            Action = "Stock.",
            Actor = "admin",
            Keyword = "RCPT",
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(PermissionCodes.AuditRead, accessControl.LastDemandedPermissionCode);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Stock.Receipt", result.Items[0].Action);
        Assert.Equal("RCPT-20260304-0001", result.Items[0].Target);
    }

    [Fact]
    public async Task SearchAuditLogsAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var service = new AuditLogQueryService(new TestDbContextFactory(options), new DenyAccessControl());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SearchAuditLogsAsync(new SearchAuditLogsQuery()));
    }

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

    private sealed class RecordingAccessControl : IAccessControl
    {
        public string? LastDemandedPermissionCode { get; private set; }

        public void DemandAuthenticated()
        {
        }

        public void DemandPermission(string permissionCode)
        {
            LastDemandedPermissionCode = permissionCode;
        }
    }

    private sealed class DenyAccessControl : IAccessControl
    {
        public void DemandAuthenticated()
        {
        }

        public void DemandPermission(string permissionCode)
        {
            throw new ForbiddenException($"Permission '{permissionCode}' is required.");
        }
    }
}
