namespace Erp.Application.DTOs;

public sealed record RegisterResult(bool Success, string? ErrorMessage)
{
    public static RegisterResult Succeeded() => new(true, null);

    public static RegisterResult Failed(string errorMessage)
        => new(false, errorMessage);
}
