using System.Text;
using Grsms.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grsms.Infrastructure.Notifications;

/// <summary>Settings for text messages (section "Sms" in appsettings.json). See docs/06-configuration.md.</summary>
public class SmsOptions
{
    /// <summary>False (the default): messages are only written to <see cref="PickupDirectory"/>, nothing is sent.</summary>
    public bool Enabled { get; set; }

    /// <summary>The SMS provider. Only "Hubtel" is built in.</summary>
    public string Provider { get; set; } = "Hubtel";

    /// <summary>Sender name shown on the phone (max 11 letters, registered with the provider).</summary>
    public string SenderId { get; set; } = "GREL";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>The provider's send address. Check it against the provider's current documentation before switching on.</summary>
    public string ApiUrl { get; set; } = "https://smsc.hubtel.com/v1/messages/send";

    /// <summary>Where messages are written while SMS is switched off.</summary>
    public string PickupDirectory { get; set; } = "App_Data/sms";
}

/// <summary>
/// Sends text messages through Hubtel when switched on; otherwise writes each one to a .txt file, so what would have been
/// sent can be checked. Failures are logged by the caller and never undo a workflow step.
/// </summary>
public class SmsSender(IOptions<SmsOptions> options, ILogger<SmsSender> logger) : ISmsSender
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SmsOptions _options = options.Value;

    public async Task SendAsync(string phone, string text, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            var folder = Path.GetFullPath(_options.PickupDirectory, AppContext.BaseDirectory);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(file, $"To: {phone}\nFrom: {_options.SenderId}\n\n{text}\n", Encoding.UTF8, ct);
            logger.LogInformation("SMS switched off: message to {Phone} saved to {File}", phone, file);
            return;
        }

        if (!string.Equals(_options.Provider, "Hubtel", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SMS provider '{_options.Provider}' is not supported. Use \"Hubtel\".");
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Sms:ClientId and Sms:ClientSecret must be set to send text messages.");

        var url = $"{_options.ApiUrl}?clientid={Uri.EscapeDataString(_options.ClientId)}&clientsecret={Uri.EscapeDataString(_options.ClientSecret)}" +
                  $"&from={Uri.EscapeDataString(_options.SenderId)}&to={Uri.EscapeDataString(phone)}&content={Uri.EscapeDataString(text)}";
        using var response = await Http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"The SMS provider answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        logger.LogInformation("SMS sent to {Phone}", phone);
    }
}
