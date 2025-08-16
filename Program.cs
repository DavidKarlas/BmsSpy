using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

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

        var upstreamStream = connectWithServer.GetStream();

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
            catch
            {
                // Ignore exceptions during teardown/half-closes
            }
        });

        try
        {
            // Client -> Upstream server
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                ProcessPacket(buffer.Take(bytesRead).ToArray());
                upstreamStream.Write(buffer, 0, bytesRead);
            }
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
        Console.WriteLine(ex.ToString());
    }
}

void ProcessPacket(byte[] packetBytes)
{
    if (packetBytes[4] != 0xb)
        return;
    var timestamp = DateTime.UtcNow;
    Console.Clear();
    var data = packetBytes.Skip(4).SkipLast(1).ToArray();

    for (int moduleIndex = 0; moduleIndex < 5; moduleIndex++)
    {
        var start = moduleIndex * 79 + 11;
        PrintModuleData(timestamp, data.Skip(start).Take(79).ToArray());
        Console.WriteLine();
    }
}

void PrintModuleData(DateTime timestamp, byte[] arr)
{
    Console.WriteLine($"Id:{arr[3]}");
    Console.WriteLine($"Voltage: {BitConverter.ToUInt16(arr, 4) / 10.0}");
    Console.WriteLine($"Amps: {BitConverter.ToInt16(arr, 6) / 100.0} Total in(Ah): {BitConverter.ToUInt32(arr, 8) / 100.0} Total out(Ah): {BitConverter.ToUInt32(arr, 12) / 100.0} Total Power(kWh): {BitConverter.ToUInt32(arr, 16) / 100.0} Total Output Power(kWh): {BitConverter.ToUInt32(arr, 20) / 100.0}");
    Console.Write($"Number of cells: {arr[29]} Min: {BitConverter.ToUInt16(arr, 64) / 1000.0} Max: {BitConverter.ToUInt16(arr, 62) / 1000.0} ");
    Console.Write("Cells: ");
    for (var i = 30; i < 62; i += 2)
    {
        Console.Write(BitConverter.ToUInt16(arr, i) / 1000.0 + ", ");
    }
    Console.WriteLine();
    Console.WriteLine($"Max volt cell id: {arr[66]} Min volt cell id: {arr[67]}");
    Console.WriteLine($"All have 3: {arr[68]}");
    Console.WriteLine($"Max temp: {arr[69]} Min temp: {arr[70]} Temp 1: {arr[71]} Temp 2: {arr[72]} Temp 3: {arr[73]}");

    // Build and push Influx Line Protocol
    try
    {
        // Parse values
        int moduleId = arr[3];
        double voltage = BitConverter.ToUInt16(arr, 4) / 10.0;
        double amps = BitConverter.ToInt16(arr, 6) / 100.0;
        double totalAhIn = BitConverter.ToUInt32(arr, 8) / 100.0;
        double totalAhOut = BitConverter.ToUInt32(arr, 12) / 100.0;
        double totalKwhIn = BitConverter.ToUInt32(arr, 16) / 100.0;
        double totalKwhOut = BitConverter.ToUInt32(arr, 20) / 100.0;
        double totalWhIn = totalKwhIn * 1000.0;
        double totalWhOut = totalKwhOut * 1000.0;

        int cellCount = arr[29];

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
            if (off + 1 >= arr.Length) break;
            double cellV = BitConverter.ToUInt16(arr, off) / 1000.0;

            sb.Append("cell_voltage,module_id=")
              .Append(moduleId.ToString(CultureInfo.InvariantCulture))
              .Append(",cell_id=")
              .Append((i + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append("voltage=").Append(cellV.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }

        // One line per temperature sensor with module_id and sensor_id tags (sensor_temperature measurement)
        int[] temps = [(arr[71]), (arr[72]), (arr[73])];
        for (int s = 0; s < temps.Length; s++)
        {
            sb.Append("sensor_temperature,module_id=")
              .Append(moduleId.ToString(CultureInfo.InvariantCulture))
              .Append(",sensor_id=")
              .Append((s + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append("temperature=").Append(temps[s].ToString(CultureInfo.InvariantCulture));

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
}