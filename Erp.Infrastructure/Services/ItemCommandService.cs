using System.Text.Json;
using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class ItemCommandService : IItemCommandService
{
    private readonly IDbContextFactory<ErpDbContext> _dbContextFactory;
    private readonly IAccessControl _accessControl;
    private readonly ICurrentUserContext _currentUserContext;

    public ItemCommandService(
        IDbContextFactory<ErpDbContext> dbContextFactory,
        IAccessControl accessControl,
        ICurrentUserContext currentUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _accessControl = accessControl;
        _currentUserContext = currentUserContext;
    }

    public async Task<ItemCommandResultDto> CreateItemAsync(
        CreateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsWrite);

        ValidateCreateCommand(command);
        var itemCode = command.ItemCode.Trim();
        var name = command.Name.Trim();
        var barcode = NormalizeBarcode(command.Barcode);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureMasterDataExistsAsync(db, command.CategoryId, command.UnitOfMeasureId, cancellationToken);
        await EnsureNoDuplicatesAsync(db, itemCode, barcode, excludedItemId: null, cancellationToken);

        var item = new Domain.Entities.Item(
            itemCode,
            name,
            command.CategoryId,
            command.UnitOfMeasureId,
            command.TrackingType,
            barcode);

        db.Items.Add(item);
        db.AuditLogs.Add(new Domain.Entities.AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "Item.Created",
            target: item.ItemCode,
            detailJson: SerializeDetail(new { itemId = item.Id, itemCode = item.ItemCode }),
            ip: null));

        await db.SaveChangesAsync(cancellationToken);

        return MapResult(item.Id, item.RowVersion, item.IsActive, item.UpdatedAtUtc);
    }

    public async Task<ItemCommandResultDto> UpdateItemAsync(
        UpdateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsWrite);

        ValidateUpdateCommand(command);
        var itemCode = command.ItemCode.Trim();
        var name = command.Name.Trim();
        var barcode = NormalizeBarcode(command.Barcode);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureMasterDataExistsAsync(db, command.CategoryId, command.UnitOfMeasureId, cancellationToken);

        var item = await db.Items.FirstOrDefaultAsync(x => x.Id == command.ItemId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        if (!item.MatchesRowVersion(command.RowVersion))
        {
            throw new ConcurrencyException("The item was modified by another session. Reload and try again.");
        }

        await EnsureNoDuplicatesAsync(db, itemCode, barcode, command.ItemId, cancellationToken);

        db.Entry(item).Property(x => x.RowVersion).OriginalValue = command.RowVersion.ToArray();
        item.Update(itemCode, name, command.CategoryId, command.UnitOfMeasureId, command.TrackingType, barcode);

        db.AuditLogs.Add(new Domain.Entities.AuditLog(
            actorUserId: _currentUserContext.CurrentUserId,
            action: "Item.Updated",
            target: item.ItemCode,
            detailJson: SerializeDetail(new { itemId = item.Id, itemCode = item.ItemCode }),
            ip: null));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Concurrency conflict detected. Reload and try again.");
        }

        return MapResult(item.Id, item.RowVersion, item.IsActive, item.UpdatedAtUtc);
    }

    public async Task<ItemCommandResultDto> ActivateItemAsync(
        ActivateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsWrite);

        if (command.ItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Item id is required.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Items.FirstOrDefaultAsync(x => x.Id == command.ItemId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        var wasActive = item.IsActive;
        item.Activate();

        if (!wasActive && item.IsActive)
        {
            db.AuditLogs.Add(new Domain.Entities.AuditLog(
                actorUserId: _currentUserContext.CurrentUserId,
                action: "Item.Activated",
                target: item.ItemCode,
                detailJson: SerializeDetail(new { itemId = item.Id, itemCode = item.ItemCode }),
                ip: null));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Concurrency conflict detected. Reload and try again.");
        }

        return MapResult(item.Id, item.RowVersion, item.IsActive, item.UpdatedAtUtc);
    }

    public async Task<ItemCommandResultDto> DeactivateItemAsync(
        DeactivateItemCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.MasterItemsWrite);

        if (command.ItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Item id is required.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Items.FirstOrDefaultAsync(x => x.Id == command.ItemId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        var wasActive = item.IsActive;
        item.Deactivate();

        if (wasActive && !item.IsActive)
        {
            db.AuditLogs.Add(new Domain.Entities.AuditLog(
                actorUserId: _currentUserContext.CurrentUserId,
                action: "Item.Deactivated",
                target: item.ItemCode,
                detailJson: SerializeDetail(new { itemId = item.Id, itemCode = item.ItemCode }),
                ip: null));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Concurrency conflict detected. Reload and try again.");
        }

        return MapResult(item.Id, item.RowVersion, item.IsActive, item.UpdatedAtUtc);
    }

    private static ItemCommandResultDto MapResult(Guid itemId, byte[] rowVersion, bool isActive, DateTime updatedAtUtc)
    {
        return new ItemCommandResultDto(itemId, rowVersion.ToArray(), isActive, updatedAtUtc);
    }

    private static void ValidateCreateCommand(CreateItemCommand command)
    {
        if (command is null)
        {
            throw new InvalidOperationException("Request payload is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ItemCode))
        {
            throw new InvalidOperationException("Item code is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new InvalidOperationException("Item name is required.");
        }

        if (command.CategoryId == Guid.Empty)
        {
            throw new InvalidOperationException("Category is required.");
        }

        if (command.UnitOfMeasureId == Guid.Empty)
        {
            throw new InvalidOperationException("Unit of measure is required.");
        }
    }

    private static void ValidateUpdateCommand(UpdateItemCommand command)
    {
        if (command is null)
        {
            throw new InvalidOperationException("Request payload is required.");
        }

        if (command.ItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Item id is required.");
        }

        if (command.RowVersion is null || command.RowVersion.Length == 0)
        {
            throw new InvalidOperationException("RowVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ItemCode))
        {
            throw new InvalidOperationException("Item code is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new InvalidOperationException("Item name is required.");
        }

        if (command.CategoryId == Guid.Empty)
        {
            throw new InvalidOperationException("Category is required.");
        }

        if (command.UnitOfMeasureId == Guid.Empty)
        {
            throw new InvalidOperationException("Unit of measure is required.");
        }
    }

    private static async Task EnsureMasterDataExistsAsync(
        ErpDbContext db,
        Guid categoryId,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken)
    {
        var categoryExists = await db.ItemCategories.AnyAsync(x => x.Id == categoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new InvalidOperationException("Category not found.");
        }

        var uomExists = await db.UnitOfMeasures.AnyAsync(x => x.Id == unitOfMeasureId, cancellationToken);
        if (!uomExists)
        {
            throw new InvalidOperationException("Unit of measure not found.");
        }
    }

    private static async Task EnsureNoDuplicatesAsync(
        ErpDbContext db,
        string itemCode,
        string? barcode,
        Guid? excludedItemId,
        CancellationToken cancellationToken)
    {
        var codeQuery = db.Items.Where(x => x.ItemCode == itemCode);
        if (excludedItemId.HasValue)
        {
            var excludedId = excludedItemId.Value;
            codeQuery = codeQuery.Where(x => x.Id != excludedId);
        }

        if (await codeQuery.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Duplicate item code.");
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }

        var barcodeQuery = db.Items.Where(x => x.Barcode == barcode);
        if (excludedItemId.HasValue)
        {
            var excludedId = excludedItemId.Value;
            barcodeQuery = barcodeQuery.Where(x => x.Id != excludedId);
        }

        if (await barcodeQuery.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Duplicate barcode.");
        }
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        return string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
    }

    private static string SerializeDetail(object detail)
    {
        return JsonSerializer.Serialize(detail);
    }
}
