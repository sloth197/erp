using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;

namespace Erp.Infrastructure.Services;

public sealed class SalesOrderService : ISalesOrderQueryService, ISalesOrderCommandService
{
    private readonly object _syncRoot = new();
    private readonly IAccessControl _accessControl;
    private readonly List<SalesOrderState> _orders;

    public SalesOrderService(IAccessControl accessControl)
    {
        _accessControl = accessControl;
        _orders = BuildSeedOrders();
    }

    public Task<SalesOrderSearchResultDto> SearchSalesOrdersAsync(
        SearchSalesOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersRead);
        cancellationToken.ThrowIfCancellationRequested();

        query ??= new SearchSalesOrdersQuery();

        lock (_syncRoot)
        {
            var filtered = ApplyFilters(query).ToList();
            var items = filtered.Select(MapToDto).ToList();
            var today = DateTime.Today;

            var result = new SalesOrderSearchResultDto(
                items,
                TodayReceivedCount: filtered.Count(x => x.OrderDate.Date == today),
                PendingShipmentCount: filtered.Count(x => x.Status is "접수" or "확정"),
                PartialShipmentCount: filtered.Count(x => x.Status == "부분출고"),
                CreditRiskCount: filtered.Count(x => x.IsCreditRisk && x.Status != "완료"));

            return Task.FromResult(result);
        }
    }

    public Task<DocumentCommandResultDto> CreateOrderAsync(
        CreateSalesOrderCommand command,
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
            var customerName = string.IsNullOrWhiteSpace(command.CustomerName)
                ? "신규 고객사"
                : command.CustomerName.Trim();
            var channel = NormalizeChannel(command.Channel);
            var requestedDeliveryDate = (command.RequestedDeliveryDate ?? DateTime.Today.AddDays(2)).Date;

            var order = new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = GenerateSalesOrderNumber(),
                CustomerName = customerName,
                OrderDate = DateTime.Today,
                RequestedDeliveryDate = requestedDeliveryDate,
                ItemCount = 2,
                OrderAmount = 5180000m,
                Priority = "보통",
                Status = "접수",
                Channel = channel,
                IsCreditRisk = false
            };

            _orders.Add(order);

            return Task.FromResult(new DocumentCommandResultDto(
                order.Id,
                order.SoNumber,
                order.Status,
                $"{order.SoNumber} 신규 주문을 생성했습니다."));
        }
    }

    public Task<DocumentCommandResultDto> ConfirmOrderAsync(
        ConfirmSalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null || command.SalesOrderId == Guid.Empty)
        {
            throw new InvalidOperationException("주문 식별자가 올바르지 않습니다.");
        }

        lock (_syncRoot)
        {
            var order = _orders.FirstOrDefault(x => x.Id == command.SalesOrderId);
            if (order is null)
            {
                throw new InvalidOperationException("대상 주문을 찾을 수 없습니다.");
            }

            if (order.Status == "완료")
            {
                return Task.FromResult(new DocumentCommandResultDto(
                    order.Id,
                    order.SoNumber,
                    order.Status,
                    "완료된 주문은 확정할 수 없습니다."));
            }

            if (order.Status is "확정" or "부분출고")
            {
                return Task.FromResult(new DocumentCommandResultDto(
                    order.Id,
                    order.SoNumber,
                    order.Status,
                    $"{order.SoNumber}는 이미 확정 이후 단계입니다."));
            }

            order.Status = "확정";
            return Task.FromResult(new DocumentCommandResultDto(
                order.Id,
                order.SoNumber,
                order.Status,
                $"{order.SoNumber} 주문을 확정했습니다."));
        }
    }

    public Task<OperationResultDto> CreateDeliveryPlanAsync(
        CreateSalesDeliveryPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.SalesOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null || command.SalesOrderId == Guid.Empty)
        {
            throw new InvalidOperationException("주문 식별자가 올바르지 않습니다.");
        }

        lock (_syncRoot)
        {
            var order = _orders.FirstOrDefault(x => x.Id == command.SalesOrderId);
            if (order is null)
            {
                throw new InvalidOperationException("대상 주문을 찾을 수 없습니다.");
            }

            return Task.FromResult(new OperationResultDto(
                $"{order.SoNumber} 배차 계획을 생성했습니다. (데모)",
                AffectedCount: 1));
        }
    }

    private IEnumerable<SalesOrderState> ApplyFilters(SearchSalesOrdersQuery query)
    {
        var customerKeyword = query.CustomerKeyword?.Trim();
        var channel = NormalizeFilterValue(query.Channel);
        var status = NormalizeFilterValue(query.Status);
        var orderDate = query.OrderDate?.Date;

        var filtered = _orders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(customerKeyword))
        {
            filtered = filtered.Where(x =>
                x.CustomerName.Contains(customerKeyword, StringComparison.OrdinalIgnoreCase));
        }

        if (orderDate.HasValue)
        {
            filtered = filtered.Where(x => x.OrderDate.Date == orderDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            filtered = filtered.Where(x => string.Equals(x.Channel, channel, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.Ordinal));
        }

        return filtered
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.SoNumber);
    }

    private string GenerateSalesOrderNumber()
    {
        var prefix = $"SO-{DateTime.Today:yyyyMM}-";
        var next = _orders
            .Where(x => x.SoNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.SoNumber[prefix.Length..])
            .Select(x => int.TryParse(x, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:000}";
    }

    private static string NormalizeChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return "직판";
        }

        var trimmed = channel.Trim();
        return trimmed switch
        {
            "직판" or "대리점" or "온라인" => trimmed,
            _ => "직판"
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

    private static SalesOrderListDto MapToDto(SalesOrderState state)
    {
        return new SalesOrderListDto(
            state.Id,
            state.SoNumber,
            state.CustomerName,
            state.OrderDate,
            state.RequestedDeliveryDate,
            state.ItemCount,
            state.OrderAmount,
            state.Priority,
            state.Status,
            state.Channel,
            state.IsCreditRisk);
    }

    private static List<SalesOrderState> BuildSeedOrders()
    {
        var today = DateTime.Today;
        return
        [
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-019",
                CustomerName = "대한유통",
                OrderDate = today,
                RequestedDeliveryDate = today.AddDays(1),
                ItemCount = 4,
                OrderAmount = 9820000m,
                Priority = "높음",
                Status = "접수",
                Channel = "직판",
                IsCreditRisk = false
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-018",
                CustomerName = "세진메디칼",
                OrderDate = today,
                RequestedDeliveryDate = today.AddDays(2),
                ItemCount = 3,
                OrderAmount = 12100000m,
                Priority = "보통",
                Status = "확정",
                Channel = "대리점",
                IsCreditRisk = false
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-017",
                CustomerName = "아람전자",
                OrderDate = today.AddDays(-1),
                RequestedDeliveryDate = today.AddDays(1),
                ItemCount = 7,
                OrderAmount = 18400000m,
                Priority = "긴급",
                Status = "부분출고",
                Channel = "온라인",
                IsCreditRisk = true
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-016",
                CustomerName = "원진상사",
                OrderDate = today.AddDays(-2),
                RequestedDeliveryDate = today.AddDays(2),
                ItemCount = 2,
                OrderAmount = 3250000m,
                Priority = "보통",
                Status = "완료",
                Channel = "직판",
                IsCreditRisk = false
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-015",
                CustomerName = "비전오토",
                OrderDate = today.AddDays(-2),
                RequestedDeliveryDate = today.AddDays(3),
                ItemCount = 5,
                OrderAmount = 7460000m,
                Priority = "높음",
                Status = "확정",
                Channel = "대리점",
                IsCreditRisk = false
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202604-014",
                CustomerName = "클린라이프",
                OrderDate = today.AddDays(-3),
                RequestedDeliveryDate = today,
                ItemCount = 2,
                OrderAmount = 2120000m,
                Priority = "낮음",
                Status = "접수",
                Channel = "온라인",
                IsCreditRisk = false
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202603-062",
                CustomerName = "하이덱스",
                OrderDate = today.AddDays(-7),
                RequestedDeliveryDate = today.AddDays(-1),
                ItemCount = 9,
                OrderAmount = 26600000m,
                Priority = "긴급",
                Status = "부분출고",
                Channel = "직판",
                IsCreditRisk = true
            },
            new SalesOrderState
            {
                Id = Guid.NewGuid(),
                SoNumber = "SO-202603-061",
                CustomerName = "모아플러스",
                OrderDate = today.AddDays(-9),
                RequestedDeliveryDate = today.AddDays(1),
                ItemCount = 3,
                OrderAmount = 4180000m,
                Priority = "보통",
                Status = "완료",
                Channel = "온라인",
                IsCreditRisk = false
            }
        ];
    }

    private sealed class SalesOrderState
    {
        public Guid Id { get; init; }
        public string SoNumber { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public DateTime OrderDate { get; init; }
        public DateTime RequestedDeliveryDate { get; init; }
        public int ItemCount { get; init; }
        public decimal OrderAmount { get; init; }
        public string Priority { get; init; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Channel { get; init; } = string.Empty;
        public bool IsCreditRisk { get; init; }
    }
}
