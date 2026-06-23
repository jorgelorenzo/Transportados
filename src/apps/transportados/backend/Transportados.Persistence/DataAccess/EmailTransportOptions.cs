using Transportados.Domain.Api.Domain;

namespace Transportados.Persistence.DataAccess;

public sealed class EmailTransportOptions
{
    public string? SenderName { get; set; }
    public string? EmailFrom { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? User { get; set; }
    public string? Pass { get; set; }
    public bool UseSsl { get; set; }
    public string? CcEmails { get; set; }

    public bool IsConfigured =>
        Port > 0 &&
        !string.IsNullOrWhiteSpace(EmailFrom) &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Pass);

    public SmtpEmailTransportSettings ToTransportSettings(Settings tenantSettings)
    {
        var tenantHasPort = tenantSettings.SmtpPort > 0;
        var tenantHasSmtpTransport = tenantSettings.IsSmtpEnabled;
        return new SmtpEmailTransportSettings
        {
            SenderName = FirstNonEmpty(tenantSettings.Name, SenderName, "Transportados"),
            EmailFrom = FirstNonEmpty(tenantSettings.EmailFrom, EmailFrom),
            Host = FirstNonEmpty(tenantSettings.SmtpHost, Host),
            Port = tenantHasPort ? tenantSettings.SmtpPort : Port,
            User = FirstNonEmpty(tenantSettings.SmtpUser, User),
            Pass = FirstNonEmpty(tenantSettings.SmtpPass, Pass),
            UseSsl = tenantHasSmtpTransport ? tenantSettings.SmtpUseSSL : UseSsl
        };
    }

    public static bool IsTransportConfigured(SmtpEmailTransportSettings transport) =>
        transport.Port > 0 &&
        !string.IsNullOrWhiteSpace(transport.EmailFrom) &&
        !string.IsNullOrWhiteSpace(transport.Host) &&
        !string.IsNullOrWhiteSpace(transport.User) &&
        !string.IsNullOrWhiteSpace(transport.Pass);

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
