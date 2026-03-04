namespace Erp.Domain.Entities;

public sealed class StockLedgerEntry
{
    public Guid Id { get; private set; }
    public string TxNo { get; private set; } = string.Empty;
    public InventoryTxType TxType { get; private set; }
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public Guid? LocationId { get; private set; }
    public Location? Location { get; private set; }
    public Guid? LotId { get; private set; }
    public StockLot? Lot { get; private set; }
    public string? SerialNo { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public decimal Qty { get; private set; }
    public decimal? UnitCost { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public StockReferenceType? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Note { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public User? ActorUser { get; private set; }

    private StockLedgerEntry()
    {
    }

    public StockLedgerEntry(
        string txNo,
        InventoryTxType txType,
        Guid itemId,
        Guid warehouseId,
        decimal qty,
        DateTime occurredAtUtc,
        Guid? locationId = null,
        Guid? lotId = null,
        string? serialNo = null,
        DateTime? expiryDate = null,
        decimal? unitCost = null,
        StockReferenceType? referenceType = null,
        Guid? referenceId = null,
        string? note = null,
        Guid? actorUserId = null)
    {
        if (string.IsNullOrWhiteSpace(txNo))
        {
            throw new ArgumentException("Transaction number is required.", nameof(txNo));
        }

        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item is required.", nameof(itemId));
        }

        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        }

        if (qty == 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be non-zero.");
        }

        if (unitCost.HasValue && unitCost.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative.");
        }

        Id = Guid.NewGuid();
        TxNo = txNo.Trim();
        TxType = txType;
        ItemId = itemId;
        WarehouseId = warehouseId;
        LocationId = NormalizeLocationId(locationId);
        LotId = NormalizeOptionalGuid(lotId);
        SerialNo = string.IsNullOrWhiteSpace(serialNo) ? null : serialNo.Trim();
        ExpiryDate = NormalizeDate(expiryDate);
        Qty = qty;
        UnitCost = unitCost;
        OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : occurredAtUtc.ToUniversalTime();
        CreatedAtUtc = DateTime.UtcNow;
        ReferenceType = referenceType;
        ReferenceId = NormalizeOptionalGuid(referenceId);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ActorUserId = NormalizeOptionalGuid(actorUserId);
    }

    private static Guid? NormalizeLocationId(Guid? locationId)
    {
        if (!locationId.HasValue || locationId.Value == Guid.Empty)
        {
            return null;
        }

        return locationId.Value;
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
    {
        if (!value.HasValue || value.Value == Guid.Empty)
        {
            return null;
        }

        return value.Value;
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalized = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value.Value.ToUniversalTime()
        };

        return normalized.Date;
    }
}
