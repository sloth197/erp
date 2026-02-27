namespace Erp.Domain.Entities;

public sealed class Warehouse
{
    private readonly List<Location> _locations = new();
    private readonly List<InventoryBalance> _inventoryBalances = new();
    private readonly List<StockLedgerEntry> _stockLedgerEntries = new();

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Location> Locations => _locations;
    public IReadOnlyCollection<InventoryBalance> InventoryBalances => _inventoryBalances;
    public IReadOnlyCollection<StockLedgerEntry> StockLedgerEntries => _stockLedgerEntries;

    private Warehouse()
    {
    }

    public Warehouse(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Warehouse code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Warehouse name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Code = code.Trim();
        Name = name.Trim();
        IsActive = true;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Warehouse name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
