using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Erp.Tests;

public sealed class InventoryCommandServiceTests
{
    [Fact]
    public async Task ReceiveStockAsync_CreatesLedgerAndIncreasesBalance()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null);
        var accessControl = new RecordingAccessControl();
        var service = new InventoryCommandService(factory, accessControl, new FakeCurrentUserContext());

        var occurredAtUtc = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc);
        var result = await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            OccurredAtUtc = occurredAtUtc,
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines =
            [
                new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 10m, UnitCost = 12.5m, Note = "Receive" }
            ]
        });

        Assert.Equal(PermissionCodes.InventoryStockWrite, accessControl.LastDemandedPermissionCode);
        Assert.StartsWith("RCPT-20260227-", result.TxNo, StringComparison.Ordinal);

        await using var db = await factory.CreateDbContextAsync();
        var ledger = await db.StockLedgerEntries.SingleAsync(x => x.TxNo == result.TxNo);
        var balance = await db.InventoryBalances.SingleAsync(x =>
            x.ItemId == fixture.ItemId &&
            x.WarehouseId == fixture.WarehouseId &&
            x.LocationId == fixture.LocationId);
        var audit = await db.AuditLogs.SingleAsync(x => x.Action == "Stock.Receipt" && x.Target == result.TxNo);

        Assert.Equal(InventoryTxType.Receipt, ledger.TxType);
        Assert.Equal(10m, ledger.Qty);
        Assert.Equal(10m, balance.QtyOnHand);
        Assert.Contains(result.TxNo, audit.DetailJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueStockAsync_DecreasesBalance()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 20m);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var result = await service.IssueStockAsync(new IssueStockCommand
        {
            TxNo = "ISS-20260227-0001",
            OccurredAtUtc = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines =
            [
                new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 8m, Note = "Issue" }
            ]
        });

        Assert.Equal("ISS-20260227-0001", result.TxNo);

        await using var db = await factory.CreateDbContextAsync();
        var ledger = await db.StockLedgerEntries.SingleAsync(x => x.TxNo == result.TxNo);
        var balance = await db.InventoryBalances.SingleAsync(x =>
            x.ItemId == fixture.ItemId &&
            x.WarehouseId == fixture.WarehouseId &&
            x.LocationId == fixture.LocationId);

        Assert.Equal(-8m, ledger.Qty);
        Assert.Equal(12m, balance.QtyOnHand);
    }

    [Fact]
    public async Task IssueStockAsync_Throws_WhenInsufficientStock()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 3m);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssueStockAsync(new IssueStockCommand
        {
            TxNo = "ISS-20260227-0002",
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines =
            [
                new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 5m }
            ]
        }));

        Assert.Contains("Insufficient stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveAndIssue_ConcurrentOperations_KeepBalanceConsistent()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 100m);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var receiveTask = service.ReceiveStockAsync(new ReceiveStockCommand
        {
            TxNo = "RCPT-20260227-0001",
            OccurredAtUtc = new DateTime(2026, 2, 27, 12, 0, 0, DateTimeKind.Utc),
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 25m }]
        });

        var issueTask = service.IssueStockAsync(new IssueStockCommand
        {
            TxNo = "ISS-20260227-0003",
            OccurredAtUtc = new DateTime(2026, 2, 27, 12, 1, 0, DateTimeKind.Utc),
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 30m }]
        });

        await Task.WhenAll(receiveTask, issueTask);

        await using var db = await factory.CreateDbContextAsync();
        var balance = await db.InventoryBalances.SingleAsync(x =>
            x.ItemId == fixture.ItemId &&
            x.WarehouseId == fixture.WarehouseId &&
            x.LocationId == fixture.LocationId);

        Assert.Equal(95m, balance.QtyOnHand);
    }

    [Fact]
    public async Task ReceiveStockAsync_AutoGeneratesSequentialTxNo()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var occurredAtUtc = new DateTime(2026, 2, 27, 13, 0, 0, DateTimeKind.Utc);

        var first = await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            OccurredAtUtc = occurredAtUtc,
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        });

        var second = await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            OccurredAtUtc = occurredAtUtc,
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        });

        Assert.Equal("RCPT-20260227-0001", first.TxNo);
        Assert.Equal("RCPT-20260227-0002", second.TxNo);
    }

    private static async Task<(TestDbContextFactory Factory, SeedFixture Fixture)> BuildFixtureAsync(decimal? seedBalanceQty)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var db = new ErpDbContext(options);

        var category = new ItemCategory("ELEC", "Electronics");
        var uom = new UnitOfMeasure("EA", "Each");
        var warehouse = new Warehouse("MAIN", "Main Warehouse");
        var location = new Location(warehouse.Id, "A-01", "A-01");
        var item = new Item("A-100", "Alpha", category.Id, uom.Id, TrackingType.None);

        db.ItemCategories.Add(category);
        db.UnitOfMeasures.Add(uom);
        db.Warehouses.Add(warehouse);
        db.Locations.Add(location);
        db.Items.Add(item);

        if (seedBalanceQty.HasValue)
        {
            db.InventoryBalances.Add(new InventoryBalance(
                itemId: item.Id,
                warehouseId: warehouse.Id,
                locationId: location.Id,
                qtyOnHand: seedBalanceQty.Value));
        }

        await db.SaveChangesAsync();

        return (new TestDbContextFactory(options), new SeedFixture(item.Id, warehouse.Id, location.Id));
    }

    private sealed record SeedFixture(Guid ItemId, Guid WarehouseId, Guid LocationId);

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

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase)
        {
            Erp.Application.Authorization.PermissionCodes.InventoryStockWrite
        };

        public Guid? CurrentUserId { get; } = Guid.NewGuid();
        public string? Username { get; } = "tester";
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
