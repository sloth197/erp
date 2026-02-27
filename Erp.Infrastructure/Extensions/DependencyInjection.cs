using Erp.Application.Interfaces;
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

        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessControl, AccessControlService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IRegistrationService, RegistrationService>();
        services.AddSingleton<IUserApprovalService, UserApprovalService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IItemCommandService, ItemCommandService>();
        services.AddSingleton<IItemQueryService, SearchItemsQueryHandler>();
        services.AddSingleton<IInventoryCommandService, InventoryCommandService>();
        services.AddSingleton<IInventoryQueryService, SearchStockOnHandQueryHandler>();

        services.AddSingleton<IDataSeeder, ErpDataSeeder>();

        return services;
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

    private static string ReplacePlaceholder(string source, string envName)
    {
        var envValue = Environment.GetEnvironmentVariable(envName);
        return source.Replace($"${{{envName}}}", envValue, StringComparison.Ordinal);
    }
}
