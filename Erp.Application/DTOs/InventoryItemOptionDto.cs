using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

public sealed record InventoryItemOptionDto(
    Guid Id,
    string ItemCode,
    string Name,
    TrackingType TrackingType,
    bool IsActive);
