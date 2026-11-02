using System.Net.Mail;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace SportsClubHub.Web.Services;

/// <summary>A message waiting to be delivered. The purpose is a short label used in logs.</summary>
public sealed record QueuedEmail(string Recipient, string Subject, string Body, string Purpose);

/// <summary>
/// Sends notification e-mail without ever getting in the way of the person using the site.
/// <list type="bullet">
/// <item><see cref="TrySendAsync"/> never throws. When the background worker is running it only queues the message,
/// so a slow or broken mail server cannot delay a registration; temporary failures are retried.</item>
/// <item>Logs never contain a full recipient address (see <see cref="EmailText.Mask"/>) or any secret.</item>
/// <item>The queue lives in memory: queued mail is lost if the app restarts before it is sent.</item>
/// </list>
/// </summary>
public sealed partial class EmailService
{
    private const int QueueCapacity = 200;

    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailTransport _transport;
    private readonly Channel<QueuedEmail> _queue = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(QueueCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private volatile bool _workerRunning;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IEmailTransport? transport = null)
    {
        _configuration = configuration;
        _logger = logger;
        _transport = transport ?? new SmtpEmailTransport();
    }

    /// <summary>How long to wait before each retry of a temporary failure (so three attempts in total).</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } = new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) };

    /// <summary>True while the background delivery loop is running (messages are then queued, not sent inline).</summary>
    public bool IsWorkerRunning => _workerRunning;

    public EmailStatus GetStatus() => EmailSettings.From(_configuration).ToStatus();

    /// <summary>Queues (or, without the worker, sends) a message. Never throws, so callers cannot be broken by e-mail.</summary>
    public async Task TrySendAsync(string recipient, string subject, string body, string purpose)
    {
        try
        {
            var settings = EmailSettings.From(_configuration);
            if (!settings.Enabled)
            {
                _logger.LogInformation("Email is disabled; skipped {Purpose} message.", purpose);
                return;
            }

            if (!EmailText.TryNormaliseAddress(recipient, out var address))
            {
                _logger.LogWarning("Skipped {Purpose} email because the recipient address is not valid.", purpose);
                return;
            }

            var message = new QueuedEmail(address, EmailText.CleanSubject(subject), body, purpose);
            if (_workerRunning)
            {
                if (!_queue.Writer.TryWrite(message))
                {
                    _logger.LogWarning("Email queue is full; dropped a {Purpose} message.", purpose);
                }

                return;
            }

            await DeliverAsync(message, maxAttempts: 1, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogError("Unexpected problem handling a {Purpose} email: {Reason}.", purpose, Describe(exception));
        }
    }

    /// <summary>Sends straight away and throws on failure. Kept for callers that need the result.</summary>
    public Task SendAsync(string recipient, string subject, string body) =>
        SendCoreAsync(new QueuedEmail(recipient, EmailText.CleanSubject(subject), body, "direct"), CancellationToken.None);

    /// <summary>Sends one test message and explains a failure in plain words (used from the admin dashboard).</summary>
    public async Task<OperationResult> SendTestAsync(string recipient)
    {
        var settings = EmailSettings.From(_configuration);
        if (!settings.Enabled)
        {
            return OperationResult.Fail("Email is turned off. Set Email__Enabled=true to send mail.");
        }

        if (!settings.IsComplete)
        {
            return OperationResult.Fail("Email is missing settings: " + string.Join(", ", settings.Missing) + ".");
        }

        if (!EmailText.TryNormaliseAddress(recipient, out var address))
        {
            return OperationResult.Fail("Your sign-in name is not a valid e-mail address, so a test cannot be sent to it.");
        }

        try
        {
            await SendCoreAsync(new QueuedEmail(address, "Sports Club Hub SA test email",
                "This is a test message from the Sports Club Hub SA admin area. Email sending works.", "test"), CancellationToken.None);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogWarning("Test email failed: {Reason}.", Describe(exception));
            return OperationResult.Fail(Explain(exception, settings));
        }
    }

    /// <summary>Delivers queued messages until the application stops. Run by <see cref="EmailQueueWorker"/>.</summary>
    public async Task RunQueueAsync(CancellationToken stoppingToken)
    {
        var settings = EmailSettings.From(_configuration);
        if (settings.Enabled && !settings.IsComplete)
        {
            _logger.LogWarning("Email is enabled but these settings are missing: {Missing}. Messages will not be sent.", string.Join(", ", settings.Missing));
        }

        _workerRunning = true;
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverAsync(message, RetryDelays.Count + 1, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _workerRunning = false;
        }
    }

    private async Task DeliverAsync(QueuedEmail message, int maxAttempts, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await SendCoreAsync(message, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                var willRetry = attempt < maxAttempts && IsTransient(exception);
                _logger.LogWarning(
                    "Could not send {Purpose} email to {Recipient} (attempt {Attempt} of {Max}): {Reason}.{Retry}",
                    message.Purpose, EmailText.Mask(message.Recipient), attempt, maxAttempts, Describe(exception),
                    willRetry ? " Will retry." : string.Empty);

                if (!willRetry)
                {
                    return;
                }

                var delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Count - 1)];
                try { await Task.Delay(delay, cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task SendCoreAsync(QueuedEmail item, CancellationToken cancellationToken)
    {
        var settings = EmailSettings.From(_configuration);
        if (!settings.Enabled)
        {
            return;
        }

        if (!settings.IsComplete)
        {
            throw new InvalidOperationException("Email is enabled but SMTP settings are incomplete: " + string.Join(", ", settings.Missing) + ".");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress!, settings.FromName),
            Subject = item.Subject,
            Body = item.Body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(item.Recipient));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            await _transport.SendAsync(settings, message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The mail server did not answer within {settings.TimeoutSeconds} seconds.");
        }
    }

    /// <summary>True for problems that may go away on their own (network trouble, busy server), not for bad settings or addresses.</summary>
    public static bool IsTransient(Exception exception)
    {
        if (exception is TimeoutException or IOException or SocketException)
        {
            return true;
        }

        if (exception.InnerException is IOException or SocketException)
        {
            return true;
        }

        if (exception is SmtpException smtp)
        {
            return smtp.StatusCode switch
            {
                SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy or SmtpStatusCode.InsufficientStorage
                    or SmtpStatusCode.LocalErrorInProcessing => true,
                SmtpStatusCode.GeneralFailure => !PermanentReply().IsMatch(smtp.Message),
                _ => false
            };
        }

        return false;
    }

    /// <summary>A description that is safe to log: the kind of problem, never the message text (which can hold an address).</summary>
    private static string Describe(Exception exception) =>
        exception is SmtpException smtp ? $"{exception.GetType().Name} ({smtp.StatusCode})" : exception.GetType().Name;

    private static string Explain(Exception exception, EmailSettings settings)
    {
        if (exception is SmtpException smtp &&
            (smtp.StatusCode is SmtpStatusCode.ClientNotPermitted or SmtpStatusCode.MustIssueStartTlsFirst ||
             smtp.Message.Contains("Authentication", StringComparison.OrdinalIgnoreCase) || smtp.Message.Contains("5.7.", StringComparison.Ordinal)))
        {
            return "The mail server rejected the sign-in. Check Email__Username and use an app password (for Gmail, a Google App Password), not the normal password.";
        }

        return exception switch
        {
            TimeoutException => $"The mail server {settings.Host}:{settings.Port} did not answer in time.",
            SocketException or IOException => $"Could not reach the mail server {settings.Host}:{settings.Port}.",
            FormatException => "A configured address is not valid. Check Email__FromAddress.",
            _ => $"The test email could not be sent ({Describe(exception)}). Check the server log."
        };
    }

    [GeneratedRegex(@"\b5\.\d\.\d+\b")]
    private static partial Regex PermanentReply();
}
