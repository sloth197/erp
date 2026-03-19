namespace Erp.Infrastructure.Email;

public enum SmtpSecurityMode
{
    None = 0,
    StartTls = 1,
    SslOnConnect = 2,
    StartTlsWhenAvailable = 3
}
