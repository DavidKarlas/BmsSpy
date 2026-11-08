using System.Net;
using System.Text;

namespace BmsSpy;

/// <summary>Serves <c>/metrics</c> (Prometheus text format) and <c>/healthz</c>.</summary>
public sealed class HttpEndpoint(int port, BmsMetrics metrics)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();
        await using var stopOnCancel = ct.Register(listener.Stop);
        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} serving /metrics and /healthz on port {port}");

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                return;
            }
            _ = Task.Run(() => Handle(context), ct);
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            switch (context.Request.Url?.AbsolutePath)
            {
                case "/metrics":
                    Write(response, 200, "text/plain; version=0.0.4; charset=utf-8", metrics.Render());
                    break;
                // Health means "the process works". It deliberately ignores whether the BMS is
                // reachable: a BMS outage must not turn into restarts of this pod.
                case "/healthz":
                    Write(response, 200, "text/plain; charset=utf-8", "ok\n");
                    break;
                default:
                    Write(response, 404, "text/plain; charset=utf-8", "not found\n");
                    break;
            }
        }
        catch (Exception)
        {
            // Client went away mid-response; nothing to do.
        }
        finally
        {
            response.Close();
        }
    }

    private static void Write(HttpListenerResponse response, int status, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
    }
}
