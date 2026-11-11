using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Email;

/// <summary>One email waiting to be sent.</summary>
public sealed record OutgoingEmail(string To, string Subject, string HtmlBody, DateTime QueuedAt);

/// <summary>
/// 11 November 2026: emails waiting for the mail server. The page that caused an email returns at once; the
/// <see cref="EmailSendingWorker"/> sends them in the background. In memory: an email still waiting when GRSMS stops is
/// sent while it shuts down (up to 30 seconds), otherwise it is logged as not sent.
/// </summary>
public sealed class EmailQueue
{
    // Not SingleReader: that variant cannot report Count.
    private readonly Channel<OutgoingEmail> _channel = Channel.CreateUnbounded<OutgoingEmail>();

    public void Enqueue(OutgoingEmail email) => _channel.Writer.TryWrite(email);

    public ChannelReader<OutgoingEmail> Reader => _channel.Reader;

    /// <summary>How many emails are waiting (for the health page and tests).</summary>
    public int Count => _channel.Reader.Count;
}

/// <summary>
/// Sends the queued emails one by one through <see cref="EmailService.SendNowAsync"/> (so the System settings in force at
/// sending time apply). A failed email is tried again after 10 seconds and after 1 minute, then logged as not sent.
/// </summary>
public sealed class EmailSendingWorker(EmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailSendingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)];
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var email in queue.Reader.ReadAllAsync(stoppingToken))
                await SendWithRetriesAsync(email, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }

        // Shutting down: send what is still waiting, for a short while.
        using var grace = new CancellationTokenSource(ShutdownGrace);
        while (queue.Reader.TryRead(out var left))
        {
            if (grace.IsCancellationRequested)
            {
                logger.LogError("GRSMS stopped before the email '{Subject}' to {To} could be sent.", left.Subject, left.To);
                continue;
            }
            await SendOnceAsync(left, grace.Token);
        }
    }

    private async Task SendWithRetriesAsync(OutgoingEmail email, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (await SendOnceAsync(email, ct)) return;
            if (attempt >= RetryDelays.Length)
            {
                logger.LogError("The email '{Subject}' to {To} could not be sent after {Attempts} attempts and was dropped.", email.Subject, email.To, attempt + 1);
                return;
            }
            try { await Task.Delay(RetryDelays[attempt], ct); }
            catch (OperationCanceledException) { queue.Enqueue(email); return; } // stopping: the shutdown loop tries it once more
        }
    }

    private async Task<bool> SendOnceAsync(OutgoingEmail email, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var started = DateTime.UtcNow;
            await scope.ServiceProvider.GetRequiredService<EmailService>().SendNowAsync(email.To, email.Subject, email.HtmlBody, ct);
            logger.LogInformation("Email '{Subject}' sent in {Milliseconds} ms ({Waited} ms after it was queued).", email.Subject,
                (int)(DateTime.UtcNow - started).TotalMilliseconds, (int)(DateTime.UtcNow - email.QueuedAt).TotalMilliseconds);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Sending the email '{Subject}' to {To} failed.", email.Subject, email.To);
            return false;
        }
    }
}
