namespace Erp.Application.DTOs;

public sealed record SalesShipmentListDto(
    Guid Id,
    string ShipmentNumber,
    string SalesOrderNumber,
    string CustomerName,
    DateTime ShipmentDate,
    string ShippingType,
    string Warehouse,
    string? Carrier,
    string? TrackingNumber,
    string Status);
