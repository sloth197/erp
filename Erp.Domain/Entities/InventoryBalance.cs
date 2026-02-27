namespace Erp.Domain.Entities;

public sealed class InventoryBalance
{
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public Guid? LocationId { get; private set; }
    public Location? Location { get; private set; }
    public decimal QtyOnHand { get; private set; }
    public decimal QtyAllocated { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private InventoryBalance()
    {
    }

    public InventoryBalance(
        Guid itemId,
        Guid warehouseId,
        Guid? locationId = null,
        decimal qtyOnHand = 0m,
        decimal qtyAllocated = 0m)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item is required.", nameof(itemId));
        }

        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        }

        if (qtyAllocated < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyAllocated), "Allocated quantity cannot be negative.");
        }

        Id = Guid.NewGuid();
        ItemId = itemId;
        WarehouseId = warehouseId;
        LocationId = NormalizeLocationId(locationId);
        QtyOnHand = qtyOnHand;
        QtyAllocated = qtyAllocated;
        RowVersion = GenerateRowVersion();
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyOnHandDelta(decimal qtyDelta)
    {
        QtyOnHand += qtyDelta;
        TouchForWrite();
    }

    public void SetAllocated(decimal qtyAllocated)
    {
        if (qtyAllocated < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(qtyAllocated), "Allocated quantity cannot be negative.");
        }

        QtyAllocated = qtyAllocated;
        TouchForWrite();
    }

    private void TouchForWrite()
    {
        UpdatedAtUtc = DateTime.UtcNow;
        RowVersion = GenerateRowVersion();
    }

    private static Guid? NormalizeLocationId(Guid? locationId)
    {
        if (!locationId.HasValue || locationId.Value == Guid.Empty)
        {
            return null;
        }

        return locationId.Value;
    }

    private static byte[] GenerateRowVersion()
    {
        return Guid.NewGuid().ToByteArray();
    }
}
