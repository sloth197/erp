namespace Erp.Domain.Entities;

public sealed class UnitOfMeasure
{
    private readonly List<Item> _items = new();

    public Guid Id { get; private set; }
    public string UomCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Item> Items => _items;

    private UnitOfMeasure()
    {
    }

    public UnitOfMeasure(string uomCode, string name)
    {
        if (string.IsNullOrWhiteSpace(uomCode))
        {
            throw new ArgumentException("UOM code is required.", nameof(uomCode));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        UomCode = uomCode.Trim();
        Name = name.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
