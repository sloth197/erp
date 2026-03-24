using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Erp.Infrastructure.Seeding;

public sealed class ErpDataSeeder : IDataSeeder
{
    private const int RandomInventoryTargetCount = 30;
    private const string SeedCategoryCode = "SEED";
    private const string SeedUomCode = "EA";
    private const string SeedItemCodePrefix = "SEED-ITEM-";
    private const string SeedReceiptTxPrefix = "SEED-RCV-";

    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;

    public ErpDataSeeder(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IPasswordHasher passwordHasher,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var adminRole = await EnsureRoleAsync(db, "Admin", cancellationToken);
        var staffRole = await EnsureRoleAsync(db, "Staff", cancellationToken);

        var permissionMap = new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in PermissionCodes.All)
        {
            var permission = await EnsurePermissionAsync(db, code, cancellationToken);
            permissionMap[code] = permission;
        }

        await EnsureRolePermissionsAsync(db, adminRole, permissionMap.Values, cancellationToken);

        var staffPermissions = new[]
        {
            PermissionCodes.NoticeRead,
            PermissionCodes.MasterItemsRead,
            PermissionCodes.MasterItemsExport,
            PermissionCodes.MasterPartnersRead,
            PermissionCodes.InventoryStockRead,
            PermissionCodes.PurchaseOrdersRead,
            PermissionCodes.SalesOrdersRead
        };

        await EnsureRolePermissionsAsync(
            db,
            staffRole,
            staffPermissions.Select(code => permissionMap[code]),
            cancellationToken);

        var adminUsername = _configuration["Seed:AdminUsername"] ?? "admin";
        var staffUsername = _configuration["Seed:StaffUsername"] ?? "staff";
        var adminPassword = ResolveSeedSecret("Seed:AdminPassword", "ERP_SEED_ADMIN_PASSWORD");
        var staffPassword = ResolveSeedSecret("Seed:StaffPassword", "ERP_SEED_STAFF_PASSWORD");

        await EnsureUserAsync(db, adminUsername, adminPassword, adminRole, cancellationToken);
        await EnsureUserAsync(db, staffUsername, staffPassword, staffRole, cancellationToken);
        await EnsureWarehouseSeedsAsync(db, cancellationToken);
        await EnsureRandomInventorySeedsAsync(db, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(ErpDbContext db, string roleName, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role(roleName);
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static async Task<Permission> EnsurePermissionAsync(ErpDbContext db, string code, CancellationToken cancellationToken)
    {
        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (permission is not null)
        {
            return permission;
        }

        permission = new Permission(code, $"Permission for {code}");
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(cancellationToken);
        return permission;
    }

    private static async Task EnsureRolePermissionsAsync(
        ErpDbContext db,
        Role role,
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            var exists = await db.RolePermissions.AnyAsync(
                x => x.RoleId == role.Id && x.PermissionId == permission.Id,
                cancellationToken);

            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
            }
        }
    }

    private async Task EnsureUserAsync(
        ErpDbContext db,
        string username,
        string? initialPassword,
        Role role,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(initialPassword))
            {
                return;
            }

            user = new User(username, _passwordHasher.Hash(initialPassword));
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (user.Status != UserStatus.Active || !user.IsActive)
        {
            user.Approve(approvedByUserId: null, approvedAtUtc: DateTime.UtcNow);
        }

        var hasRole = await db.UserRoles.AnyAsync(
            x => x.UserId == user.Id && x.RoleId == role.Id,
            cancellationToken);

        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole(user.Id, role.Id));
        }
    }

    private string? ResolveSeedSecret(string configKey, string envName)
    {
        var raw = _configuration[configKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Environment.GetEnvironmentVariable(envName);
        }

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, $"${{{envName}}}", StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(envName);
        }

        return trimmed;
    }

    private static async Task EnsureWarehouseSeedsAsync(ErpDbContext db, CancellationToken cancellationToken)
    {
        var mainWarehouse = await EnsureWarehouseAsync(db, "MAIN", "Main Warehouse", cancellationToken);
        _ = await EnsureLocationAsync(db, mainWarehouse.Id, "A-01", "A-01", cancellationToken);
        _ = await EnsureLocationAsync(db, mainWarehouse.Id, "A-02", "A-02", cancellationToken);
    }

    private static async Task<Warehouse> EnsureWarehouseAsync(
        ErpDbContext db,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (warehouse is not null)
        {
            return warehouse;
        }

        warehouse = new Warehouse(code, name);
        db.Warehouses.Add(warehouse);
        return warehouse;
    }

    private static async Task<Location> EnsureLocationAsync(
        ErpDbContext db,
        Guid warehouseId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(
            x => x.WarehouseId == warehouseId && x.Code == code,
            cancellationToken);

        if (location is not null)
        {
            return location;
        }

        location = new Location(warehouseId, code, name);
        db.Locations.Add(location);
        return location;
    }

    private static async Task EnsureRandomInventorySeedsAsync(ErpDbContext db, CancellationToken cancellationToken)
    {
        var currentBalanceCount = await db.InventoryBalances.CountAsync(cancellationToken);
        if (currentBalanceCount >= RandomInventoryTargetCount)
        {
            return;
        }

        var warehouse = await db.Warehouses
            .OrderBy(x => x.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (warehouse is null)
        {
            return;
        }

        var locationIds = await db.Locations
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouse.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var category = await EnsureItemCategoryAsync(db, SeedCategoryCode, "Seeded Items", cancellationToken);
        var unitOfMeasure = await EnsureUnitOfMeasureAsync(db, SeedUomCode, "Each", cancellationToken);

        var missingCount = RandomInventoryTargetCount - currentBalanceCount;
        var candidateItemIds = await GetCandidateItemIdsAsync(db, warehouse.Id, cancellationToken);

        if (candidateItemIds.Count < missingCount)
        {
            var createCount = missingCount - candidateItemIds.Count;
            await CreateSeedItemsAsync(db, category.Id, unitOfMeasure.Id, createCount, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            candidateItemIds = await GetCandidateItemIdsAsync(db, warehouse.Id, cancellationToken);
        }

        var receiptSequence = await GetNextSeedReceiptSequenceAsync(db, cancellationToken);
        var random = new Random();

        for (var i = 0; i < missingCount && i < candidateItemIds.Count; i++)
        {
            var itemId = candidateItemIds[i];
            var locationId = ResolveRandomLocation(locationIds, random);
            var qty = Math.Round((decimal)(random.NextDouble() * 900d + 10d), 2, MidpointRounding.AwayFromZero);
            var unitCost = Math.Round((decimal)(random.NextDouble() * 90d + 1d), 2, MidpointRounding.AwayFromZero);
            var occurredAtUtc = DateTime.UtcNow.AddMinutes(-random.Next(30, 14 * 24 * 60));
            var txNo = $"{SeedReceiptTxPrefix}{receiptSequence + i:000000}";

            var balanceId = Guid.NewGuid();
            var rowVersion = Guid.NewGuid().ToByteArray();
            var nowUtc = DateTime.UtcNow;

            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO inventory_balances (id, item_id, warehouse_id, location_id, qty_on_hand, qty_allocated, row_version, created_at_utc, updated_at_utc)
VALUES ({balanceId}, {itemId}, {warehouse.Id}, {locationId}, {qty}, {0m}, {rowVersion}, {nowUtc}, {nowUtc});", cancellationToken);

            db.StockLedgerEntries.Add(new StockLedgerEntry(
                txNo: txNo,
                txType: InventoryTxType.Receipt,
                itemId: itemId,
                warehouseId: warehouse.Id,
                qty: qty,
                occurredAtUtc: occurredAtUtc,
                locationId: locationId,
                unitCost: unitCost,
                note: "Seeded random inventory."));
        }
    }

    private static async Task<List<Guid>> GetCandidateItemIdsAsync(
        ErpDbContext db,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var itemIdsInWarehouse = await db.InventoryBalances
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId)
            .Select(x => x.ItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var excludedIds = itemIdsInWarehouse.ToHashSet();
        var activeItemIds = await db.Items
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ItemCode)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return activeItemIds
            .Where(x => !excludedIds.Contains(x))
            .ToList();
    }

    private static Guid? ResolveRandomLocation(IReadOnlyList<Guid> locationIds, Random random)
    {
        if (locationIds.Count == 0)
        {
            return null;
        }

        var useNullLocation = random.Next(0, 10) < 2;
        if (useNullLocation)
        {
            return null;
        }

        return locationIds[random.Next(locationIds.Count)];
    }

    private static async Task<ItemCategory> EnsureItemCategoryAsync(
        ErpDbContext db,
        string categoryCode,
        string name,
        CancellationToken cancellationToken)
    {
        var category = await db.ItemCategories
            .FirstOrDefaultAsync(x => x.CategoryCode == categoryCode, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new ItemCategory(categoryCode, name);
        db.ItemCategories.Add(category);
        return category;
    }

    private static async Task<UnitOfMeasure> EnsureUnitOfMeasureAsync(
        ErpDbContext db,
        string uomCode,
        string name,
        CancellationToken cancellationToken)
    {
        var unitOfMeasure = await db.UnitOfMeasures
            .FirstOrDefaultAsync(x => x.UomCode == uomCode, cancellationToken);
        if (unitOfMeasure is not null)
        {
            return unitOfMeasure;
        }

        unitOfMeasure = new UnitOfMeasure(uomCode, name);
        db.UnitOfMeasures.Add(unitOfMeasure);
        return unitOfMeasure;
    }

    private static async Task CreateSeedItemsAsync(
        ErpDbContext db,
        Guid categoryId,
        Guid unitOfMeasureId,
        int createCount,
        CancellationToken cancellationToken)
    {
        if (createCount <= 0)
        {
            return;
        }

        var existingCodes = await db.Items
            .AsNoTracking()
            .Select(x => x.ItemCode)
            .ToListAsync(cancellationToken);

        var codeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var nextSequence = existingCodes
            .Where(x => x.StartsWith(SeedItemCodePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => ExtractSequence(x, SeedItemCodePrefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        var created = 0;
        while (created < createCount)
        {
            var itemCode = $"{SeedItemCodePrefix}{nextSequence:0000}";
            nextSequence++;

            if (!codeSet.Add(itemCode))
            {
                continue;
            }

            db.Items.Add(new Item(
                itemCode: itemCode,
                name: $"Seed Item {itemCode[^4..]}",
                categoryId: categoryId,
                unitOfMeasureId: unitOfMeasureId,
                trackingType: TrackingType.None));

            created++;
        }
    }

    private static async Task<int> GetNextSeedReceiptSequenceAsync(
        ErpDbContext db,
        CancellationToken cancellationToken)
    {
        var txNos = await db.StockLedgerEntries
            .AsNoTracking()
            .Where(x => x.TxNo.StartsWith(SeedReceiptTxPrefix))
            .Select(x => x.TxNo)
            .ToListAsync(cancellationToken);

        var maxSequence = txNos
            .Select(x => ExtractSequence(x, SeedReceiptTxPrefix))
            .DefaultIfEmpty(0)
            .Max();

        return maxSequence + 1;
    }

    private static int ExtractSequence(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(value[prefix.Length..], out var parsed) ? parsed : 0;
    }
}
