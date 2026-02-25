namespace Erp.Domain.Entities;

public sealed class ItemUomConversion
{
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; } = null!;

    public Guid FromUnitOfMeasureId { get; private set; }
    public UnitOfMeasure FromUnitOfMeasure { get; private set; } = null!;

    public Guid ToUnitOfMeasureId { get; private set; }
    public UnitOfMeasure ToUnitOfMeasure { get; private set; } = null!;

    public decimal Factor { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ItemUomConversion()
    {
    }

    public ItemUomConversion(Guid itemId, Guid fromUnitOfMeasureId, Guid toUnitOfMeasureId, decimal factor)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item is required.", nameof(itemId));
        }

        if (fromUnitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("From UOM is required.", nameof(fromUnitOfMeasureId));
        }

        if (toUnitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("To UOM is required.", nameof(toUnitOfMeasureId));
        }

        if (factor <= 0)
        {
            throw new ArgumentException("Factor must be greater than zero.", nameof(factor));
        }

        ItemId = itemId;
        FromUnitOfMeasureId = fromUnitOfMeasureId;
        ToUnitOfMeasureId = toUnitOfMeasureId;
        Factor = factor;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
