namespace BmsSpy.Protocol;

/// <summary>
/// Wire frame used between the BMS and the (former) manufacturer cloud:
/// <code>0x68 | class | len (u16 LE) | payload[len] | crc16 (u16 LE) | 0x16</code>
/// The CRC is CRC-16/MODBUS over everything from the 0x68 start byte up to the end of the payload.
/// Payload byte 0 is the command. Frames from the server have bit 7 of the class set.
/// </summary>
public sealed class Frame(byte @class, byte[] payload)
{
    public const byte StartByte = 0x68;
    public const byte EndByte = 0x16;
    public const int Overhead = 7; // start + class + len(2) + crc(2) + end

    public byte Class { get; } = @class;
    public byte[] Payload { get; } = payload;
    public byte Command => Payload.Length > 0 ? Payload[0] : (byte)0;

    public byte[] Encode()
    {
        var buf = new byte[Payload.Length + Overhead];
        buf[0] = StartByte;
        buf[1] = Class;
        buf[2] = (byte)Payload.Length;
        buf[3] = (byte)(Payload.Length >> 8);
        Payload.CopyTo(buf, 4);
        var crc = Crc16Modbus.Compute(buf.AsSpan(0, 4 + Payload.Length));
        buf[^3] = (byte)crc;
        buf[^2] = (byte)(crc >> 8);
        buf[^1] = EndByte;
        return buf;
    }

    public override string ToString() => $"class=0x{Class:x2} cmd=0x{Command:x2} len={Payload.Length} {Convert.ToHexString(Payload)}";
}

public static class FrameClass
{
    /// <summary>BMS -> server: login/registration (cmd 0x01). Reply class 0x80.</summary>
    public const byte Login = 0x00;
    /// <summary>BMS -> server: periodic poll, once a minute (cmd 0x01). Reply class 0x81.</summary>
    public const byte Poll = 0x01;
    /// <summary>BMS -> server: telemetry upload (cmd 0x0a/0x0b/0x0c). Reply class 0x82.</summary>
    public const byte Telemetry = 0x02;
    public const byte ReplyFlag = 0x80;
}

public static class TelemetryCommand
{
    public const byte PackStatus = 0x0a;
    public const byte ModuleData = 0x0b;
    public const byte ModuleStatus = 0x0c;
}

public static class Crc16Modbus
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return crc;
    }
}
