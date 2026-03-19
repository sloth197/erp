namespace Erp.Infrastructure.Services;

public sealed class EmailVerificationOptions
{
    public int CodeLength { get; set; } = 8;
    public int ExpiresInMinutes { get; set; } = 3;
    public int MaxAttemptCount { get; set; } = 5;
    public string DefaultPurpose { get; set; } = "signup";
    public string Subject { get; set; } = "[ERP] Verification Code";
}
