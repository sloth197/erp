using Erp.Application.Interfaces;
using Erp.Infrastructure.Email;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Seeding;
using Erp.Infrastructure.Security;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = ResolveConfigPlaceholders(config.GetConnectionString("ErpDb"));
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("${", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:ErpDb is missing or unresolved. Set ERP_DB_NAME, ERP_DB_USER, and ERP_DB_PASSWORD.");
        }

        services.AddDbContextFactory<ErpDbContext>(options => options.UseNpgsql(connectionString));

        var smtpOptions = ResolveSmtpOptions(config);
        var emailVerificationOptions = ResolveEmailVerificationOptions(config);

        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddSingleton(smtpOptions);
        services.AddSingleton(emailVerificationOptions);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailVerificationService, EmailVerificationService>();
        services.AddSingleton<IAccessControl, AccessControlService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IRegistrationService, RegistrationService>();
        services.AddSingleton<IUserApprovalService, UserApprovalService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IItemCommandService, ItemCommandService>();
        services.AddSingleton<IItemQueryService, SearchItemsQueryHandler>();
        services.AddSingleton<IInventoryCommandService, InventoryCommandService>();
        services.AddSingleton<IInventoryQueryService, SearchStockOnHandQueryHandler>();
        services.AddSingleton<PurchaseOrderService>();
        services.AddSingleton<IPurchaseOrderQueryService>(sp => sp.GetRequiredService<PurchaseOrderService>());
        services.AddSingleton<IPurchaseOrderCommandService>(sp => sp.GetRequiredService<PurchaseOrderService>());
        services.AddSingleton<SalesOrderService>();
        services.AddSingleton<ISalesOrderQueryService>(sp => sp.GetRequiredService<SalesOrderService>());
        services.AddSingleton<ISalesOrderCommandService>(sp => sp.GetRequiredService<SalesOrderService>());
        services.AddSingleton<SalesShipmentService>();
        services.AddSingleton<ISalesShipmentQueryService>(sp => sp.GetRequiredService<SalesShipmentService>());
        services.AddSingleton<ISalesShipmentCommandService>(sp => sp.GetRequiredService<SalesShipmentService>());
        services.AddSingleton<IAuditLogQueryService, AuditLogQueryService>();
        services.AddSingleton<IHomeDashboardQueryService, HomeDashboardQueryService>();

        services.AddSingleton<IDataSeeder, ErpDataSeeder>();

        return services;
    }

    private static SmtpOptions ResolveSmtpOptions(IConfiguration config)
    {
        var section = config.GetSection("Smtp");

        var host = ResolveValue(section["Host"]);
        var portRaw = ResolveValue(section["Port"]);
        var securityModeRaw = ResolveValue(section["SecurityMode"]);
        var username = ResolveValue(section["Username"]);
        var password = ResolveValue(section["Password"]);
        var from = ResolveValue(section["From"]);

        var port = 587;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        var securityMode = ParseSecurityMode(securityModeRaw);

        return new SmtpOptions
        {
            Host = host ?? string.Empty,
            Port = port,
            SecurityMode = securityMode,
            Username = username,
            Password = password,
            From = string.IsNullOrWhiteSpace(from) ? "ERP <no-reply@example.com>" : from.Trim()
        };
    }

    private static EmailVerificationOptions ResolveEmailVerificationOptions(IConfiguration config)
    {
        var section = config.GetSection("EmailVerification");

        var codeLengthRaw = ResolveValue(section["CodeLength"]);
        var expiresInMinutesRaw = ResolveValue(section["ExpiresInMinutes"]);
        var maxAttemptCountRaw = ResolveValue(section["MaxAttemptCount"]);
        var defaultPurpose = ResolveValue(section["DefaultPurpose"]);
        var subject = ResolveValue(section["Subject"]);

        var codeLength = ParseIntOrDefault(codeLengthRaw, 8);
        var expiresInMinutes = ParseIntOrDefault(expiresInMinutesRaw, 3);
        var maxAttemptCount = ParseIntOrDefault(maxAttemptCountRaw, 5);

        return new EmailVerificationOptions
        {
            CodeLength = Math.Clamp(codeLength, 8, 8),
            ExpiresInMinutes = Math.Clamp(expiresInMinutes, 1, 60),
            MaxAttemptCount = Math.Clamp(maxAttemptCount, 1, 10),
            DefaultPurpose = string.IsNullOrWhiteSpace(defaultPurpose) ? "signup" : defaultPurpose.Trim().ToLowerInvariant(),
            Subject = string.IsNullOrWhiteSpace(subject) ? "[ERP] Verification Code" : subject.Trim()
        };
    }

    private static string? ResolveConfigPlaceholders(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ReplacePlaceholder(value, "ERP_DB_NAME")
            .Replace("${ERP_DB_USER}", Environment.GetEnvironmentVariable("ERP_DB_USER"), StringComparison.Ordinal)
            .Replace("${ERP_DB_PASSWORD}", Environment.GetEnvironmentVariable("ERP_DB_PASSWORD"), StringComparison.Ordinal)
            .Trim();
    }

    private static string? ResolveValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var envName = trimmed[2..^1];
            return Environment.GetEnvironmentVariable(envName);
        }

        return trimmed;
    }

    private static SmtpSecurityMode ParseSecurityMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SmtpSecurityMode.StartTls;
        }

        return Enum.TryParse<SmtpSecurityMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : SmtpSecurityMode.StartTls;
    }

    private static int ParseIntOrDefault(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string ReplacePlaceholder(string source, string envName)
    {
        var envValue = Environment.GetEnvironmentVariable(envName);
        return source.Replace($"${{{envName}}}", envValue, StringComparison.Ordinal);
    }
}
