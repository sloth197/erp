using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

public sealed record ItemListDto(
    Guid Id,
    string ItemCode,
    string? Barcode,
    string Name,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    string UnitOfMeasureName,
    TrackingType TrackingType,
    bool IsActive,
    byte[] RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
