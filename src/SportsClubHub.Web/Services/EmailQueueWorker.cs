namespace SportsClubHub.Web.Services;

/// <summary>Runs <see cref="EmailService"/>'s delivery loop for the lifetime of the application.</summary>
public sealed class EmailQueueWorker : BackgroundService
{
    private readonly EmailService _email;

    public EmailQueueWorker(EmailService email) => _email = email;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _email.RunQueueAsync(stoppingToken);
}
