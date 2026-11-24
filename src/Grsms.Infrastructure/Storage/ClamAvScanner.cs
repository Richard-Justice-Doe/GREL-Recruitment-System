using System.Net;
using System.Net.Sockets;
using System.Text;
using Grsms.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grsms.Infrastructure.Storage;

/// <summary>Where the ClamAV scanner (clamd) listens: "Antivirus" in appsettings / environment (Antivirus__Host).</summary>
public sealed class AntivirusOptions
{
    /// <summary>Host name of clamd (e.g. "clamav" in docker-compose.prod.yml). Empty = no scanning (development only).</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// 22 Nov 2026 (docs/41, H3): every upload is scanned by ClamAV (clamd) before it is stored, using its INSTREAM command over TCP:
/// "zINSTREAM\0", then the file in chunks (4-byte big-endian length + bytes), then a zero length; clamd answers "stream: OK" or
/// "stream: &lt;signature&gt; FOUND". FAIL CLOSED: if clamd cannot be reached, answers an error or takes too long, the file is
/// refused (<see cref="VirusScanResult.NotAvailable"/>), never let through unscanned.
/// </summary>
public sealed class ClamAvScanner(IOptions<AntivirusOptions> options, ILogger<ClamAvScanner> logger) : IVirusScanner
{
    private const int Chunk = 64 * 1024;

    public async Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var o = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, o.TimeoutSeconds)));
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(o.Host!, o.Port, timeout.Token);
            await using var net = tcp.GetStream();
            await net.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

            var buffer = new byte[Chunk];
            int read;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            {
                await net.WriteAsync(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(read)), timeout.Token);
                await net.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            await net.WriteAsync(new byte[4], timeout.Token); // zero length: end of the file
            await net.FlushAsync(timeout.Token);

            using var reply = new MemoryStream();
            await net.CopyToAsync(reply, timeout.Token);
            var answer = Encoding.ASCII.GetString(reply.ToArray()).TrimEnd('\0', '\n', '\r', ' ');
            return Interpret(answer, fileName);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The virus scanner (ClamAV at {Host}:{Port}) could not be reached; '{File}' was refused.", o.Host, o.Port, fileName);
            return VirusScanResult.NotAvailable;
        }
        finally
        {
            if (content.CanSeek) content.Position = 0;
        }
    }

    /// <summary>clamd's answer → result. Anything other than a clear OK or FOUND is treated as "not available" (fail closed).</summary>
    internal VirusScanResult Interpret(string answer, string fileName)
    {
        if (answer.EndsWith(" OK", StringComparison.Ordinal)) return VirusScanResult.Passed;
        if (answer.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var threat = answer[(answer.IndexOf(':') + 1)..^" FOUND".Length].Trim();
            logger.LogWarning("Virus found in upload '{File}': {Threat}", fileName, threat);
            return new VirusScanResult(false, threat);
        }
        logger.LogError("Unexpected answer from the virus scanner for '{File}': {Answer}", fileName, answer);
        return VirusScanResult.NotAvailable;
    }
}
