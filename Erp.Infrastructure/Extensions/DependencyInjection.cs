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
        var connectionString = config.GetConnectionString("ErpDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ErpDb' was not found. Configure ConnectionStrings:ErpDb.");
        }

        services.AddDbContextFactory<ErpDbContext>(options => options.UseNpgsql(connectionString));

        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessControl, AccessControlService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IItemCommandService, ItemCommandService>();
        services.AddSingleton<IItemQueryService, SearchItemsQueryHandler>();

        services.AddSingleton<IDataSeeder, ErpDataSeeder>();

        return services;
    }
}
