namespace Erp.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SmtpSecurityMode SecurityMode { get; set; } = SmtpSecurityMode.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "ERP <no-reply@example.com>";
}
