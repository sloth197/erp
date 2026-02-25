namespace Erp.Infrastructure.Seeding;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
