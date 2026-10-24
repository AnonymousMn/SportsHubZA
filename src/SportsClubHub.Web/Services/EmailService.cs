using System.Net;
using System.Net.Mail;

namespace SportsClubHub.Web.Services;

public sealed class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string recipient, string subject, string body)
    {
        var enabled = _configuration.GetValue<bool>("Email:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("Email is disabled; skipped message to {Recipient}.", recipient);
            return;
        }

        var host = _configuration["Email:Host"];
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];
        var fromAddress = _configuration["Email:FromAddress"] ?? username;
        var fromName = _configuration["Email:FromName"] ?? "Sports Club Hub SA";
        var port = _configuration.GetValue<int?>("Email:Port") ?? 587;

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Email is enabled but SMTP settings are incomplete.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };
        await client.SendMailAsync(message);
    }

    public async Task TrySendAsync(string recipient, string subject, string body, string purpose)
    {
        try
        {
            await SendAsync(recipient, subject, body);
        }
        catch (SmtpException exception)
        {
            _logger.LogError(exception, "Could not send {Purpose} email to {Recipient}.", purpose, recipient);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Email configuration prevented sending {Purpose} email to {Recipient}.", purpose, recipient);
        }
    }
}
