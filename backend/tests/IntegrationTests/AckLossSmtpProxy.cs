using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HawalekNow.IntegrationTests;

// Real SMTP relay to Mailpit: the first DATA succeeds at the provider, then its ACK is lost.
public sealed class AckLossSmtpProxy : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task run;
    private int accepted;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public AckLossSmtpProxy() { listener.Start(); run = RunAsync(); }

    private async Task RunAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var incoming = await listener.AcceptTcpClientAsync(stop.Token);
                using var upstream = new TcpClient();
                await upstream.ConnectAsync(IPAddress.Loopback, MailpitProbe.SmtpPort, stop.Token);
                using var fromClient = new StreamReader(incoming.GetStream(), Encoding.ASCII, false, leaveOpen: true);
                using var toClient = new StreamWriter(incoming.GetStream(), Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
                using var fromProvider = new StreamReader(upstream.GetStream(), Encoding.ASCII, false, leaveOpen: true);
                using var toProvider = new StreamWriter(upstream.GetStream(), Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
                await ResponseAsync(fromProvider, toClient);
                while (await fromClient.ReadLineAsync(stop.Token) is { } command)
                {
                    await toProvider.WriteLineAsync(command.AsMemory(), stop.Token);
                    if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponseAsync(fromProvider, toClient);
                        while (await fromClient.ReadLineAsync(stop.Token) is { } line)
                        {
                            await toProvider.WriteLineAsync(line.AsMemory(), stop.Token);
                            if (line == ".") break;
                        }
                        var ack = await fromProvider.ReadLineAsync(stop.Token);
                        if (ack is null || !ack.StartsWith("250", StringComparison.Ordinal)) throw new InvalidOperationException("Test provider did not accept DATA.");
                        if (Interlocked.Increment(ref accepted) == 1) break; // close downstream without forwarding a successful ACK
                        await toClient.WriteLineAsync(ack.AsMemory(), stop.Token);
                    }
                    else
                    {
                        await ResponseAsync(fromProvider, toClient);
                        if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
    private async Task ResponseAsync(StreamReader from, StreamWriter to)
    {
        string? line;
        do
        {
            line = await from.ReadLineAsync(stop.Token) ?? throw new InvalidOperationException("SMTP test relay disconnected.");
            await to.WriteLineAsync(line.AsMemory(), stop.Token);
        } while (line.Length > 3 && line[3] == '-');
    }
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync(); listener.Stop();
        try { await run; } finally { stop.Dispose(); }
    }
}
