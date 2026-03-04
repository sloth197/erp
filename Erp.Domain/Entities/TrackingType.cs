namespace Erp.Domain.Entities;

public enum TrackingType
{
    None = 0,
    Lot = 1,
    Batch = Lot,
    Serial = 2,
    Expiry = 3
}
