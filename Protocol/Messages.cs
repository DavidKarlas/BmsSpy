using System.Buffers.Binary;

namespace BmsSpy.Protocol;

// Decoders for frames sent by the BMS. Offsets are into the frame payload (payload[0] is the command byte).
// See PROTOCOL.md for the full layout including fields that are not decoded here.

public sealed record LoginRequest(uint DeviceId, byte Version, IReadOnlyList<ModuleInfo> Modules, uint BmsClock)
{
    public static LoginRequest Parse(ReadOnlySpan<byte> p)
    {
        var deviceId = BinaryPrimitives.ReadUInt32LittleEndian(p[1..]);
        var version = p[5];
        var count = p[6];
        var modules = new ModuleInfo[count];
        for (var i = 0; i < count; i++)
        {
            var rec = p.Slice(7 + i * 13, 13);
            modules[i] = new ModuleInfo(BinaryPrimitives.ReadUInt32LittleEndian(rec), rec[4]);
        }
        var clock = BinaryPrimitives.ReadUInt32LittleEndian(p[(7 + count * 13)..]);
        return new LoginRequest(deviceId, version, modules, clock);
    }
}

public sealed record ModuleInfo(uint Serial, byte Index);

public enum FlowState
{
    Discharging = -1,
    Idle = 0,
    Charging = 1,
}

public sealed record PackStatus(
    uint DeviceId,
    double Voltage,
    double Current,
    double TotalAhIn,
    double TotalAhOut,
    double TotalKwhIn,
    double TotalKwhOut,
    byte Soc,
    byte Soh,
    double MaxCellVoltage,
    double MinCellVoltage,
    sbyte MaxTemperature,
    sbyte MinTemperature,
    FlowState? State,
    double RequestChargingVoltage,
    double ChargeLimitAmps,
    double RequestDischargingVoltage,
    double DischargeLimitAmps,
    bool ChargeAllowed,
    bool DischargeAllowed,
    bool ForceCharge)
{
    public const int Length = 66;

    public static PackStatus Parse(ReadOnlySpan<byte> p)
    {
        if (p.Length < Length)
            throw new FormatException($"Pack status payload too short ({p.Length} < {Length})");
        return new PackStatus(
            DeviceId: BinaryPrimitives.ReadUInt32LittleEndian(p[1..]),
            Voltage: BinaryPrimitives.ReadUInt16LittleEndian(p[9..]) / 100.0,
            Current: BinaryPrimitives.ReadInt16LittleEndian(p[11..]) / 100.0,
            TotalAhIn: BinaryPrimitives.ReadUInt32LittleEndian(p[13..]) / 100.0,
            TotalAhOut: BinaryPrimitives.ReadUInt32LittleEndian(p[17..]) / 100.0,
            TotalKwhIn: BinaryPrimitives.ReadUInt32LittleEndian(p[21..]) / 100.0,
            TotalKwhOut: BinaryPrimitives.ReadUInt32LittleEndian(p[25..]) / 100.0,
            Soc: p[29],
            Soh: p[30],
            MaxCellVoltage: BinaryPrimitives.ReadUInt16LittleEndian(p[31..]) / 1000.0,
            MinCellVoltage: BinaryPrimitives.ReadUInt16LittleEndian(p[33..]) / 1000.0,
            MaxTemperature: (sbyte)p[37],
            MinTemperature: (sbyte)p[38],
            State: p[49] switch
            {
                2 => FlowState.Idle,
                3 => FlowState.Discharging,
                4 => FlowState.Charging,
                _ => null,
            },
            RequestChargingVoltage: BinaryPrimitives.ReadUInt16LittleEndian(p[54..]) / 10.0,
            ChargeLimitAmps: BinaryPrimitives.ReadUInt16LittleEndian(p[56..]) / 10.0,
            RequestDischargingVoltage: BinaryPrimitives.ReadUInt16LittleEndian(p[58..]) / 10.0,
            DischargeLimitAmps: BinaryPrimitives.ReadUInt16LittleEndian(p[60..]) / 10.0,
            ChargeAllowed: p[62] != 0,
            DischargeAllowed: p[63] != 0,
            ForceCharge: p[64] != 0);
    }
}

public sealed record ModuleData(
    uint Serial,
    byte Index,
    double Voltage,
    double Current,
    double TotalAhIn,
    double TotalAhOut,
    double TotalKwhIn,
    double TotalKwhOut,
    byte Soc,
    byte Soh,
    FlowState? State,
    double[] CellVoltages,
    sbyte[] Temperatures)
{
    public const int RecordLength = 79;
    private const int MaxCells = 16;
    private const int MaxTemps = 3;

    /// <summary>Parses all module records of a 0x0b frame. The count varies, e.g. when a module is offline.</summary>
    public static IReadOnlyList<ModuleData> ParseAll(ReadOnlySpan<byte> p)
    {
        var count = p[9];
        if (p.Length < 10 + count * RecordLength)
            throw new FormatException($"Module data payload too short for {count} modules ({p.Length} bytes)");
        var result = new ModuleData[count];
        for (var i = 0; i < count; i++)
            result[i] = Parse(p.Slice(10 + i * RecordLength, RecordLength));
        return result;
    }

    private static ModuleData Parse(ReadOnlySpan<byte> r)
    {
        var cellCount = Math.Min((int)r[30], MaxCells);
        var cells = new double[cellCount];
        for (var i = 0; i < cellCount; i++)
            cells[i] = BinaryPrimitives.ReadUInt16LittleEndian(r[(31 + i * 2)..]) / 1000.0;

        var tempCount = Math.Min((int)r[69], MaxTemps);
        var temps = new sbyte[tempCount];
        for (var i = 0; i < tempCount; i++)
            temps[i] = (sbyte)r[72 + i];

        return new ModuleData(
            Serial: BinaryPrimitives.ReadUInt32LittleEndian(r),
            Index: r[4],
            Voltage: BinaryPrimitives.ReadUInt16LittleEndian(r[5..]) / 10.0,
            Current: BinaryPrimitives.ReadInt16LittleEndian(r[7..]) / 100.0,
            TotalAhIn: BinaryPrimitives.ReadUInt32LittleEndian(r[9..]) / 100.0,
            TotalAhOut: BinaryPrimitives.ReadUInt32LittleEndian(r[13..]) / 100.0,
            TotalKwhIn: BinaryPrimitives.ReadUInt32LittleEndian(r[17..]) / 100.0,
            TotalKwhOut: BinaryPrimitives.ReadUInt32LittleEndian(r[21..]) / 100.0,
            Soc: r[25],
            Soh: r[26],
            State: r[27] switch
            {
                0 => FlowState.Discharging,
                1 => FlowState.Charging,
                2 => FlowState.Idle,
                _ => null,
            },
            CellVoltages: cells,
            Temperatures: temps);
    }
}

/// <summary>Builders for the replies the manufacturer cloud used to send.</summary>
public static class Replies
{
    /// <summary>Login accepted. Carries the server's UTC unix time, which the BMS adopts as its clock.</summary>
    public static Frame LoginAccepted(uint unixTime)
    {
        var p = new byte[6];
        p[0] = 0x01;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1), unixTime);
        p[5] = 0x00; // status: 0 = OK (cloud also sent 1/2/3 when it refused the session)
        return new Frame(FrameClass.Login | FrameClass.ReplyFlag, p);
    }

    /// <summary>
    /// Reply to the once-a-minute poll. The cloud always answered with the device id and first
    /// four parameter bytes echoed back, followed by zeros (i.e. "nothing to change").
    /// </summary>
    public static Frame PollReply(ReadOnlySpan<byte> request)
    {
        var p = new byte[18];
        request[..Math.Min(9, request.Length)].CopyTo(p);
        return new Frame(FrameClass.Poll | FrameClass.ReplyFlag, p);
    }

    public static Frame PackStatusAck(uint unixTime)
    {
        var p = new byte[8];
        p[0] = 0x01;
        p[1] = 0x01;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(4), unixTime);
        return new Frame(FrameClass.Telemetry | FrameClass.ReplyFlag, p);
    }

    public static Frame ModuleDataAck() => new(FrameClass.Telemetry | FrameClass.ReplyFlag, [0x02, 0x01]);

    public static Frame ModuleStatusAck() => new(FrameClass.Telemetry | FrameClass.ReplyFlag, [0x03, 0x01]);
}
