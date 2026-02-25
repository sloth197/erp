using System.Reflection;
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

public sealed class SearchItemsQueryHandlerTests
{
    [Fact]
    public async Task SearchItemsAsync_AppliesFilterSortPagingAndReturnsTotalCount()
    {
        var (dbContextFactory, fixture) = await BuildFixtureAsync();
        var accessControl = new RecordingAccessControl();
        var handler = new SearchItemsQueryHandler(dbContextFactory, accessControl);

        var query = new SearchItemsQuery
        {
            CategoryId = fixture.ElectronicsCategoryId,
            IsActive = true,
            Page = 1,
            PageSize = 1,
            SortBy = "itemCode",
            SortDirection = "desc"
        };

        var result = await handler.SearchItemsAsync(query);

        Assert.Equal(PermissionCodes.MasterItemsRead, accessControl.LastDemandedPermissionCode);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("B-100", result.Items[0].ItemCode);
    }

    [Fact]
    public async Task SearchItemsAsync_FiltersByKeywordTrackingTypeAndIsActive()
    {
        var (dbContextFactory, _) = await BuildFixtureAsync();
        var accessControl = new RecordingAccessControl();
        var handler = new SearchItemsQueryHandler(dbContextFactory, accessControl);

        var query = new SearchItemsQuery
        {
            Keyword = "Charlie",
            TrackingType = TrackingType.Serial,
            IsActive = false,
            Page = 1,
            PageSize = 10,
            SortBy = "name",
            SortDirection = "asc"
        };

        var result = await handler.SearchItemsAsync(query);

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("C-100", item.ItemCode);
        Assert.Equal(TrackingType.Serial, item.TrackingType);
        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task SearchItemsAsync_ThrowsForbidden_WhenPermissionDenied()
    {
        var (dbContextFactory, _) = await BuildFixtureAsync();
        var accessControl = new RecordingAccessControl { ThrowForbidden = true };
        var handler = new SearchItemsQueryHandler(dbContextFactory, accessControl);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.SearchItemsAsync(new SearchItemsQuery()));
    }

    [Fact]
    public async Task SearchItemsAsync_HandlesThousandRowsWithPaging()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var db = new ErpDbContext(options))
        {
            var category = new ItemCategory("BULK", "Bulk");
            var uom = new UnitOfMeasure("EA", "Each");
            db.ItemCategories.Add(category);
            db.UnitOfMeasures.Add(uom);

            for (var i = 1; i <= 1000; i++)
            {
                db.Items.Add(new Item(
                    $"ITM-{i:0000}",
                    $"Bulk Item {i:0000}",
                    category.Id,
                    uom.Id,
                    TrackingType.None,
                    $"BC-{i:0000}"));
            }

            await db.SaveChangesAsync();
        }

        var accessControl = new RecordingAccessControl();
        var handler = new SearchItemsQueryHandler(new TestDbContextFactory(options), accessControl);
        var stopwatch = Stopwatch.StartNew();

        var result = await handler.SearchItemsAsync(new SearchItemsQuery
        {
            Page = 5,
            PageSize = 100,
            SortBy = "itemCode",
            SortDirection = "asc"
        });

        stopwatch.Stop();

        Assert.Equal(1000, result.TotalCount);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal("ITM-0401", result.Items[0].ItemCode);
        Assert.Equal(PermissionCodes.MasterItemsRead, accessControl.LastDemandedPermissionCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000);
    }

    private static async Task<(TestDbContextFactory DbContextFactory, SeedFixture Fixture)> BuildFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ErpDbContext(options);
        var fixture = Seed(db);
        await db.SaveChangesAsync();

        return (new TestDbContextFactory(options), fixture);
    }

    private static SeedFixture Seed(ErpDbContext db)
    {
        var electronics = new ItemCategory("ELEC", "Electronics");
        var food = new ItemCategory("FOOD", "Food");

        var each = new UnitOfMeasure("EA", "Each");
        var box = new UnitOfMeasure("BOX", "Box");

        db.ItemCategories.AddRange(electronics, food);
        db.UnitOfMeasures.AddRange(each, box);

        var alpha = new Item("A-100", "Alpha Item", electronics.Id, each.Id, TrackingType.None, "88001");
        var bravo = new Item("B-100", "Bravo Item", electronics.Id, each.Id, TrackingType.Batch, "88002");
        var charlie = new Item("C-100", "Charlie Item", food.Id, box.Id, TrackingType.Serial, "99001");

        SetPrivateProperty(charlie, nameof(Item.IsActive), false);

        db.Items.AddRange(alpha, bravo, charlie);

        return new SeedFixture(electronics.Id);
    }

    private static void SetPrivateProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var propertyInfo = typeof(TTarget).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (propertyInfo is null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found.");
        }

        propertyInfo.SetValue(target, value);
    }

    private sealed record SeedFixture(Guid ElectronicsCategoryId);

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
        public bool ThrowForbidden { get; init; }
        public string? LastDemandedPermissionCode { get; private set; }

        public void DemandAuthenticated()
        {
        }

        public void DemandPermission(string permissionCode)
        {
            LastDemandedPermissionCode = permissionCode;

            if (ThrowForbidden)
            {
                throw new ForbiddenException("Permission denied.");
            }
        }
    }
}
