namespace Erp.Domain.Entities;

public sealed class Location
{
    private readonly List<InventoryBalance> _inventoryBalances = new();
    private readonly List<StockLedgerEntry> _stockLedgerEntries = new();

    public Guid Id { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<InventoryBalance> InventoryBalances => _inventoryBalances;
    public IReadOnlyCollection<StockLedgerEntry> StockLedgerEntries => _stockLedgerEntries;

    private Location()
    {
    }

    public Location(Guid warehouseId, string code, string name)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Location code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Location name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        WarehouseId = warehouseId;
        Code = code.Trim();
        Name = name.Trim();
        IsActive = true;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Location name is required.", nameof(name));
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
