using System.Net;
using System.Net.Mail;

namespace SportsClubHub.Web.Services;

/// <summary>The part of sending that talks to the network, separated so tests can replace it.</summary>
public interface IEmailTransport
{
    Task SendAsync(EmailSettings settings, MailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// SMTP with STARTTLS (use port 587). SmtpClient does not support implicit TLS, so port 465 will not work.
/// The cancellation token ends the send, so nothing is left running after the client is disposed.
/// </summary>
public sealed class SmtpEmailTransport : IEmailTransport
{
    public async Task SendAsync(EmailSettings settings, MailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            Timeout = settings.TimeoutSeconds * 1000
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
