namespace Erp.Domain.Entities;

public sealed class StockLot
{
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; } = null!;
    public string LotNo { get; private set; } = string.Empty;
    public DateTime? ExpiryDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private StockLot()
    {
    }

    public StockLot(Guid itemId, string lotNo, DateTime? expiryDate = null)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item is required.", nameof(itemId));
        }

        if (string.IsNullOrWhiteSpace(lotNo))
        {
            throw new ArgumentException("Lot number is required.", nameof(lotNo));
        }

        Id = Guid.NewGuid();
        ItemId = itemId;
        LotNo = lotNo.Trim();
        ExpiryDate = NormalizeDate(expiryDate);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void EnsureExpiryDate(DateTime expiryDate)
    {
        var normalized = NormalizeDate(expiryDate)!.Value;

        if (!ExpiryDate.HasValue)
        {
            ExpiryDate = normalized;
            return;
        }

        if (ExpiryDate.Value != normalized)
        {
            throw new InvalidOperationException("Lot expiry date does not match existing lot expiry.");
        }
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
