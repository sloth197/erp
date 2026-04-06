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

        Assert.Equal(PermissionCodes.InventoryStockReceipt, accessControl.LastDemandedPermissionCode);
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
        var accessControl = new RecordingAccessControl();
        var service = new InventoryCommandService(factory, accessControl, new FakeCurrentUserContext());

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
        Assert.Equal(PermissionCodes.InventoryStockIssue, accessControl.LastDemandedPermissionCode);

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

    [Fact]
    public async Task ReceiveStockAsync_Throws_WhenRequestedTxNoAlreadyExists()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());
        const string duplicatedTxNo = "RCPT-20260227-9999";

        await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            TxNo = duplicatedTxNo,
            OccurredAtUtc = new DateTime(2026, 2, 27, 14, 0, 0, DateTimeKind.Utc),
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveStockAsync(new ReceiveStockCommand
        {
            TxNo = duplicatedTxNo,
            OccurredAtUtc = new DateTime(2026, 2, 27, 14, 1, 0, DateTimeKind.Utc),
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));

        Assert.Equal("Duplicate TxNo.", ex.Message);
    }

    [Fact]
    public async Task ReceiveStockAsync_RequiresLot_ForLotTrackedItem()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Lot);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 2m }]
        }));

        Assert.Contains("LotNo is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveStockAsync_RequiresSerial_ForSerialTrackedItem()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Serial);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));

        Assert.Contains("SerialNo is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveStockAsync_RequiresExpiry_ForExpiryTrackedItem()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Expiry);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m, LotNo = "LOT-1" }]
        }));

        Assert.Contains("ExpiryDate is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueStockAsync_RequiresLot_ForLotTrackedItem()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Lot);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 3m, LotNo = "LOT-A" }]
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssueStockAsync(new IssueStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));

        Assert.Contains("LotNo is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueStockAsync_Fails_WhenLotOnHandIsInsufficient()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Lot);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 2m, LotNo = "LOT-Z" }]
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssueStockAsync(new IssueStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 3m, LotNo = "LOT-Z" }]
        }));

        Assert.Contains("Insufficient stock for lot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueStockAsync_RequiresSerial_ForSerialTrackedItem()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null, trackingType: TrackingType.Serial);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        await service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m, SerialNo = "SER-1001" }]
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IssueStockAsync(new IssueStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));

        Assert.Contains("SerialNo is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdjustStockByCountAsync_AlignsBalanceToCountedQty_AndWritesLedger()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 15m);
        var service = new InventoryCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var occurredAtUtc = new DateTime(2026, 2, 28, 9, 30, 0, DateTimeKind.Utc);
        var result = await service.AdjustStockByCountAsync(new AdjustStockByCountCommand
        {
            OccurredAtUtc = occurredAtUtc,
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines =
            [
                new AdjustStockByCountLineCommand { ItemId = fixture.ItemId, CountedQty = 11m, Note = "Cycle count" }
            ]
        });

        Assert.StartsWith("ADJ-20260228-", result.TxNo, StringComparison.Ordinal);
        Assert.Equal(1, result.LineCount);

        await using var db = await factory.CreateDbContextAsync();
        var ledger = await db.StockLedgerEntries.SingleAsync(x => x.TxNo == result.TxNo);
        var balance = await db.InventoryBalances.SingleAsync(x =>
            x.ItemId == fixture.ItemId &&
            x.WarehouseId == fixture.WarehouseId &&
            x.LocationId == fixture.LocationId);
        var audit = await db.AuditLogs.SingleAsync(x => x.Action == "Stock.AdjustByCount" && x.Target == result.TxNo);

        Assert.Equal(InventoryTxType.AdjustOut, ledger.TxType);
        Assert.Equal(-4m, ledger.Qty);
        Assert.Equal(11m, balance.QtyOnHand);
        Assert.Contains("Cycle count", audit.DetailJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdjustStockByCountAsync_DemandsAdjustPermission()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 0m);
        var accessControl = new RecordingAccessControl();
        var currentUser = new FakeCurrentUserContext();
        var service = new InventoryCommandService(factory, accessControl, currentUser);

        var result = await service.AdjustStockByCountAsync(new AdjustStockByCountCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines =
            [
                new AdjustStockByCountLineCommand { ItemId = fixture.ItemId, CountedQty = 3m }
            ]
        });

        await using var db = await factory.CreateDbContextAsync();
        var ledger = await db.StockLedgerEntries.SingleAsync(x => x.TxNo == result.TxNo);

        Assert.Equal(PermissionCodes.InventoryStockAdjust, accessControl.LastDemandedPermissionCode);
        Assert.Equal(InventoryTxType.AdjustIn, ledger.TxType);
        Assert.Equal(3m, ledger.Qty);
    }

    [Fact]
    public async Task ReceiveStockAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: null);
        var service = new InventoryCommandService(factory, new DenyAccessControl(), new FakeCurrentUserContext());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReceiveStockAsync(new ReceiveStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new ReceiveStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));
    }

    [Fact]
    public async Task IssueStockAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 10m);
        var service = new InventoryCommandService(factory, new DenyAccessControl(), new FakeCurrentUserContext());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.IssueStockAsync(new IssueStockCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new IssueStockLineCommand { ItemId = fixture.ItemId, Qty = 1m }]
        }));
    }

    [Fact]
    public async Task AdjustStockByCountAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var (factory, fixture) = await BuildFixtureAsync(seedBalanceQty: 0m);
        var service = new InventoryCommandService(factory, new DenyAccessControl(), new FakeCurrentUserContext());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.AdjustStockByCountAsync(new AdjustStockByCountCommand
        {
            WarehouseId = fixture.WarehouseId,
            LocationId = fixture.LocationId,
            Lines = [new AdjustStockByCountLineCommand { ItemId = fixture.ItemId, CountedQty = 1m }]
        }));
    }

    private static async Task<(TestDbContextFactory Factory, SeedFixture Fixture)> BuildFixtureAsync(
        decimal? seedBalanceQty,
        TrackingType trackingType = TrackingType.None)
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
        var item = new Item("A-100", "Alpha", category.Id, uom.Id, trackingType);

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
            if (permissions.Length == 0)
            {
                permissions =
                [
                    Erp.Application.Authorization.PermissionCodes.InventoryStockRead,
                    Erp.Application.Authorization.PermissionCodes.InventoryStockReceipt,
                    Erp.Application.Authorization.PermissionCodes.InventoryStockIssue,
                    Erp.Application.Authorization.PermissionCodes.InventoryStockAdjust
                ];
            }

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
