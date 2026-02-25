namespace Erp.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public User? ActorUser { get; private set; }

    public string Action { get; private set; } = string.Empty;
    public string? Target { get; private set; }
    public string? DetailJson { get; private set; }
    public string? Ip { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AuditLog()
    {
    }

    public AuditLog(Guid? actorUserId, string action, string? target = null, string? detailJson = null, string? ip = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action.Trim();
        Target = string.IsNullOrWhiteSpace(target) ? null : target.Trim();
        DetailJson = string.IsNullOrWhiteSpace(detailJson) ? null : detailJson.Trim();
        Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
