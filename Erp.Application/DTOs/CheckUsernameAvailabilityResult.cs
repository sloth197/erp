namespace Erp.Application.DTOs;

public sealed record CheckUsernameAvailabilityResult(bool Available, string? Message)
{
    public static CheckUsernameAvailabilityResult AvailableResult(string? message = null)
        => new(true, message);

    public static CheckUsernameAvailabilityResult Unavailable(string message)
        => new(false, message);
}
