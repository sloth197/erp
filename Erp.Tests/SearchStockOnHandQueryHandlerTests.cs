using System.Diagnostics;
using Erp.Application.Authorization;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Application.Queries;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class SearchStockOnHandQueryHandlerTests
{
    [Fact]
    public async Task SearchStockOnHandAsync_ReturnsLocationRows_WhenIncludeLocationsTrue()
    {
        var (factory, fixture) = await BuildFixtureAsync();
        var accessControl = new RecordingAccessControl();
        var handler = new SearchStockOnHandQueryHandler(
            factory,
            accessControl,
            new FakeCurrentUserContext(PermissionCodes.InventoryStockRead));

        var result = await handler.SearchStockOnHandAsync(new SearchStockOnHandQuery
        {
            WarehouseId = fixture.MainWarehouseId,
            IncludeLocations = true,
            Keyword = "A-100",
            Sort = "locationCode:asc",
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(PermissionCodes.InventoryStockRead, accessControl.LastDemandedPermissionCode);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("A-01", result.Items[0].LocationCode);
        Assert.Equal(10m, result.Items[0].QtyOnHand);
        Assert.Equal("A-02", result.Items[1].LocationCode);
        Assert.Equal(5m, result.Items[1].QtyOnHand);
    }

    [Fact]
    public async Task SearchStockOnHandAsync_AggregatesRows_WhenIncludeLocationsFalse()
    {
        var (factory, fixture) = await BuildFixtureAsync();
        var handler = new SearchStockOnHandQueryHandler(
            factory,
            new RecordingAccessControl(),
            new FakeCurrentUserContext(PermissionCodes.InventoryStockRead));

        var result = await handler.SearchStockOnHandAsync(new SearchStockOnHandQuery
        {
            WarehouseId = fixture.MainWarehouseId,
            IncludeLocations = false,
            IsActive = true,
            Sort = "itemCode:asc",
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("A-100", result.Items[0].ItemCode);
        Assert.Null(result.Items[0].LocationCode);
        Assert.Equal(15m, result.Items[0].QtyOnHand);
        Assert.Equal("B-100", result.Items[1].ItemCode);
        Assert.Equal(7m, result.Items[1].QtyOnHand);
    }

    [Fact]
    public async Task SearchStockOnHandAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var (factory, _) = await BuildFixtureAsync();
        var handler = new SearchStockOnHandQueryHandler(
            factory,
            new DenyAccessControl(),
            new FakeCurrentUserContext());

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.SearchStockOnHandAsync(new SearchStockOnHandQuery()));
    }

    [Fact]
    public async Task SearchStockOnHandAsync_HandlesLargeDatasetWithPaging()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var db = new ErpDbContext(options))
        {
            var category = new ItemCategory("CAT", "Category");
            var uom = new UnitOfMeasure("EA", "Each");
            var warehouse = new Warehouse("MAIN", "Main Warehouse");
            db.ItemCategories.Add(category);
            db.UnitOfMeasures.Add(uom);
            db.Warehouses.Add(warehouse);

            for (var i = 1; i <= 2000; i++)
            {
                var item = new Item(
                    $"ITM-{i:0000}",
                    $"Item {i:0000}",
                    category.Id,
                    uom.Id,
                    TrackingType.None);

                db.Items.Add(item);
                db.InventoryBalances.Add(new InventoryBalance(item.Id, warehouse.Id, null, qtyOnHand: i));
            }

            await db.SaveChangesAsync();
        }

        var handler = new SearchStockOnHandQueryHandler(
            new TestDbContextFactory(options),
            new RecordingAccessControl(),
            new FakeCurrentUserContext(PermissionCodes.InventoryStockRead));

        var stopwatch = Stopwatch.StartNew();
        var result = await handler.SearchStockOnHandAsync(new SearchStockOnHandQuery
        {
            IncludeLocations = false,
            Sort = "qtyOnHand:desc",
            Page = 2,
            PageSize = 100
        });
        stopwatch.Stop();

        Assert.Equal(2000, result.TotalCount);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal(1900m, result.Items[0].QtyOnHand);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000);
    }

    private static async Task<(TestDbContextFactory Factory, SeedFixture Fixture)> BuildFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ErpDbContext(options);

        var categoryA = new ItemCategory("ELEC", "Electronics");
        var categoryB = new ItemCategory("FOOD", "Food");
        var uom = new UnitOfMeasure("EA", "Each");
        var mainWarehouse = new Warehouse("MAIN", "Main Warehouse");
        var subWarehouse = new Warehouse("SUB", "Sub Warehouse");
        var mainA01 = new Location(mainWarehouse.Id, "A-01", "A-01");
        var mainA02 = new Location(mainWarehouse.Id, "A-02", "A-02");

        db.ItemCategories.AddRange(categoryA, categoryB);
        db.UnitOfMeasures.Add(uom);
        db.Warehouses.AddRange(mainWarehouse, subWarehouse);
        db.Locations.AddRange(mainA01, mainA02);

        var itemA = new Item("A-100", "Alpha", categoryA.Id, uom.Id, TrackingType.None);
        var itemB = new Item("B-100", "Bravo", categoryA.Id, uom.Id, TrackingType.Batch);
        var itemC = new Item("C-100", "Charlie", categoryB.Id, uom.Id, TrackingType.Serial);
        itemC.Deactivate();

        db.Items.AddRange(itemA, itemB, itemC);

        db.InventoryBalances.AddRange(
            new InventoryBalance(itemA.Id, mainWarehouse.Id, mainA01.Id, qtyOnHand: 10m),
            new InventoryBalance(itemA.Id, mainWarehouse.Id, mainA02.Id, qtyOnHand: 5m),
            new InventoryBalance(itemB.Id, mainWarehouse.Id, null, qtyOnHand: 7m),
            new InventoryBalance(itemC.Id, subWarehouse.Id, null, qtyOnHand: 20m));

        await db.SaveChangesAsync();

        return (new TestDbContextFactory(options), new SeedFixture(mainWarehouse.Id));
    }

    private sealed record SeedFixture(Guid MainWarehouseId);

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

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        private readonly HashSet<string> _permissions;

        public FakeCurrentUserContext(params string[] permissions)
        {
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public Guid? CurrentUserId { get; } = Guid.NewGuid();
        public string? Username { get; } = "tester";
        public string? Email { get; } = "tester@example.com";
        public string? Name { get; } = "Tester";
        public string? Company { get; } = "ERP";
        public string? PhoneNumber { get; } = "010-0000-0000";
        public UserJobGrade? JobGrade { get; } = UserJobGrade.Staff;
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> PermissionCodes => _permissions;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public bool HasPermission(string permissionCode)
        {
            return _permissions.Contains(permissionCode);
        }
    }
}
