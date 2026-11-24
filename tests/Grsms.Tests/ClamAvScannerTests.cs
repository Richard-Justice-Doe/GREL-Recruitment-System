using System.Net;
using System.Net.Sockets;
using System.Text;
using Grsms.Application.Common.Interfaces;
using Grsms.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Grsms.Tests;

/// <summary>
/// 22 Nov 2026 (docs/41, H3): the ClamAV scanner speaks clamd's INSTREAM protocol, refuses infected files with the signature
/// name, and FAILS CLOSED (refuses the file) when clamd cannot be reached or answers something unexpected. A small fake clamd on
/// a local port checks the exact bytes sent.
/// </summary>
public class ClamAvScannerTests
{
    /// <summary>A one-connection fake clamd: records the file it receives and answers <paramref name="reply"/>.</summary>
    private static (int Port, Task<byte[]> Received) FakeClamd(string reply)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var s = client.GetStream();
            var command = new byte[10];
            await s.ReadExactlyAsync(command);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            var file = new MemoryStream();
            while (true)
            {
                var len = new byte[4];
                await s.ReadExactlyAsync(len);
                var n = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(len));
                if (n == 0) break;
                var chunk = new byte[n];
                await s.ReadExactlyAsync(chunk);
                file.Write(chunk);
            }
            await s.WriteAsync(Encoding.ASCII.GetBytes(reply + "\0"));
            listener.Stop();
            return file.ToArray();
        });
        return (port, received);
    }

    private static ClamAvScanner Scanner(int port) =>
        new(Options.Create(new AntivirusOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 10 }), NullLogger<ClamAvScanner>.Instance);

    [Fact]
    public async Task A_clean_file_passes_and_is_sent_whole_in_chunks()
    {
        var content = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray(); // larger than one 64 KB chunk
        var (port, received) = FakeClamd("stream: OK");
        var result = await Scanner(port).ScanAsync(new MemoryStream(content), "cv.pdf");
        Assert.Equal(VirusScanResult.Passed, result);
        Assert.Equal(content, await received);
    }

    [Fact]
    public async Task An_infected_file_is_refused_with_the_signature_name()
    {
        var (port, _) = FakeClamd("stream: Win.Test.EICAR_HDB-1 FOUND");
        var result = await Scanner(port).ScanAsync(new MemoryStream("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR"u8.ToArray()), "eicar.pdf");
        Assert.False(result.Clean);
        Assert.False(result.Unavailable);
        Assert.Equal("Win.Test.EICAR_HDB-1", result.Threat);
    }

    [Fact]
    public async Task No_scanner_or_an_error_answer_refuses_the_file()
    {
        // Nothing listening on the port: refused, not let through.
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var closedPort = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();
        Assert.Equal(VirusScanResult.NotAvailable, await Scanner(closedPort).ScanAsync(new MemoryStream([1, 2, 3]), "cv.pdf"));

        var (port, _) = FakeClamd("INSTREAM size limit exceeded. ERROR");
        Assert.Equal(VirusScanResult.NotAvailable, await Scanner(port).ScanAsync(new MemoryStream([1, 2, 3]), "big.pdf"));
    }
}
