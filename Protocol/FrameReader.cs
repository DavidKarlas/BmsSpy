namespace BmsSpy.Protocol;

/// <summary>
/// Splits a TCP byte stream into frames. Frames may arrive split across reads or several per read.
/// Anything that does not form a valid frame (bad length, missing end byte, CRC mismatch) is skipped
/// one byte at a time until the parser re-synchronises on the next valid frame.
/// </summary>
public sealed class FrameReader(Stream stream)
{
    // Largest frame seen is 413 bytes (5 modules); leave plenty of headroom for bigger packs.
    private const int MaxPayload = 4096;

    private byte[] _buffer = new byte[8192];
    private int _start;
    private int _end;

    /// <summary>Number of bytes dropped while hunting for a valid frame.</summary>
    public long DiscardedBytes { get; private set; }

    /// <summary>Returns the next valid frame, or null when the peer closed the connection.</summary>
    public async ValueTask<Frame?> ReadAsync(CancellationToken ct)
    {
        while (true)
        {
            if (TryParse(out var frame))
                return frame;

            if (_start > 0)
            {
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }
            if (_end == _buffer.Length)
                Array.Resize(ref _buffer, _buffer.Length * 2);

            var read = await stream.ReadAsync(_buffer.AsMemory(_end), ct);
            if (read == 0)
                return null;
            _end += read;
        }
    }

    private bool TryParse(out Frame? frame)
    {
        frame = null;
        while (_end - _start >= Frame.Overhead)
        {
            var span = _buffer.AsSpan(_start, _end - _start);
            if (span[0] != Frame.StartByte)
            {
                Skip();
                continue;
            }

            var length = span[2] | span[3] << 8;
            if (length > MaxPayload)
            {
                Skip();
                continue;
            }

            var total = length + Frame.Overhead;
            if (span.Length < total)
                return false; // wait for more data

            var candidate = span[..total];
            var crc = (ushort)(candidate[^3] | candidate[^2] << 8);
            if (candidate[^1] != Frame.EndByte || Crc16Modbus.Compute(candidate[..^3]) != crc)
            {
                Skip();
                continue;
            }

            frame = new Frame(candidate[1], candidate.Slice(4, length).ToArray());
            _start += total;
            return true;
        }
        return false;
    }

    private void Skip()
    {
        _start++;
        DiscardedBytes++;
    }
}
