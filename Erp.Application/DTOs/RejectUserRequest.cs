namespace Erp.Application.DTOs;

public sealed record RejectUserRequest(
    Guid UserId,
    string? Reason = null);
