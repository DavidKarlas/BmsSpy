using BmsSpy.Protocol;

namespace BmsSpy;

/// <summary>
/// Plays the role of the manufacturer cloud for one TCP connection: accepts the login,
/// acknowledges every telemetry frame, answers the periodic poll and records decoded data.
/// </summary>
public sealed class BmsSession(Stream stream, string peer, BmsMetrics metrics, TimeSpan idleTimeout, bool verbose)
{
    private readonly HashSet<string> _reportedUnknown = [];
    private long _frames;

    public async Task RunAsync(CancellationToken ct)
    {
        var reader = new FrameReader(stream);
        metrics.SessionStarted();
        Log("connected");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Frame? frame;
                using (var idle = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    idle.CancelAfter(idleTimeout);
                    try
                    {
                        frame = await reader.ReadAsync(idle.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Log($"no data for {idleTimeout.TotalSeconds:0}s, reconnecting");
                        return;
                    }
                }

                if (frame is null)
                {
                    Log("closed by BMS");
                    return;
                }

                _frames++;
                var reply = Handle(frame);
                if (reply is not null)
                    await stream.WriteAsync(reply.Encode(), ct);
            }
        }
        finally
        {
            metrics.SessionEnded();
            Log($"session ended after {_frames} frames ({reader.DiscardedBytes} garbage bytes skipped)");
        }
    }

    private Frame? Handle(Frame frame)
    {
        if (verbose)
            Log($">> {frame}");

        switch (frame.Class, frame.Command)
        {
            case (FrameClass.Login, 0x01):
                metrics.FrameReceived("login");
                Decode(frame, p =>
                {
                    var login = LoginRequest.Parse(p);
                    metrics.SetLogin(login);
                    var modules = string.Join(", ", login.Modules.Select(m => $"#{m.Index}={m.Serial}"));
                    Log($"login from device {login.DeviceId} (v{login.Version}, {login.Modules.Count} modules: {modules})");
                });
                return Replies.LoginAccepted(Now());

            case (FrameClass.Poll, 0x01):
                metrics.FrameReceived("poll");
                return Replies.PollReply(frame.Payload);

            case (FrameClass.Telemetry, TelemetryCommand.PackStatus):
                metrics.FrameReceived("pack_status");
                Decode(frame, p => metrics.SetPack(PackStatus.Parse(p)));
                return Replies.PackStatusAck(Now());

            case (FrameClass.Telemetry, TelemetryCommand.ModuleData):
                metrics.FrameReceived("module_data");
                Decode(frame, p => metrics.SetModules(ModuleData.ParseAll(p)));
                return Replies.ModuleDataAck();

            case (FrameClass.Telemetry, TelemetryCommand.ModuleStatus):
                // Per-module alarm/status words; meaning not decoded yet. Just acknowledge.
                metrics.FrameReceived("module_status");
                return Replies.ModuleStatusAck();

            default:
                // Not part of the known protocol. Log once per (class, cmd) and do not reply.
                metrics.FrameReceived("unknown");
                if (_reportedUnknown.Add($"{frame.Class:x2}/{frame.Command:x2}"))
                    Log($"unknown frame, not answering: {frame}");
                return null;
        }
    }

    private void Decode(Frame frame, Action<byte[]> decode)
    {
        // Decoding must never stop us from acknowledging the frame.
        try
        {
            decode(frame.Payload);
        }
        catch (Exception e)
        {
            Log($"failed to decode {frame}: {e.Message}");
        }
    }

    private static uint Now() => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void Log(string message) => Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{peer}] {message}");
}
