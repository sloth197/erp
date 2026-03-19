namespace Erp.Domain.Entities;

public sealed class EmailVerificationCode
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public int MaxAttemptCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastSentAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    private EmailVerificationCode()
    {
    }

    public EmailVerificationCode(
        string email,
        string purpose,
        string codeHash,
        DateTime expiresAtUtc,
        int maxAttemptCount = 5)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Purpose is required.", nameof(purpose));
        }

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Code hash is required.", nameof(codeHash));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAtUtc));
        }

        if (maxAttemptCount < 1)
        {
            throw new ArgumentException("Max attempt count must be at least 1.", nameof(maxAttemptCount));
        }

        Id = Guid.NewGuid();
        Email = email.Trim();
        Purpose = purpose.Trim();
        CodeHash = codeHash.Trim();
        AttemptCount = 0;
        MaxAttemptCount = maxAttemptCount;
        CreatedAtUtc = DateTime.UtcNow;
        LastSentAtUtc = CreatedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        LastAttemptAtUtc = null;
        VerifiedAtUtc = null;
        IsRevoked = false;
    }

    public bool CanVerify(DateTime utcNow)
    {
        if (IsRevoked)
        {
            return false;
        }

        if (VerifiedAtUtc.HasValue)
        {
            return false;
        }

        if (ExpiresAtUtc <= utcNow)
        {
            return false;
        }

        return AttemptCount < MaxAttemptCount;
    }

    public void RegisterAttempt(DateTime? attemptedAtUtc = null)
    {
        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkVerified(DateTime? verifiedAtUtc = null)
    {
        VerifiedAtUtc = verifiedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkSent(DateTime? sentAtUtc = null)
    {
        LastSentAtUtc = sentAtUtc ?? DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
