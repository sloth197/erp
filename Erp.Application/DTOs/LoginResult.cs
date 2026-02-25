namespace Erp.Application.DTOs;

public sealed record LoginResult(bool Success, string? ErrorMessage, int? LockoutRemainingSeconds)
{
    public static LoginResult Succeeded() => new(true, null, null);

    public static LoginResult Failed(string errorMessage, int? lockoutRemainingSeconds = null)
        => new(false, errorMessage, lockoutRemainingSeconds);
}
