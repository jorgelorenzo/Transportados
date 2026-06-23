namespace Transportados.Persistence.DataAccess;

public interface IEmailSender
{
    bool IsEnabled { get; }
    Task<EmailSendResponse> SendEmailAsync(EmailSendRequest request, CancellationToken cancellationToken = default);
}

public sealed class EmailSendRequest
{
    public Guid? TenantId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientDisplayName { get; set; }
    public List<string> CcEmails { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public SmtpEmailTransportSettings Transport { get; set; } = new();
    public List<EmailAttachment> Attachments { get; set; } = new();
}

public sealed class SmtpEmailTransportSettings
{
    public string SenderName { get; set; } = string.Empty;
    public string EmailFrom { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
}

public sealed class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
}

public sealed class EmailSendResponse
{
    public bool Success { get; set; }
    public bool ShouldRetry { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExternalStatus { get; set; }
    public string? DeliveryStatus { get; set; }
    public string EventId { get; set; } = string.Empty;
}
