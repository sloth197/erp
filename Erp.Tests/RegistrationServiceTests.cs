using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Security;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_CreatesPendingUser_AndAuditLog()
    {
        var fixture = CreateFixture();

        var result = await fixture.RegistrationService.RegisterAsync(
            new RegisterRequest("signup-user", "Password!1", "signup@erp.local", "홍길동", "010-1234-5678", "테스트회사"));

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        await using var db = await fixture.Factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Username == "signup-user");

        Assert.Equal("signup@erp.local", user.Email);
        Assert.Equal("홍길동", user.Name);
        Assert.Equal("010-1234-5678", user.PhoneNumber);
        Assert.Equal("테스트회사", user.Company);
        Assert.Equal(UserStatus.Pending, user.Status);
        Assert.False(user.IsActive);

        var hasAudit = await db.AuditLogs.AnyAsync(
            x => x.Action == "User.Registered" && x.Target == "signup-user");
        Assert.True(hasAudit);
    }

    [Fact]
    public async Task LoginAsync_FailsForPendingUser_WithExpectedMessage()
    {
        var fixture = CreateFixture();

        var registerResult = await fixture.RegistrationService.RegisterAsync(
            new RegisterRequest("pending-user", "Password!1", null, "테스터", "010-1111-2222", "테스트회사"));
        Assert.True(registerResult.Success);

        var loginResult = await fixture.AuthService.LoginAsync("pending-user", "Password!1");

        Assert.False(loginResult.Success);
        Assert.Equal("\uC774\uBA54\uC77C \uC778\uC99D\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.", loginResult.ErrorMessage);
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(options);
        IPasswordHasher passwordHasher = new Pbkdf2PasswordHasher();
        var currentUserContext = new CurrentUserContext();

        var registrationService = new RegistrationService(factory, passwordHasher);
        var authService = new AuthService(factory, passwordHasher, currentUserContext);

        return new TestFixture(factory, registrationService, authService);
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        RegistrationService RegistrationService,
        AuthService AuthService);

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
