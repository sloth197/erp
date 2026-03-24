namespace Erp.Application.DTOs;

public sealed record RegisterRequest(
    string Username,
    string Password,
    string? Email,
    string? Name = null,
    string? PhoneNumber = null,
    string? Company = null);
