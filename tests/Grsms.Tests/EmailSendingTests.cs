using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Settings;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Email;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grsms.Tests;

/// <summary>
/// 11 November 2026: with a mail server set, emails are sent in the background so nobody waits for the server (GREL's took
/// 4-5 s per email: approving a request took 17 s). A small fake SMTP server stands in for the real one, answering slowly.
/// </summary>
public class EmailSendingTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private readonly FakeSmtpServer _smtp = new(answerDelay: TimeSpan.FromMilliseconds(400));

    public async Task InitializeAsync()
    {
        _smtp.Start();
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
        {
            [SettingCatalogue.SmtpHost] = "127.0.0.1", [SettingCatalogue.SmtpPort] = _smtp.Port.ToString(), [SettingCatalogue.SmtpSsl] = "false"
        });
    }

    public Task DisposeAsync()
    {
        _smtp.Dispose();
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Actions_hand_emails_over_at_once_and_the_background_sender_delivers_them()
    {
        var queue = _db.Services.GetRequiredService<EmailQueue>();
        var watch = Stopwatch.StartNew();
        using (var scope = _db.NewScope())
        {
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
            for (var i = 1; i <= 3; i++) await email.SendAsync($"person{i}@grel.test", $"Request RR-{i} approved", $"<p>Hello {i}</p>");
        }
        Assert.True(watch.ElapsedMilliseconds < 1000, $"queuing 3 emails took {watch.ElapsedMilliseconds} ms");
        Assert.Equal(3, queue.Count);
        Assert.Empty(_smtp.Messages); // nothing sent while the person waited

        var worker = new EmailSendingWorker(queue, _db.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailSendingWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        var until = DateTime.UtcNow.AddSeconds(30);
        while (_smtp.Messages.Count < 3 && DateTime.UtcNow < until) await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, _smtp.Messages.Count);
        Assert.Contains(_smtp.Messages, m => m.Contains("person2@grel.test") && m.Contains("Request RR-2 approved"));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task The_test_email_button_waits_for_the_server_and_reports_the_result()
    {
        _db.CurrentUser.SignInAs("sa", "Super Admin", Roles.SuperAdmin);
        using (var scope = _db.NewScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<SystemSettingsService>().SendTestEmailAsync("it@grel.test");
            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        }
        Assert.Contains(_smtp.Messages, m => m.Contains("GRSMS test email")); // sent before the button's answer came back
        Assert.Equal(0, _db.Services.GetRequiredService<EmailQueue>().Count);

        _smtp.Dispose(); // the server goes away: the button says so instead of pretending it worked
        using (var scope = _db.NewScope())
            Assert.False((await scope.ServiceProvider.GetRequiredService<SystemSettingsService>().SendTestEmailAsync("it@grel.test")).Succeeded);
    }

    /// <summary>Just enough SMTP for System.Net.Mail (no TLS, no login), answering each command after <paramref name="answerDelay"/>.</summary>
    private sealed class FakeSmtpServer(TimeSpan answerDelay) : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        public List<string> Messages { get; } = [];
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public void Start()
        {
            _listener.Start();
            _ = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    TcpClient client;
                    try { client = await _listener.AcceptTcpClientAsync(_stop.Token); } catch { return; }
                    _ = Task.Run(() => ServeAsync(client));
                }
            });
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII);
                var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                async Task Say(string line) { await Task.Delay(answerDelay); await writer.WriteLineAsync(line); }
                await Say("220 fake ESMTP ready");
                while (await reader.ReadLineAsync() is { } line)
                {
                    var cmd = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                    if (cmd is "EHLO" or "HELO") await Say("250 fake");
                    else if (cmd == "DATA")
                    {
                        await Say("354 go ahead");
                        var body = new StringBuilder();
                        while (await reader.ReadLineAsync() is { } data && data != ".") body.AppendLine(data);
                        lock (Messages) Messages.Add(body.ToString());
                        await Say("250 queued");
                    }
                    else if (cmd == "QUIT") { await Say("221 bye"); return; }
                    else await Say("250 ok");
                }
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }
}
