#define DISABLED_DEBUGGING

using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

#if DEBUGGING
while (true)
{
    ProcessPacket([0x68, 0x02, 0x42, 0x00, 0x0a, 0x8e, 0x1f, 0x5f, 0x04, 0x16, 0x0a, 0x01, 0x00, 0x35, 0x66, 0x28, 0xff, 0xd3, 0x4b, 0x17, 0x00, 0xca, 0xa6, 0x16, 0x00, 0x7d, 0x45, 0x06, 0x00, 0x56, 0xef, 0x05, 0x00, 0x2f, 0x61, 0xd3, 0x0c, 0x9c, 0x0c, 0x01, 0x03, 0x23, 0x20, 0x01, 0x04, 0x2e, 0x00, 0x05, 0x00, 0x31, 0x00, 0x05, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x18, 0x0b, 0x58, 0x02, 0x70, 0x08, 0x58, 0x02, 0x01, 0x01, 0x00, 0x01, 0xfa, 0x3e, 0x16]);
    //ProcessPacket([0x68, 0x02, 0x96, 0x01, 0x0b, 0x8e, 0x1f, 0x5f, 0x04, 0x1f, 0x0a, 0x01, 0x00, 0x05, 0xf4, 0x20, 0x5f, 0x04, 0x01, 0x0d, 0x02, 0x38, 0xff, 0xd3, 0x4b, 0x17, 0x00, 0xca, 0xa6, 0x16, 0x00, 0xaa, 0x40, 0x01, 0x00, 0x47, 0x2f, 0x01, 0x00, 0x2f, 0x61, 0x00, 0x00, 0x00, 0x10, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd3, 0x0c, 0xd2, 0x0c, 0xd3, 0x0c, 0xd3, 0x0c, 0xd3, 0x0c, 0xd3, 0x0c, 0xd3, 0x0c, 0xd3, 0x0c, 0xd2, 0x0c, 0xd0, 0x0c, 0xd3, 0x0c, 0xd0, 0x0c, 0x06, 0x0f, 0x03, 0x23, 0x23, 0x23, 0x23, 0x23, 0x00, 0x00, 0x00, 0x00, 0xcb, 0x20, 0x5f, 0x04, 0x02, 0x0c, 0x02, 0x2d, 0xff, 0x15, 0x4d, 0x17, 0x00, 0x81, 0x8d, 0x16, 0x00, 0x80, 0x40, 0x01, 0x00, 0xb1, 0x2d, 0x01, 0x00, 0x30, 0x61, 0x00, 0x00, 0x00, 0x10, 0xd0, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xd1, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xd1, 0x0c, 0xd0, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd0, 0x0c, 0x09, 0x00, 0x03, 0x23, 0x22, 0x22, 0x23, 0x22, 0x00, 0x01, 0x00, 0x00, 0x39, 0x21, 0x5f, 0x04, 0x03, 0x04, 0x02, 0x3c, 0xff, 0x93, 0x61, 0x17, 0x00, 0xa6, 0xc9, 0x16, 0x00, 0xfb, 0x3f, 0x01, 0x00, 0x06, 0x2f, 0x01, 0x00, 0x2f, 0x61, 0x00, 0x00, 0x00, 0x10, 0x9e, 0x0c, 0x9d, 0x0c, 0x9c, 0x0c, 0x9c, 0x0c, 0x9d, 0x0c, 0x9d, 0x0c, 0x9d, 0x0c, 0x9d, 0x0c, 0x9d, 0x0c, 0x9c, 0x0c, 0x9c, 0x0c, 0x9d, 0x0c, 0x9c, 0x0c, 0x9c, 0x0c, 0x9d, 0x0c, 0x9d, 0x0c, 0x9e, 0x0c, 0x9c, 0x0c, 0x00, 0x02, 0x03, 0x23, 0x22, 0x23, 0x23, 0x22, 0x00, 0x01, 0x00, 0x00, 0xc1, 0x20, 0x5f, 0x04, 0x04, 0x0d, 0x02, 0x2b, 0xff, 0x73, 0x61, 0x17, 0x00, 0x66, 0xe9, 0x16, 0x00, 0x19, 0x42, 0x01, 0x00, 0x7a, 0x32, 0x01, 0x00, 0x30, 0x61, 0x00, 0x00, 0x00, 0x10, 0xd0, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd2, 0x0c, 0xd1, 0x0c, 0xd2, 0x0c, 0xd0, 0x0c, 0x05, 0x00, 0x03, 0x21, 0x20, 0x21, 0x21, 0x20, 0x00, 0x01, 0x00, 0x00, 0x8e, 0x1f, 0x5f, 0x04, 0x05, 0x0c, 0x02, 0x3a, 0xff, 0xae, 0x6d, 0x17, 0x00, 0x8a, 0xcd, 0x16, 0x00, 0x3d, 0x42, 0x01, 0x00, 0xdd, 0x30, 0x01, 0x00, 0x2f, 0x61, 0x00, 0x00, 0x00, 0x10, 0xcf, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xcf, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xcf, 0x0c, 0xd1, 0x0c, 0xd0, 0x0c, 0xd0, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd1, 0x0c, 0xd0, 0x0c, 0xd1, 0x0c, 0xcf, 0x0c, 0x09, 0x00, 0x03, 0x21, 0x21, 0x22, 0x22, 0x21, 0x02, 0x00, 0x25, 0x25, 0x00, 0x14, 0x9a, 0x16]);
    Thread.Sleep(1000);
}
#else
var influxHttpClient = new HttpClient();
var influxUrl = Environment.GetEnvironmentVariable("INFLUX_URL") ?? "http://192.168.88.2:8428/influx/api/v2/write";
var influxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
if (!string.IsNullOrWhiteSpace(influxToken))
{
    influxHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", influxToken);
}
var bmsIp = Environment.GetEnvironmentVariable("BMS_IP");
if (string.IsNullOrWhiteSpace(bmsIp))
{
    throw new Exception("Environment variable 'BMS_IP' is not set.");
}

while (true)
{
    try
    {
        var tcpClient = new TcpClient();
        tcpClient.Connect(IPAddress.Parse(bmsIp), 9090);

        Console.WriteLine("Connected to a client!");
        Console.WriteLine($"Client: {tcpClient.Client.RemoteEndPoint}");
        var stream = tcpClient.GetStream();
        var buffer = new byte[4 * 1024];
        int bytesRead;

        var connectWithServer = new TcpClient();
        connectWithServer.Connect(IPAddress.Parse("47.88.9.64"), 9090);
        Console.WriteLine("Connected to backed server!");

        var upstreamStream = connectWithServer.GetStream();

        var failure = false;
        stream.ReadTimeout = 2 * 60 * 1000; // 2 minutes
        upstreamStream.ReadTimeout = 2 * 60 * 1000; // 2 minutes

        var serverToClientTask = Task.Run(() => {
            var srvBuf = new byte[4 * 1024];
            int srvRead;
            try
            {
                while ((srvRead = upstreamStream.Read(srvBuf, 0, srvBuf.Length)) != 0)
                {
                    // Upstream server -> Client
                    stream.Write(srvBuf, 0, srvRead);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in Upstream -> Client loop: {e}");
                failure = true;
            }
        });

        try
        {
            // Client -> Upstream server
            while (!failure && (bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                ProcessPacket(buffer.Take(bytesRead).ToArray());
                upstreamStream.Write(buffer, 0, bytesRead);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in Client -> Upstream loop: {e}");
        }
        finally
        {
            try { upstreamStream.Close(); } catch { }
            try { stream.Close(); } catch { }
            try { connectWithServer.Close(); } catch { }
            try { tcpClient.Close(); } catch { }
            try { serverToClientTask.Wait(1000); } catch { }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in main loop: {ex}");
    }
}
#endif
void ProcessPacket(byte[] packetBytes)
{
    var data = packetBytes.Skip(4).SkipLast(1).ToArray();
    switch (packetBytes[4])
    {
        case 0xa:
            PrintPackData(data);
            break;
        case 0xb:
            for (int moduleIndex = 0; moduleIndex < 5; moduleIndex++)
            {
                var start = moduleIndex * 79 + 11;
                PrintModuleData(data.Skip(start).Take(79).ToArray());
            }
            break;
    }
}

void PrintPackData(byte[] data)
{
#if DEBUGGING
            Console.Clear();
            Console.WriteLine($"Request charging voltage: {BitConverter.ToUInt16(data, 54) / 10.0}V");
            Console.WriteLine($"Charge limit: {BitConverter.ToUInt16(data, 56) / 10.0}A");
            Console.WriteLine($"Request discharging voltage: {BitConverter.ToUInt16(data, 58) / 10.0}V");
            Console.WriteLine($"Discharge limit: {BitConverter.ToUInt16(data, 60) / 10.0}A");
#else
    try
    {
        double requestChargingVoltage = BitConverter.ToUInt16(data, 54) / 10.0;
        double chargeLimitAmps = BitConverter.ToUInt16(data, 56) / 10.0;
        double requestDischargingVoltage = BitConverter.ToUInt16(data, 58) / 10.0;
        double dischargeLimitAmps = BitConverter.ToUInt16(data, 60) / 10.0;

        var sb = new StringBuilder();
        sb.Append("bms ");
        sb.Append("request_charging_voltage=").Append(requestChargingVoltage.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",charge_limit_amps=").Append(chargeLimitAmps.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",request_discharging_voltage=").Append(requestDischargingVoltage.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",discharge_limit_amps=").Append(dischargeLimitAmps.ToString("0.###", CultureInfo.InvariantCulture));

        var line = sb.ToString();
        var content = new StringContent(line, Encoding.UTF8, "text/plain");
        _ = influxHttpClient.PostAsync(influxUrl, content).ContinueWith(_ => {
            var __ = _.Exception;
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
    catch
    {
        // Ignore parsing/post errors to avoid impacting proxy loop
    }
#endif
}

void PrintModuleData(byte[] data)
{
#if DEBUGGING
    Console.Clear();

    Console.WriteLine($"Id:{data[3]}");
    Console.WriteLine($"Voltage: {BitConverter.ToUInt16(data, 4) / 10.0}");
    Console.WriteLine($"Amps: {BitConverter.ToInt16(data, 6) / 100.0} Total in(Ah): {BitConverter.ToUInt32(data, 8) / 100.0} Total out(Ah): {BitConverter.ToUInt32(data, 12) / 100.0} Total Power(kWh): {BitConverter.ToUInt32(data, 16) / 100.0} Total Output Power(kWh): {BitConverter.ToUInt32(data, 20) / 100.0}");
    Console.Write($"Number of cells: {data[29]} Min: {BitConverter.ToUInt16(data, 64) / 1000.0} Max: {BitConverter.ToUInt16(data, 62) / 1000.0} ");
    Console.Write("Cells: ");
    for (var i = 30; i < 62; i += 2)
    {
        Console.Write(BitConverter.ToUInt16(data, i) / 1000.0 + ", ");
    }
    Console.WriteLine();
    Console.WriteLine($"Max volt cell id: {data[66]} Min volt cell id: {data[67]}");
    Console.WriteLine($"All have 3: {data[68]}");
    Console.WriteLine($"Max temp: {data[69]} Min temp: {data[70]} Temp 1: {data[71]} Temp 2: {data[72]} Temp 3: {data[73]}");
#else
    // Build and push Influx Line Protocol
    try
    {
        // Parse values
        int moduleId = data[3];
        double voltage = BitConverter.ToUInt16(data, 4) / 10.0;
        double amps = BitConverter.ToInt16(data, 6) / 100.0;
        double totalAhIn = BitConverter.ToUInt32(data, 8) / 100.0;
        double totalAhOut = BitConverter.ToUInt32(data, 12) / 100.0;
        double totalKwhIn = BitConverter.ToUInt32(data, 16) / 100.0;
        double totalKwhOut = BitConverter.ToUInt32(data, 20) / 100.0;
        double totalWhIn = totalKwhIn * 1000.0;
        double totalWhOut = totalKwhOut * 1000.0;

        int cellCount = data[29];

        var sb = new StringBuilder();

        // bms (summary) line with module_id tag
        sb.Append("bms,module_id=").Append(moduleId.ToString(CultureInfo.InvariantCulture));
        sb.Append(' ');
        sb.Append("voltage=").Append(voltage.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",amps=").Append(amps.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",total_ah_in=").Append(totalAhIn.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(",total_ah_out=").Append(totalAhOut.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(",total_wh_in=").Append(totalWhIn.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(",total_wh_out=").Append(totalWhOut.ToString("0.##", CultureInfo.InvariantCulture));

        sb.Append('\n');

        for (int i = 0; i < cellCount; i++)
        {
            int off = 30 + (i * 2);
            if (off + 1 >= data.Length) break;
            double cellV = BitConverter.ToUInt16(data, off) / 1000.0;

            sb.Append("bms,module_id=")
              .Append(moduleId.ToString(CultureInfo.InvariantCulture))
              .Append(",cell_id=")
              .Append((i + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append("cell_voltage=").Append(cellV.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }

        // One line per temperature sensor with module_id and sensor_id tags (sensor_temperature measurement)
        int[] temps = [(data[71]), (data[72]), (data[73])];
        for (int s = 0; s < temps.Length; s++)
        {
            sb.Append("bms,module_id=")
              .Append(moduleId.ToString(CultureInfo.InvariantCulture))
              .Append(",sensor_id=")
              .Append((s + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append("sensor_temperature=").Append(temps[s].ToString(CultureInfo.InvariantCulture));

            if (s < temps.Length - 1)
                sb.Append('\n');
        }

        var line = sb.ToString();
        var content = new StringContent(line, Encoding.UTF8, "text/plain");
        _ = influxHttpClient.PostAsync(influxUrl, content).ContinueWith(_ => {
            // Swallow exceptions to avoid impacting proxy loop
            var __ = _.Exception;
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
    catch
    {
        // Ignore any parsing/posting errors to avoid impacting proxy
    }
#endif
}