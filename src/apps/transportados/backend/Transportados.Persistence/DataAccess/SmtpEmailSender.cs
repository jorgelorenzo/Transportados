using System.Net;
using System.Net.Mail;

namespace Transportados.Persistence.DataAccess;

public sealed class SmtpEmailSender : IEmailSender
{
    public bool IsEnabled => true;

    public async Task<EmailSendResponse> SendEmailAsync(
        EmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var eventId = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey;

        if (!EmailTransportOptions.IsTransportConfigured(request.Transport))
        {
            return Failed(eventId, "Transporte SMTP no configurado.", shouldRetry: false);
        }

        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return Failed(eventId, "Debe indicar un email destino.", shouldRetry: false);
        }

        try
        {
            using var message = BuildMessage(request);
            using var client = new SmtpClient(request.Transport.Host, request.Transport.Port)
            {
                EnableSsl = request.Transport.UseSsl,
                Credentials = new NetworkCredential(request.Transport.User, request.Transport.Pass)
            };

            await client.SendMailAsync(message, cancellationToken);

            return new EmailSendResponse
            {
                Success = true,
                ProviderMessageId = eventId,
                ExternalStatus = "sent",
                DeliveryStatus = "sent",
                EventId = eventId
            };
        }
        catch (SmtpException ex)
        {
            return Failed(eventId, ex.Message, shouldRetry: true, ex.StatusCode.ToString());
        }
        catch (FormatException ex)
        {
            return Failed(eventId, ex.Message, shouldRetry: false);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(eventId, ex.Message, shouldRetry: false);
        }
    }

    private static MailMessage BuildMessage(EmailSendRequest request)
    {
        var senderName = string.IsNullOrWhiteSpace(request.Transport.SenderName)
            ? "Transportados"
            : request.Transport.SenderName.Trim();
        var message = new MailMessage
        {
            From = new MailAddress(request.Transport.EmailFrom.Trim(), senderName),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(request.RecipientEmail.Trim(), request.RecipientDisplayName));
        foreach (var cc in request.CcEmails.Select(NormalizeEmail).Where(email => email.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            message.CC.Add(cc);
        }

        foreach (var attachment in request.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.ContentBase64))
            {
                continue;
            }

            var bytes = Convert.FromBase64String(attachment.ContentBase64);
            var stream = new MemoryStream(bytes);
            message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
        }

        return message;
    }

    private static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static EmailSendResponse Failed(
        string eventId,
        string errorMessage,
        bool shouldRetry,
        string? externalStatus = null) =>
        new()
        {
            Success = false,
            ShouldRetry = shouldRetry,
            ErrorMessage = errorMessage,
            ExternalStatus = externalStatus,
            DeliveryStatus = "failed",
            EventId = eventId
        };
}
