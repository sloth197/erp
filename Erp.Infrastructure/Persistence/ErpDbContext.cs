using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var item = modelBuilder.Entity<Item>();
        item.ToTable("items");
        item.HasKey(x => x.Id);

        item.Property(x => x.Code)
            .HasColumnName("code")
            .IsRequired()
            .HasMaxLength(30);

        item.HasIndex(x => x.Code)
            .IsUnique();

        item.Property(x => x.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        item.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
