namespace Erp.Application.DTOs;

public sealed record ApproveUserRequest(
    Guid UserId,
    bool AssignDefaultRole = true);
