namespace Erp.Domain.Entities;

public sealed class User
{
    private readonly List<UserRole> _userRoles = new();

    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    private User()
    {
    }

    public User(string username, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        Id = Guid.NewGuid();
        Username = username.Trim();
        PasswordHash = passwordHash.Trim();
        IsActive = true;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public bool IsLockedOut(DateTime utcNow)
    {
        return LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;
    }

    public int GetLockoutRemainingSeconds(DateTime utcNow)
    {
        if (!IsLockedOut(utcNow))
        {
            return 0;
        }

        return (int)Math.Ceiling((LockoutEndUtc!.Value - utcNow).TotalSeconds);
    }

    public void RegisterLoginFailure(DateTime utcNow, int maxFailedCount, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;

        if (FailedLoginCount >= maxFailedCount)
        {
            LockoutEndUtc = utcNow.Add(lockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterLoginSuccess()
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash.Trim();
    }

    public void Disable()
    {
        IsActive = false;
    }

    public void Enable()
    {
        IsActive = true;
    }
}
