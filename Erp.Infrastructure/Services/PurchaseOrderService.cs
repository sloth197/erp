using Erp.Application.Authorization;
using Erp.Application.Commands;
using Erp.Application.DTOs;
using Erp.Application.Interfaces;
using Erp.Application.Queries;

namespace Erp.Infrastructure.Services;

public sealed class PurchaseOrderService : IPurchaseOrderQueryService, IPurchaseOrderCommandService
{
    private readonly object _syncRoot = new();
    private readonly IAccessControl _accessControl;
    private readonly List<PurchaseOrderState> _orders;

    public PurchaseOrderService(IAccessControl accessControl)
    {
        _accessControl = accessControl;
        _orders = BuildSeedOrders();
    }

    public Task<PurchaseOrderSearchResultDto> SearchPurchaseOrdersAsync(
        SearchPurchaseOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.PurchaseOrdersRead);
        cancellationToken.ThrowIfCancellationRequested();

        query ??= new SearchPurchaseOrdersQuery();

        lock (_syncRoot)
        {
            var filtered = ApplyFilters(query).ToList();
            var result = BuildSearchResult(filtered);
            return Task.FromResult(result);
        }
    }

    public Task<DocumentCommandResultDto> CreateDraftAsync(
        CreatePurchaseOrderDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.PurchaseOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null)
        {
            throw new InvalidOperationException("요청 데이터가 없습니다.");
        }

        lock (_syncRoot)
        {
            var supplierName = string.IsNullOrWhiteSpace(command.SupplierName)
                ? "신규 공급처"
                : command.SupplierName.Trim();
            var itemSummary = string.IsNullOrWhiteSpace(command.ItemSummary)
                ? "신규 품목 1건"
                : command.ItemSummary.Trim();
            var dueDate = (command.DueDate ?? DateTime.Today.AddDays(5)).Date;

            var state = new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = GeneratePurchaseOrderNumber(),
                SupplierName = supplierName,
                ItemSummary = itemSummary,
                OrderDate = DateTime.Today,
                DueDate = dueDate,
                ItemCount = 1,
                OrderAmount = 3250000m,
                ReceiptStatus = "입고대기",
                Owner = "구매팀",
                Status = "작성중"
            };

            _orders.Add(state);

            return Task.FromResult(new DocumentCommandResultDto(
                state.Id,
                state.PoNumber,
                state.Status,
                $"{state.PoNumber} 신규 발주 초안을 생성했습니다."));
        }
    }

    public Task<DocumentCommandResultDto> RequestApprovalAsync(
        RequestPurchaseOrderApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        _accessControl.DemandPermission(PermissionCodes.PurchaseOrdersWrite);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is null || command.PurchaseOrderId == Guid.Empty)
        {
            throw new InvalidOperationException("발주 식별자가 올바르지 않습니다.");
        }

        lock (_syncRoot)
        {
            var order = _orders.FirstOrDefault(x => x.Id == command.PurchaseOrderId);
            if (order is null)
            {
                throw new InvalidOperationException("대상 발주를 찾을 수 없습니다.");
            }

            if (order.Status == "입고완료")
            {
                return Task.FromResult(new DocumentCommandResultDto(
                    order.Id,
                    order.PoNumber,
                    order.Status,
                    "이미 입고 완료된 발주는 승인 요청할 수 없습니다."));
            }

            if (order.Status is "승인완료" or "승인대기")
            {
                return Task.FromResult(new DocumentCommandResultDto(
                    order.Id,
                    order.PoNumber,
                    order.Status,
                    $"{order.PoNumber}는 이미 승인 처리 상태입니다."));
            }

            order.Status = "승인완료";

            return Task.FromResult(new DocumentCommandResultDto(
                order.Id,
                order.PoNumber,
                order.Status,
                $"{order.PoNumber} 승인 요청을 반영했습니다."));
        }
    }

    private PurchaseOrderSearchResultDto BuildSearchResult(IReadOnlyList<PurchaseOrderState> filtered)
    {
        var today = DateTime.Today;
        var weekStart = GetWeekStartMonday(today);
        var thisWeek = filtered
            .Where(x => x.OrderDate.Date >= weekStart && x.OrderDate.Date <= today)
            .ToList();

        var items = filtered.Select(MapToDto).ToList();
        var pendingApprovalCount = filtered.Count(x => x.Status is "작성중" or "승인대기");
        var delayedCount = filtered.Count(x => x.DueDate.Date < today && x.ReceiptStatus != "입고완료");
        var weekOrderAmount = thisWeek.Sum(x => x.OrderAmount);

        return new PurchaseOrderSearchResultDto(
            items,
            thisWeek.Count,
            pendingApprovalCount,
            delayedCount,
            weekOrderAmount);
    }

    private IEnumerable<PurchaseOrderState> ApplyFilters(SearchPurchaseOrdersQuery query)
    {
        var supplierKeyword = query.SupplierKeyword?.Trim();
        var itemKeyword = query.ItemKeyword?.Trim();
        var status = NormalizeFilterValue(query.Status);
        var dueDate = query.DueDate?.Date;

        var filtered = _orders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(supplierKeyword))
        {
            filtered = filtered.Where(x =>
                x.SupplierName.Contains(supplierKeyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(itemKeyword))
        {
            filtered = filtered.Where(x =>
                x.ItemSummary.Contains(itemKeyword, StringComparison.OrdinalIgnoreCase));
        }

        if (dueDate.HasValue)
        {
            filtered = filtered.Where(x => x.DueDate.Date == dueDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x => string.Equals(x.Status, status, StringComparison.Ordinal));
        }

        return filtered
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.PoNumber);
    }

    private string GeneratePurchaseOrderNumber()
    {
        var prefix = $"PO-{DateTime.Today:yyyyMM}-";
        var next = _orders
            .Where(x => x.PoNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.PoNumber[prefix.Length..])
            .Select(x => int.TryParse(x, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:000}";
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

    private static DateTime GetWeekStartMonday(DateTime date)
    {
        var delta = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-delta);
    }

    private static PurchaseOrderListDto MapToDto(PurchaseOrderState state)
    {
        return new PurchaseOrderListDto(
            state.Id,
            state.PoNumber,
            state.SupplierName,
            state.ItemSummary,
            state.OrderDate,
            state.DueDate,
            state.ItemCount,
            state.OrderAmount,
            state.ReceiptStatus,
            state.Owner,
            state.Status);
    }

    private static List<PurchaseOrderState> BuildSeedOrders()
    {
        var today = DateTime.Today;
        return
        [
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202604-012",
                SupplierName = "한빛테크",
                ItemSummary = "베어링, 샤프트 외 5건",
                OrderDate = today.AddDays(-1),
                DueDate = today.AddDays(3),
                ItemCount = 6,
                OrderAmount = 12400000m,
                ReceiptStatus = "입고대기",
                Owner = "구매1팀",
                Status = "승인완료"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202604-011",
                SupplierName = "세림머티리얼",
                ItemSummary = "알루미늄 플레이트 2건",
                OrderDate = today.AddDays(-2),
                DueDate = today.AddDays(4),
                ItemCount = 2,
                OrderAmount = 8400000m,
                ReceiptStatus = "부분입고",
                Owner = "구매2팀",
                Status = "승인완료"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202604-010",
                SupplierName = "대동산업",
                ItemSummary = "고무패킹 3건",
                OrderDate = today.AddDays(-3),
                DueDate = today.AddDays(-1),
                ItemCount = 3,
                OrderAmount = 2300000m,
                ReceiptStatus = "입고대기",
                Owner = "구매1팀",
                Status = "승인대기"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202604-009",
                SupplierName = "동서정밀",
                ItemSummary = "체결부품 10건",
                OrderDate = today.AddDays(-4),
                DueDate = today.AddDays(2),
                ItemCount = 10,
                OrderAmount = 6150000m,
                ReceiptStatus = "입고완료",
                Owner = "구매3팀",
                Status = "입고완료"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202604-008",
                SupplierName = "KM상사",
                ItemSummary = "전장모듈 4건",
                OrderDate = today.AddDays(-5),
                DueDate = today.AddDays(1),
                ItemCount = 4,
                OrderAmount = 15200000m,
                ReceiptStatus = "입고대기",
                Owner = "구매2팀",
                Status = "작성중"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202603-041",
                SupplierName = "태성화학",
                ItemSummary = "윤활제 2건",
                OrderDate = today.AddDays(-8),
                DueDate = today.AddDays(-2),
                ItemCount = 2,
                OrderAmount = 1750000m,
                ReceiptStatus = "입고대기",
                Owner = "구매1팀",
                Status = "작성중"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202603-040",
                SupplierName = "미래소재",
                ItemSummary = "필름/라벨 6건",
                OrderDate = today.AddDays(-10),
                DueDate = today.AddDays(-1),
                ItemCount = 6,
                OrderAmount = 2980000m,
                ReceiptStatus = "입고완료",
                Owner = "구매3팀",
                Status = "입고완료"
            },
            new PurchaseOrderState
            {
                Id = Guid.NewGuid(),
                PoNumber = "PO-202603-039",
                SupplierName = "유진메탈",
                ItemSummary = "강판 3건",
                OrderDate = today.AddDays(-12),
                DueDate = today.AddDays(5),
                ItemCount = 3,
                OrderAmount = 9680000m,
                ReceiptStatus = "입고대기",
                Owner = "구매2팀",
                Status = "승인완료"
            }
        ];
    }

    private sealed class PurchaseOrderState
    {
        public Guid Id { get; init; }
        public string PoNumber { get; init; } = string.Empty;
        public string SupplierName { get; init; } = string.Empty;
        public string ItemSummary { get; init; } = string.Empty;
        public DateTime OrderDate { get; init; }
        public DateTime DueDate { get; init; }
        public int ItemCount { get; init; }
        public decimal OrderAmount { get; init; }
        public string ReceiptStatus { get; init; } = string.Empty;
        public string Owner { get; init; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
