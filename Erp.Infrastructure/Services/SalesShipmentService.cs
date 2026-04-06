using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;

namespace Erp.Infrastructure.Services;

public sealed class SalesShipmentService : ISalesShipmentQueryService, ISalesShipmentCommandService
{
    private readonly object _syncRoot = new();
    private readonly IAccessControl _accessControl;
    private readonly List<SalesShipmentState> _shipments;

    public SalesShipmentService(IAccessControl accessControl)
    {
        _accessControl = accessControl;
        _shipments = BuildSeedShipments();
    }

    public Task<SalesShipmentSearchResultDto> SearchSalesShipmentsAsync(
        SearchSalesShipmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        query ??= new SearchSalesShipmentsQuery();

        lock (_syncRoot)
        {
            var filtered = ApplyFilters(query).ToList();
            var items = filtered.Select(MapToDto).ToList();
            var today = DateTime.Today;

            var result = new SalesShipmentSearchResultDto(
                items,
                TodayShipmentCount: filtered.Count(x => x.ShipmentDate.Date == today),
                PickingWaitingCount: filtered.Count(x => x.Status == "피킹대기"),
                PackedCount: filtered.Count(x => x.Status == "포장완료"),
                MissingTrackingCount: filtered.Count(x => string.IsNullOrWhiteSpace(x.TrackingNumber)));

            return Task.FromResult(result);
        }
    }

    public Task<DocumentCommandResultDto> CreateShipmentAsync(
        CreateSalesShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null)
        {
            throw new InvalidOperationException("요청 데이터가 없습니다.");
        }

        lock (_syncRoot)
        {
            var shipment = new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = GenerateShipmentNumber(),
                SalesOrderNumber = $"SO-{DateTime.Today:yyyyMM}-{Random.Shared.Next(80, 120):000}",
                CustomerName = "신규 고객사",
                ShipmentDate = DateTime.Today,
                ShippingType = NormalizeShippingType(command.ShippingType),
                Warehouse = NormalizeWarehouse(command.Warehouse),
                Carrier = null,
                TrackingNumber = null,
                Status = "피킹대기"
            };

            _shipments.Add(shipment);

            return Task.FromResult(new DocumentCommandResultDto(
                shipment.Id,
                shipment.ShipmentNumber,
                shipment.Status,
                $"{shipment.ShipmentNumber} 출고 건을 생성했습니다."));
        }
    }

    public Task<DocumentCommandResultDto> ConfirmShipmentAsync(
        ConfirmSalesShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null || command.ShipmentId == Guid.Empty)
        {
            throw new InvalidOperationException("출고 식별자가 올바르지 않습니다.");
        }

        if (!command.IsPickingCompleted || !command.IsPackingCompleted || !command.IsTrackingCompleted)
        {
            throw new InvalidOperationException("피킹/포장/송장 등록 확인 후 출고 확정이 가능합니다.");
        }

        lock (_syncRoot)
        {
            var shipment = _shipments.FirstOrDefault(x => x.Id == command.ShipmentId);
            if (shipment is null)
            {
                throw new InvalidOperationException("대상 출고를 찾을 수 없습니다.");
            }

            if (shipment.Status == "출고완료")
            {
                return Task.FromResult(new DocumentCommandResultDto(
                    shipment.Id,
                    shipment.ShipmentNumber,
                    shipment.Status,
                    $"{shipment.ShipmentNumber}는 이미 출고 완료 상태입니다."));
            }

            if (string.IsNullOrWhiteSpace(shipment.Carrier))
            {
                shipment.Carrier = shipment.ShippingType == "직배송" ? "자체배송" : "한빛택배";
            }

            if (string.IsNullOrWhiteSpace(shipment.TrackingNumber))
            {
                shipment.TrackingNumber = BuildTrackingNumber();
            }

            shipment.Status = "출고완료";

            return Task.FromResult(new DocumentCommandResultDto(
                shipment.Id,
                shipment.ShipmentNumber,
                shipment.Status,
                $"{shipment.ShipmentNumber} 출고를 확정했습니다."));
        }
    }

    public Task<OperationResultDto> RegisterBulkTrackingAsync(
        RegisterBulkTrackingCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        var shipmentIds = command?.ShipmentIds?
            .Where(x => x != Guid.Empty)
            .ToHashSet() ?? new HashSet<Guid>();

        lock (_syncRoot)
        {
            var targets = shipmentIds.Count == 0
                ? _shipments
                : _shipments.Where(x => shipmentIds.Contains(x.Id)).ToList();

            var updated = 0;
            foreach (var shipment in targets)
            {
                if (!string.IsNullOrWhiteSpace(shipment.TrackingNumber))
                {
                    continue;
                }

                shipment.Carrier = string.IsNullOrWhiteSpace(shipment.Carrier)
                    ? shipment.ShippingType == "직배송" ? "자체배송" : "한빛택배"
                    : shipment.Carrier;
                shipment.TrackingNumber = BuildTrackingNumber();
                updated++;
            }

            return Task.FromResult(updated == 0
                ? new OperationResultDto("송장 등록이 필요한 출고 건이 없습니다.")
                : new OperationResultDto($"{updated}건 송장을 일괄 등록했습니다.", updated));
        }
    }

    public Task<OperationResultDto> CloseShipmentDayAsync(
        CloseShipmentDayCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        var date = (command?.Date ?? DateTime.Today).Date;

        lock (_syncRoot)
        {
            var completed = _shipments.Count(x =>
                x.Status == "출고완료" &&
                x.ShipmentDate.Date == date);

            return Task.FromResult(new OperationResultDto(
                $"출고 마감을 처리했습니다. ({date:yyyy-MM-dd} 출고완료 {completed}건)",
                completed));
        }
    }

    private IEnumerable<SalesShipmentState> ApplyFilters(SearchSalesShipmentsQuery query)
    {
        var warehouse = NormalizeFilterValue(query.Warehouse);
        var shippingType = NormalizeFilterValue(query.ShippingType);
        var status = NormalizeFilterValue(query.Status);
        var shipmentDate = query.ShipmentDate?.Date;

        var filtered = _shipments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(warehouse))
        {
            filtered = filtered.Where(x => string.Equals(x.Warehouse, warehouse, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(shippingType))
        {
            filtered = filtered.Where(x => string.Equals(x.ShippingType, shippingType, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.Ordinal));
        }

        if (shipmentDate.HasValue)
        {
            filtered = filtered.Where(x => x.ShipmentDate.Date == shipmentDate.Value);
        }

        return filtered
            .OrderByDescending(x => x.ShipmentDate)
            .ThenByDescending(x => x.ShipmentNumber);
    }

    private string GenerateShipmentNumber()
    {
        var prefix = $"SH-{DateTime.Today:yyyyMM}-";
        var next = _shipments
            .Where(x => x.ShipmentNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ShipmentNumber[prefix.Length..])
            .Select(x => int.TryParse(x, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:000}";
    }

    private static string BuildTrackingNumber()
    {
        return $"TRK-{DateTime.Today:MMdd}-{Random.Shared.Next(100000, 999999)}";
    }

    private static string NormalizeWarehouse(string? warehouse)
    {
        if (string.IsNullOrWhiteSpace(warehouse))
        {
            return "본사 창고";
        }

        var trimmed = warehouse.Trim();
        return trimmed switch
        {
            "본사 창고" or "동부 센터" or "서부 센터" => trimmed,
            _ => "본사 창고"
        };
    }

    private static string NormalizeShippingType(string? shippingType)
    {
        if (string.IsNullOrWhiteSpace(shippingType))
        {
            return "택배";
        }

        var trimmed = shippingType.Trim();
        return trimmed switch
        {
            "택배" or "직배송" or "퀵배송" => trimmed,
            _ => "택배"
        };
    }

    private static string? NormalizeFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.Equals(trimmed, "전체", StringComparison.Ordinal) ? null : trimmed;
    }

    private static SalesShipmentListDto MapToDto(SalesShipmentState state)
    {
        return new SalesShipmentListDto(
            state.Id,
            state.ShipmentNumber,
            state.SalesOrderNumber,
            state.CustomerName,
            state.ShipmentDate,
            state.ShippingType,
            state.Warehouse,
            state.Carrier,
            state.TrackingNumber,
            state.Status);
    }

    private static List<SalesShipmentState> BuildSeedShipments()
    {
        var today = DateTime.Today;
        return
        [
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202604-021",
                SalesOrderNumber = "SO-202604-019",
                CustomerName = "대한유통",
                ShipmentDate = today,
                ShippingType = "택배",
                Warehouse = "본사 창고",
                Carrier = "한빛택배",
                TrackingNumber = "TRK-0406-483901",
                Status = "출고완료"
            },
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202604-020",
                SalesOrderNumber = "SO-202604-018",
                CustomerName = "세진메디칼",
                ShipmentDate = today,
                ShippingType = "직배송",
                Warehouse = "동부 센터",
                Carrier = "자체배송",
                TrackingNumber = null,
                Status = "포장완료"
            },
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202604-019",
                SalesOrderNumber = "SO-202604-017",
                CustomerName = "아람전자",
                ShipmentDate = today.AddDays(-1),
                ShippingType = "택배",
                Warehouse = "본사 창고",
                Carrier = "한빛택배",
                TrackingNumber = null,
                Status = "피킹대기"
            },
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202604-018",
                SalesOrderNumber = "SO-202604-015",
                CustomerName = "비전오토",
                ShipmentDate = today.AddDays(-1),
                ShippingType = "퀵배송",
                Warehouse = "서부 센터",
                Carrier = "스피드퀵",
                TrackingNumber = "TRK-0405-103820",
                Status = "출고완료"
            },
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202604-017",
                SalesOrderNumber = "SO-202604-014",
                CustomerName = "클린라이프",
                ShipmentDate = today.AddDays(-2),
                ShippingType = "택배",
                Warehouse = "동부 센터",
                Carrier = null,
                TrackingNumber = null,
                Status = "포장완료"
            },
            new SalesShipmentState
            {
                Id = Guid.NewGuid(),
                ShipmentNumber = "SH-202603-063",
                SalesOrderNumber = "SO-202603-062",
                CustomerName = "하이덱스",
                ShipmentDate = today.AddDays(-8),
                ShippingType = "직배송",
                Warehouse = "본사 창고",
                Carrier = "자체배송",
                TrackingNumber = "TRK-0329-880122",
                Status = "출고완료"
            }
        ];
    }

    private sealed class SalesShipmentState
    {
        public Guid Id { get; init; }
        public string ShipmentNumber { get; init; } = string.Empty;
        public string SalesOrderNumber { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public DateTime ShipmentDate { get; init; }
        public string ShippingType { get; init; } = string.Empty;
        public string Warehouse { get; init; } = string.Empty;
        public string? Carrier { get; set; }
        public string? TrackingNumber { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
