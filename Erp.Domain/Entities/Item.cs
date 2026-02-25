namespace Erp.Domain.Entities;

public sealed class Item
{
    public Guid Id { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public TrackingType TrackingType { get; private set; }
    public Guid CategoryId { get; private set; }
    public ItemCategory Category { get; private set; } = null!;
    public Guid UnitOfMeasureId { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Item()
    {
    }

    public Item(
        string itemCode,
        string name,
        Guid categoryId,
        Guid unitOfMeasureId,
        TrackingType trackingType,
        string? barcode = null)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            throw new ArgumentException("Item code is required.", nameof(itemCode));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(categoryId));
        }

        if (unitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("Unit of measure is required.", nameof(unitOfMeasureId));
        }

        Id = Guid.NewGuid();
        ItemCode = itemCode.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        Name = name.Trim();
        IsActive = true;
        TrackingType = trackingType;
        CategoryId = categoryId;
        UnitOfMeasureId = unitOfMeasureId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
