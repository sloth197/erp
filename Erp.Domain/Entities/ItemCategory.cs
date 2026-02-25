namespace Erp.Domain.Entities;

public sealed class ItemCategory
{
    private readonly List<Item> _items = new();

    public Guid Id { get; private set; }
    public string CategoryCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Item> Items => _items;

    private ItemCategory()
    {
    }

    public ItemCategory(string categoryCode, string name)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            throw new ArgumentException("Category code is required.", nameof(categoryCode));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        CategoryCode = categoryCode.Trim();
        Name = name.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
