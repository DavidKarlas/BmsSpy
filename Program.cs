using System.Net.Sockets;
using BmsSpy;

// BmsSpy is a local replacement for the battery manufacturer's cloud server.
// The BMS Wi-Fi module runs in TCP-server mode and BmsSpy connects to it. Over that connection
// BmsSpy plays the cloud's role: it answers the login, acknowledges telemetry and answers the
// periodic poll, so no internet connection is needed. Telemetry is exposed for Prometheus on
// /metrics. (Client mode is avoided because the module may then dial the cloud directly
// instead of the configured server.)

var bmsHost = Environment.GetEnvironmentVariable("BMS_HOST") ?? Environment.GetEnvironmentVariable("BMS_IP");
if (string.IsNullOrWhiteSpace(bmsHost))
    throw new Exception("Environment variable 'BMS_HOST' is not set.");
var bmsPort = int.Parse(Environment.GetEnvironmentVariable("BMS_PORT") ?? "9090");
var metricsPort = int.Parse(Environment.GetEnvironmentVariable("METRICS_PORT") ?? "8080");
var idleTimeout = TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("IDLE_TIMEOUT_SECONDS") ?? "120"));
var verbose = Environment.GetEnvironmentVariable("VERBOSE") is "1" or "true";
var bmsEndpoint = $"{bmsHost}:{bmsPort}";

var cts = new CancellationTokenSource(); // not disposed: ProcessExit may still cancel it after Main returns
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

// The BMS sends a full update every ~10 s; after a minute without one, stop exporting stale values.
var metrics = new BmsMetrics(staleAfter: TimeSpan.FromSeconds(60));
var http = new HttpEndpoint(metricsPort, metrics).RunAsync(cts.Token);

try
{
    while (!cts.IsCancellationRequested)
    {
        using (var client = await ConnectAsync(cts.Token))
        {
            if (client is not null)
                await RunSessionAsync(client, cts.Token);
        }
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
    }
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
}
await http;

async Task<TcpClient?> ConnectAsync(CancellationToken ct)
{
    var client = new TcpClient();
    try
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(bmsHost, bmsPort, connectTimeout.Token);
        return client;
    }
    catch (Exception e) when (!ct.IsCancellationRequested)
    {
        Log($"cannot connect to BMS at {bmsEndpoint}: {e.Message}");
        client.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(8), ct);
        return null;
    }
}

async Task RunSessionAsync(TcpClient client, CancellationToken ct)
{
    try
    {
        client.NoDelay = true;
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        await using var stream = client.GetStream();
        await new BmsSession(stream, bmsEndpoint, metrics, idleTimeout, verbose).RunAsync(ct);
    }
    catch (Exception e) when (!ct.IsCancellationRequested)
    {
        Log($"[{bmsEndpoint}] connection error: {e.GetBaseException().Message}");
    }
}

static void Log(string message) => Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
