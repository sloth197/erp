namespace Erp.Domain.Entities;

public sealed class User
{
    private readonly List<UserRole> _userRoles = new();

    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Company { get; private set; }
    public UserJobGrade JobGrade { get; private set; } = UserJobGrade.Staff;
    public UserStatus Status { get; private set; } = UserStatus.Pending;
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }

    public DateTime? DisabledAtUtc { get; private set; }
    public Guid? DisabledByUserId { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public string? RejectReason { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    private User()
    {
    }

    public User(
        string username,
        string passwordHash,
        string? email = null,
        string? name = null,
        string? phoneNumber = null,
        string? company = null,
        UserJobGrade jobGrade = UserJobGrade.Staff)
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
        Email = NormalizeOptional(email);
        Name = NormalizeOptional(name);
        PhoneNumber = NormalizeOptional(phoneNumber);
        Company = NormalizeOptional(company);
        JobGrade = jobGrade;

        Status = UserStatus.Pending;
        IsActive = false;
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        CreatedAtUtc = DateTime.UtcNow;

        ApprovedAtUtc = null;
        ApprovedByUserId = null;
        DisabledAtUtc = null;
        DisabledByUserId = null;
        RejectedAtUtc = null;
        RejectedByUserId = null;
        RejectReason = null;
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

    public void SetEmail(string? email)
    {
        Email = NormalizeOptional(email);
    }

    public void SetProfile(string? name, string? phoneNumber, string? company)
    {
        Name = NormalizeOptional(name);
        PhoneNumber = NormalizeOptional(phoneNumber);
        Company = NormalizeOptional(company);
    }

    public void SetJobGrade(UserJobGrade jobGrade)
    {
        JobGrade = jobGrade;
    }

    public void Approve(Guid? approvedByUserId = null, DateTime? approvedAtUtc = null)
    {
        Status = UserStatus.Active;
        IsActive = true;

        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = approvedAtUtc ?? DateTime.UtcNow;

        DisabledByUserId = null;
        DisabledAtUtc = null;

        RejectedByUserId = null;
        RejectedAtUtc = null;
        RejectReason = null;
    }

    public void Reject(Guid? rejectedByUserId = null, string? reason = null, DateTime? rejectedAtUtc = null)
    {
        Status = UserStatus.Rejected;
        IsActive = false;

        RejectedByUserId = rejectedByUserId;
        RejectedAtUtc = rejectedAtUtc ?? DateTime.UtcNow;
        RejectReason = NormalizeOptional(reason);
    }

    public void Disable()
    {
        Disable(null, null);
    }

    public void Disable(Guid? disabledByUserId, DateTime? disabledAtUtc = null)
    {
        Status = UserStatus.Disabled;
        IsActive = false;

        DisabledByUserId = disabledByUserId;
        DisabledAtUtc = disabledAtUtc ?? DateTime.UtcNow;
    }

    public void Enable()
    {
        Approve(null, null);
    }

    public void Enable(Guid? approvedByUserId, DateTime? approvedAtUtc = null)
    {
        Approve(approvedByUserId, approvedAtUtc);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
