using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureItems(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureRoles(modelBuilder);
        ConfigurePermissions(modelBuilder);
        ConfigureUserRoles(modelBuilder);
        ConfigureRolePermissions(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
    }

    private static void ConfigureItems(ModelBuilder modelBuilder)
    {
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

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("users");
        user.HasKey(x => x.Id);

        user.Property(x => x.Id).HasColumnName("id");
        user.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(100)
            .IsRequired();
        user.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();
        user.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();
        user.Property(x => x.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .IsRequired();
        user.Property(x => x.LockoutEndUtc)
            .HasColumnName("lockout_end_utc");
        user.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        user.HasIndex(x => x.Username).IsUnique();

        user.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId);
    }

    private static void ConfigureRoles(ModelBuilder modelBuilder)
    {
        var role = modelBuilder.Entity<Role>();
        role.ToTable("roles");
        role.HasKey(x => x.Id);

        role.Property(x => x.Id).HasColumnName("id");
        role.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        role.HasIndex(x => x.Name).IsUnique();

        role.HasMany(x => x.UserRoles)
            .WithOne(x => x.Role)
            .HasForeignKey(x => x.RoleId);

        role.HasMany(x => x.RolePermissions)
            .WithOne(x => x.Role)
            .HasForeignKey(x => x.RoleId);
    }

    private static void ConfigurePermissions(ModelBuilder modelBuilder)
    {
        var permission = modelBuilder.Entity<Permission>();
        permission.ToTable("permissions");
        permission.HasKey(x => x.Id);

        permission.Property(x => x.Id).HasColumnName("id");
        permission.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(200)
            .IsRequired();
        permission.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        permission.HasIndex(x => x.Code).IsUnique();

        permission.HasMany(x => x.RolePermissions)
            .WithOne(x => x.Permission)
            .HasForeignKey(x => x.PermissionId);
    }

    private static void ConfigureUserRoles(ModelBuilder modelBuilder)
    {
        var userRole = modelBuilder.Entity<UserRole>();
        userRole.ToTable("user_roles");
        userRole.HasKey(x => new { x.UserId, x.RoleId });

        userRole.Property(x => x.UserId).HasColumnName("user_id");
        userRole.Property(x => x.RoleId).HasColumnName("role_id");
    }

    private static void ConfigureRolePermissions(ModelBuilder modelBuilder)
    {
        var rolePermission = modelBuilder.Entity<RolePermission>();
        rolePermission.ToTable("role_permissions");
        rolePermission.HasKey(x => new { x.RoleId, x.PermissionId });

        rolePermission.Property(x => x.RoleId).HasColumnName("role_id");
        rolePermission.Property(x => x.PermissionId).HasColumnName("permission_id");
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AuditLog>();
        audit.ToTable("audit_logs");
        audit.HasKey(x => x.Id);

        audit.Property(x => x.Id).HasColumnName("id");
        audit.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        audit.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(200)
            .IsRequired();
        audit.Property(x => x.Target)
            .HasColumnName("target")
            .HasMaxLength(200);
        audit.Property(x => x.DetailJson)
            .HasColumnName("detail_json")
            .HasMaxLength(4000);
        audit.Property(x => x.Ip)
            .HasColumnName("ip")
            .HasMaxLength(100);
        audit.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        audit.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        audit.HasIndex(x => x.CreatedAtUtc);
        audit.HasIndex(x => x.ActorUserId);
    }
}
