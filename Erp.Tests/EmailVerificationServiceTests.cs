using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class EmailVerificationServiceTests
{
    [Fact]
    public async Task SendCodeAsync_AllowsThreeSends_ThenAppliesCooldown()
    {
        var fixture = CreateFixture();
        var request = new SendEmailVerificationCodeRequest("tester@erp.local", "signup");

        var first = await fixture.Service.SendCodeAsync(request);
        var second = await fixture.Service.SendCodeAsync(request);
        var third = await fixture.Service.SendCodeAsync(request);
        var fourth = await fixture.Service.SendCodeAsync(request);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(third.Success);

        Assert.False(fourth.Success);
        Assert.Contains("3회", fourth.ErrorMessage);
        Assert.Contains("5분", fourth.ErrorMessage);
        Assert.Equal(3, fixture.EmailSender.SendCount);

        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(3, await db.EmailVerificationCodes.CountAsync(x => x.Email == "tester@erp.local"));
        Assert.True(await db.AuditLogs.AnyAsync(
            x => x.Action == "EmailVerification.SendCooldown" && x.Target == "tester@erp.local"));
    }

    [Fact]
    public async Task SendCodeAsync_CooldownIsScopedByEmail()
    {
        var fixture = CreateFixture();

        for (var i = 0; i < 3; i++)
        {
            var result = await fixture.Service.SendCodeAsync(
                new SendEmailVerificationCodeRequest("tester@erp.local", "signup"));

            Assert.True(result.Success);
        }

        var otherEmailResult = await fixture.Service.SendCodeAsync(
            new SendEmailVerificationCodeRequest("other@erp.local", "signup"));

        Assert.True(otherEmailResult.Success);
        Assert.Equal(4, fixture.EmailSender.SendCount);
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(options);
        var emailSender = new RecordingEmailSender();
        var emailVerificationOptions = new EmailVerificationOptions
        {
            CodeLength = 8,
            ExpiresInMinutes = 3,
            MaxAttemptCount = 5,
            MaxSendCountBeforeCooldown = 3,
            SendCooldownMinutes = 5,
            DefaultPurpose = "signup",
            Subject = "[ERP] Verification Code"
        };

        var service = new EmailVerificationService(factory, emailSender, emailVerificationOptions);

        return new TestFixture(factory, service, emailSender);
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        EmailVerificationService Service,
        RecordingEmailSender EmailSender);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }

        public Task SendAsync(
            string toEmail,
            string subject,
            string textBody,
            string? htmlBody = null,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }
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
}
