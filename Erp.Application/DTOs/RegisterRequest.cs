namespace Erp.Application.DTOs;

public sealed record RegisterRequest(
    string Username,
    string Password,
    string? Email);
