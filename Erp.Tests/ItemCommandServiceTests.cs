using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Domain.Entities;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Erp.Tests;

public sealed class ItemCommandServiceTests
{
    [Fact]
    public async Task CreateItemAsync_Throws_WhenItemCodeIsDuplicated()
    {
        var (factory, fixture) = await BuildFixtureAsync();
        var accessControl = new RecordingAccessControl();
        var service = new ItemCommandService(factory, accessControl, new FakeCurrentUserContext());

        var command = new CreateItemCommand
        {
            ItemCode = fixture.ExistingItemCode,
            Barcode = "NEW-BC-1",
            Name = "Duplicated Code",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.None
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemAsync(command));

        Assert.Contains("item code", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PermissionCodes.MasterItemsWrite, accessControl.LastDemandedPermissionCode);
    }

    [Fact]
    public async Task CreateItemAsync_Throws_WhenBarcodeIsDuplicated()
    {
        var (factory, fixture) = await BuildFixtureAsync();
        var service = new ItemCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var command = new CreateItemCommand
        {
            ItemCode = "ITEM-NEW",
            Barcode = fixture.ExistingBarcode,
            Name = "Duplicated Barcode",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.Batch
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemAsync(command));

        Assert.Contains("barcode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateItemAsync_ThrowsConcurrencyException_WhenSecondSessionUsesStaleRowVersion()
    {
        var (factory, fixture) = await BuildFixtureAsync();
        var service = new ItemCommandService(factory, new RecordingAccessControl(), new FakeCurrentUserContext());

        var firstCommand = new UpdateItemCommand
        {
            ItemId = fixture.ItemId,
            RowVersion = fixture.InitialRowVersion.ToArray(),
            ItemCode = fixture.ExistingItemCode,
            Barcode = fixture.ExistingBarcode,
            Name = "Updated By Session1",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.Serial
        };

        _ = await service.UpdateItemAsync(firstCommand);

        var secondCommand = new UpdateItemCommand
        {
            ItemId = fixture.ItemId,
            RowVersion = fixture.InitialRowVersion.ToArray(),
            ItemCode = fixture.ExistingItemCode,
            Barcode = fixture.ExistingBarcode,
            Name = "Updated By Session2",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.Batch
        };

        await Assert.ThrowsAsync<ConcurrencyException>(() => service.UpdateItemAsync(secondCommand));
    }

    [Fact]
    public async Task Commands_WriteAuditLogs_ForCreateUpdateAndDeactivate()
    {
        var actorId = Guid.NewGuid();
        var (factory, fixture) = await BuildFixtureAsync();
        var service = new ItemCommandService(
            factory,
            new RecordingAccessControl(),
            new FakeCurrentUserContext(actorId, "auditor"));

        var created = await service.CreateItemAsync(new CreateItemCommand
        {
            ItemCode = "AUD-NEW",
            Barcode = "AUD-BC",
            Name = "Audit Item",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.None
        });

        var updated = await service.UpdateItemAsync(new UpdateItemCommand
        {
            ItemId = created.ItemId,
            RowVersion = created.RowVersion,
            ItemCode = "AUD-NEW",
            Barcode = "AUD-BC",
            Name = "Audit Item Updated",
            CategoryId = fixture.CategoryId,
            UnitOfMeasureId = fixture.UnitOfMeasureId,
            TrackingType = TrackingType.Batch
        });

        _ = await service.DeactivateItemAsync(new DeactivateItemCommand { ItemId = updated.ItemId });

        await using var db = await factory.CreateDbContextAsync();
        var actions = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Target == "AUD-NEW")
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.Action)
            .ToListAsync();

        Assert.Contains("Item.Created", actions);
        Assert.Contains("Item.Updated", actions);
        Assert.Contains("Item.Deactivated", actions);
    }

    private static async Task<(TestDbContextFactory Factory, SeedFixture Fixture)> BuildFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ErpDbContext(options);

        var category = new ItemCategory("CAT", "Category");
        var uom = new UnitOfMeasure("EA", "Each");
        var item = new Item("ITEM-001", "Seed Item", category.Id, uom.Id, TrackingType.None, "BC-001");

        db.ItemCategories.Add(category);
        db.UnitOfMeasures.Add(uom);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var fixture = new SeedFixture(
            item.Id,
            item.ItemCode,
            item.Barcode!,
            item.RowVersion.ToArray(),
            category.Id,
            uom.Id);

        return (new TestDbContextFactory(options), fixture);
    }

    private sealed record SeedFixture(
        Guid ItemId,
        string ExistingItemCode,
        string ExistingBarcode,
        byte[] InitialRowVersion,
        Guid CategoryId,
        Guid UnitOfMeasureId);

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
        private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

        public FakeCurrentUserContext()
            : this(Guid.NewGuid(), "tester")
        {
        }

        public FakeCurrentUserContext(Guid userId, string username)
        {
            CurrentUserId = userId;
            Username = username;
            _permissions.Add(Erp.Application.Authorization.PermissionCodes.MasterItemsWrite);
        }

        public Guid? CurrentUserId { get; }
        public string? Username { get; }
        public bool IsAuthenticated => CurrentUserId.HasValue;
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
