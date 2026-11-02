using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class EmailServiceTests
{
    private sealed class FakeTransport : IEmailTransport
    {
        private readonly Queue<Exception?> _outcomes = new();
        public List<(string To, string Subject, string? Password)> Sent { get; } = new();
        public int Attempts { get; private set; }

        public FakeTransport(params Exception?[] outcomes)
        {
            foreach (var outcome in outcomes) { _outcomes.Enqueue(outcome); }
        }

        public Task SendAsync(EmailSettings settings, MailMessage message, CancellationToken cancellationToken)
        {
            Attempts++;
            if (_outcomes.Count > 0 && _outcomes.Dequeue() is { } failure)
            {
                throw failure;
            }

            Sent.Add((message.To[0].Address, message.Subject, settings.Password));
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger : ILogger<EmailService>
    {
        public List<string> Lines { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
    }

    private static IConfiguration Config(bool enabled = true, string? password = "app-password") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Enabled"] = enabled.ToString(),
            ["Email:Host"] = "smtp.example.test",
            ["Email:Username"] = "sender@example.test",
            ["Email:Password"] = password,
            ["Email:FromAddress"] = "sender@example.test"
        }).Build();

    private static EmailService Service(FakeTransport transport, IConfiguration? config = null, ILogger<EmailService>? logger = null) =>
        new(config ?? Config(), logger ?? new ListLogger(), transport) { RetryDelays = new[] { TimeSpan.Zero, TimeSpan.Zero } };

    private static async Task<T> WaitFor<T>(Func<T> read, Func<T, bool> done)
    {
        for (var i = 0; i < 100 && !done(read()); i++) { await Task.Delay(50); }
        return read();
    }

    [Fact]
    public async Task Disabled_SendsNothing()
    {
        var transport = new FakeTransport();
        await Service(transport, Config(enabled: false)).TrySendAsync("a@example.test", "Hi", "Body", "test");
        Assert.Equal(0, transport.Attempts);
    }

    [Fact]
    public async Task WithoutWorker_SendsOnceInline()
    {
        var transport = new FakeTransport();
        await Service(transport).TrySendAsync("a@example.test", "Hi", "Body", "test");
        Assert.Single(transport.Sent);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("a@example.test, b@example.test")]
    [InlineData("Someone <a@example.test>")]
    public async Task InvalidRecipient_IsSkippedWithoutThrowing(string recipient)
    {
        var transport = new FakeTransport();
        await Service(transport).TrySendAsync(recipient, "Hi", "Body", "test");
        Assert.Equal(0, transport.Attempts);
    }

    [Fact]
    public async Task TransportFailure_NeverReachesTheCaller()
    {
        foreach (Exception failure in new Exception[] { new FormatException(), new ObjectDisposedException("client"), new SmtpException(SmtpStatusCode.GeneralFailure), new IOException() })
        {
            await Service(new FakeTransport(failure)).TrySendAsync("a@example.test", "Hi", "Body", "test");
        }
    }

    [Fact]
    public async Task IncompleteSettings_DoNotThrowOrSend()
    {
        var transport = new FakeTransport();
        await Service(transport, Config(password: null)).TrySendAsync("a@example.test", "Hi", "Body", "test");
        Assert.Equal(0, transport.Attempts);
    }

    [Fact]
    public async Task Queued_TemporaryFailureIsRetriedUntilItWorks()
    {
        var transport = new FakeTransport(new TimeoutException(), new IOException(), null);
        var service = Service(transport);
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(() => service.RunQueueAsync(stop.Token));
        await WaitFor(() => service.IsWorkerRunning, running => running);

        await service.TrySendAsync("a@example.test", "Hi", "Body", "test");
        await WaitFor(() => transport.Sent.Count, count => count == 1);

        stop.Cancel();
        await worker;
        Assert.Equal(3, transport.Attempts);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task Queued_PermanentFailureIsNotRetried()
    {
        var transport = new FakeTransport(new SmtpException(SmtpStatusCode.GeneralFailure, "Server response was: 5.7.8 Authentication failed"));
        var service = Service(transport);
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(() => service.RunQueueAsync(stop.Token));
        await WaitFor(() => service.IsWorkerRunning, running => running);

        await service.TrySendAsync("a@example.test", "Hi", "Body", "test");
        await WaitFor(() => transport.Attempts, attempts => attempts >= 1);
        await Task.Delay(200);

        stop.Cancel();
        await worker;
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task Queued_SendReturnsImmediately()
    {
        var service = Service(new FakeTransport());
        using var stop = new CancellationTokenSource();
        var worker = Task.Run(() => service.RunQueueAsync(stop.Token));
        await WaitFor(() => service.IsWorkerRunning, running => running);

        // Nothing is delivered inline: the call only queues, so it completes at once.
        await service.TrySendAsync("a@example.test", "Hi", "Body", "test").WaitAsync(TimeSpan.FromSeconds(2));

        stop.Cancel();
        await worker;
    }

    [Fact]
    public async Task Logs_NeverContainTheFullRecipientAddress()
    {
        var logger = new ListLogger();
        await Service(new FakeTransport(new FormatException("jane@example.test is bad")), logger: logger)
            .TrySendAsync("jane@example.test", "Hi", "Body", "test");

        Assert.NotEmpty(logger.Lines);
        Assert.All(logger.Lines, line => Assert.DoesNotContain("jane@example.test", line));
        Assert.Contains(logger.Lines, line => line.Contains("j***@example.test"));
    }

    [Fact]
    public async Task AppPasswordSpaces_AreIgnored()
    {
        var transport = new FakeTransport();
        await Service(transport, Config(password: "abcd efgh ijkl mnop")).TrySendAsync("a@example.test", "Hi", "Body", "test");
        Assert.Equal("abcdefghijklmnop", transport.Sent.Single().Password);
    }

    [Fact]
    public async Task SubjectLineBreaks_AreRemoved()
    {
        var transport = new FakeTransport();
        await Service(transport).TrySendAsync("a@example.test", "Hello\r\nBcc: evil@example.test", "Body", "test");
        Assert.DoesNotContain('\n', transport.Sent.Single().Subject);
        Assert.DoesNotContain('\r', transport.Sent.Single().Subject);
    }

    [Fact]
    public async Task SendTest_ExplainsProblemsWithoutSecrets()
    {
        var disabled = await Service(new FakeTransport(), Config(enabled: false)).SendTestAsync("a@example.test");
        Assert.False(disabled.Succeeded);

        var incomplete = await Service(new FakeTransport(), Config(password: null)).SendTestAsync("a@example.test");
        Assert.Contains("Password", incomplete.Error);

        var rejected = await Service(new FakeTransport(new SmtpException(SmtpStatusCode.ClientNotPermitted, "5.7.0 Authentication Required"))).SendTestAsync("a@example.test");
        Assert.Contains("rejected the sign-in", rejected.Error);
        Assert.DoesNotContain("app-password", rejected.Error);

        Assert.True((await Service(new FakeTransport()).SendTestAsync("a@example.test")).Succeeded);
    }

    [Fact]
    public void Status_ReportsDisabledNeedsSetupAndReady()
    {
        Assert.Equal(EmailState.Disabled, Service(new FakeTransport(), Config(enabled: false)).GetStatus().State);

        var needsSetup = Service(new FakeTransport(), Config(password: null)).GetStatus();
        Assert.Equal(EmailState.NeedsSetup, needsSetup.State);
        Assert.Equal(new[] { "Password" }, needsSetup.MissingSettings);

        Assert.Equal(EmailState.Ready, Service(new FakeTransport()).GetStatus().State);
    }

    [Theory]
    [InlineData("jane@example.test", "j***@example.test")]
    [InlineData("not-an-email", "(invalid address)")]
    [InlineData(null, "(invalid address)")]
    public void Mask_HidesTheLocalPart(string? input, string expected) => Assert.Equal(expected, EmailText.Mask(input));

    [Fact]
    public void IsTransient_SeparatesTemporaryFromPermanentProblems()
    {
        Assert.True(EmailService.IsTransient(new TimeoutException()));
        Assert.True(EmailService.IsTransient(new IOException()));
        Assert.True(EmailService.IsTransient(new SmtpException(SmtpStatusCode.ServiceNotAvailable)));
        Assert.True(EmailService.IsTransient(new SmtpException(SmtpStatusCode.GeneralFailure, "Failure sending mail.")));
        Assert.False(EmailService.IsTransient(new SmtpException(SmtpStatusCode.GeneralFailure, "Server response was: 5.7.8 Authentication failed")));
        Assert.False(EmailService.IsTransient(new FormatException()));
        Assert.False(EmailService.IsTransient(new InvalidOperationException()));
    }
}
