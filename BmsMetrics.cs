using System.Globalization;
using System.Text;
using BmsSpy.Protocol;

namespace BmsSpy;

/// <summary>
/// Holds the latest state reported by the BMS and renders it in the Prometheus text format at scrape
/// time (the usual pattern for exporters). Telemetry older than <c>staleAfter</c> is left out, so a
/// lost connection shows up as a gap in graphs instead of the last value repeated forever.
/// Lifetime charge/energy totals are exposed as counters with the BMS's own absolute values.
/// </summary>
public sealed class BmsMetrics(TimeSpan staleAfter)
{
    private readonly Lock _lock = new();
    private readonly SortedDictionary<string, long> _frames = new(StringComparer.Ordinal);
    private bool _connected;
    private long _connections;
    private DateTimeOffset? _lastFrame;
    private LoginRequest? _login;
    private PackStatus? _pack;
    private DateTimeOffset _packAt;
    private IReadOnlyList<ModuleData>? _modules;
    private DateTimeOffset _modulesAt;

    public void SessionStarted()
    {
        lock (_lock)
        {
            _connected = true;
            _connections++;
        }
    }

    public void SessionEnded()
    {
        lock (_lock)
            _connected = false;
    }

    public void FrameReceived(string type)
    {
        lock (_lock)
        {
            _frames[type] = _frames.GetValueOrDefault(type) + 1;
            _lastFrame = DateTimeOffset.UtcNow;
        }
    }

    public void SetLogin(LoginRequest login)
    {
        lock (_lock)
            _login = login;
    }

    public void SetPack(PackStatus pack)
    {
        lock (_lock)
        {
            _pack = pack;
            _packAt = DateTimeOffset.UtcNow;
        }
    }

    public void SetModules(IReadOnlyList<ModuleData> modules)
    {
        lock (_lock)
        {
            _modules = modules;
            _modulesAt = DateTimeOffset.UtcNow;
        }
    }

    public string Render()
    {
        var w = new Writer();
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;

            w.Family("bms_connected", "gauge", "1 while a TCP session with the BMS is open.");
            w.Sample("bms_connected", _connected ? 1 : 0);

            w.Family("bms_connections_total", "counter", "TCP sessions opened to the BMS.");
            w.Sample("bms_connections_total", _connections);

            w.Family("bms_frames_received_total", "counter", "Valid frames received from the BMS, by type.");
            foreach (var (type, count) in _frames)
                w.Sample("bms_frames_received_total", count, ("type", type));

            if (_lastFrame is { } last)
            {
                w.Family("bms_last_frame_timestamp_seconds", "gauge", "Unix time of the last valid frame from the BMS.");
                w.Sample("bms_last_frame_timestamp_seconds", last.ToUnixTimeMilliseconds() / 1000.0);
            }

            if (_login is { } login)
            {
                w.Family("bms_info", "gauge", "BMS identity from its last login.");
                w.Sample("bms_info", 1, ("device_id", I(login.DeviceId)), ("protocol_version", I(login.Version)));
            }

            if (_pack is { } pack && now - _packAt <= staleAfter)
                RenderPack(w, pack);

            if (_modules is { } modules && now - _modulesAt <= staleAfter)
                RenderModules(w, modules);
        }
        return w.ToString();
    }

    private static void RenderPack(Writer w, PackStatus p)
    {
        w.Gauge("bms_pack_voltage_volts", "Pack voltage.", p.Voltage);
        w.Gauge("bms_pack_current_amperes", "Pack current, positive while charging.", p.Current);
        w.Gauge("bms_pack_power_watts", "Pack voltage times current, positive while charging.", Math.Round(p.Voltage * p.Current, 2));
        w.Gauge("bms_pack_state_of_charge_ratio", "Pack state of charge (0-1).", p.Soc / 100.0);
        w.Gauge("bms_pack_state_of_health_ratio", "Pack state of health (0-1).", p.Soh / 100.0);
        w.Gauge("bms_pack_cell_voltage_max_volts", "Highest cell voltage in the pack.", p.MaxCellVoltage);
        w.Gauge("bms_pack_cell_voltage_min_volts", "Lowest cell voltage in the pack.", p.MinCellVoltage);
        w.Gauge("bms_pack_temperature_max_celsius", "Highest temperature in the pack.", p.MaxTemperature);
        w.Gauge("bms_pack_temperature_min_celsius", "Lowest temperature in the pack.", p.MinTemperature);
        if (p.State is { } state)
            w.Gauge("bms_pack_state", "1 charging, 0 idle, -1 discharging.", (int)state);
        w.Gauge("bms_pack_charge_voltage_volts", "Charge voltage the BMS requests from the inverter.", p.RequestChargingVoltage);
        w.Gauge("bms_pack_charge_current_limit_amperes", "Charge current limit the BMS requests from the inverter.", p.ChargeLimitAmps);
        w.Gauge("bms_pack_discharge_voltage_volts", "Discharge cut-off voltage the BMS requests from the inverter.", p.RequestDischargingVoltage);
        w.Gauge("bms_pack_discharge_current_limit_amperes", "Discharge current limit the BMS requests from the inverter.", p.DischargeLimitAmps);
        w.Gauge("bms_pack_charge_allowed", "1 if the BMS allows charging.", p.ChargeAllowed ? 1 : 0);
        w.Gauge("bms_pack_discharge_allowed", "1 if the BMS allows discharging.", p.DischargeAllowed ? 1 : 0);
        w.Gauge("bms_pack_force_charge_requested", "1 if the BMS requests a forced charge (very low state of charge).", p.ForceCharge ? 1 : 0);
        w.Counter("bms_pack_charged_coulombs_total", "Lifetime charge into the pack, as reported by the BMS (1 Ah = 3600 C).", AhToCoulombs(p.TotalAhIn));
        w.Counter("bms_pack_discharged_coulombs_total", "Lifetime charge out of the pack, as reported by the BMS (1 Ah = 3600 C).", AhToCoulombs(p.TotalAhOut));
        w.Counter("bms_pack_charged_joules_total", "Lifetime energy into the pack, as reported by the BMS (1 kWh = 3.6 MJ).", KwhToJoules(p.TotalKwhIn));
        w.Counter("bms_pack_discharged_joules_total", "Lifetime energy out of the pack, as reported by the BMS (1 kWh = 3.6 MJ).", KwhToJoules(p.TotalKwhOut));
    }

    private static void RenderModules(Writer w, IReadOnlyList<ModuleData> modules)
    {
        w.Gauge("bms_modules", "Number of battery modules reported by the BMS.", modules.Count);

        // Modules are labelled by position and serial: when a module drops out, the remaining ones
        // shift positions, and the serial keeps each physical module's counters separate.
        static (string, string)[] L(ModuleData m) => [("module", I(m.Index)), ("serial", I(m.Serial))];

        void PerModule(string name, string type, string help, Func<ModuleData, double?> value)
        {
            w.Family(name, type, help);
            foreach (var m in modules)
                if (value(m) is { } v)
                    w.Sample(name, v, L(m));
        }

        PerModule("bms_module_voltage_volts", "gauge", "Module voltage.", m => m.Voltage);
        PerModule("bms_module_current_amperes", "gauge", "Module current, positive while charging.", m => m.Current);
        PerModule("bms_module_state_of_charge_ratio", "gauge", "Module state of charge (0-1).", m => m.Soc / 100.0);
        PerModule("bms_module_state_of_health_ratio", "gauge", "Module state of health (0-1).", m => m.Soh / 100.0);
        PerModule("bms_module_state", "gauge", "1 charging, 0 idle, -1 discharging.", m => (int?)m.State);
        PerModule("bms_module_charged_coulombs_total", "counter", "Lifetime charge into the module, as reported by the BMS (1 Ah = 3600 C).", m => AhToCoulombs(m.TotalAhIn));
        PerModule("bms_module_discharged_coulombs_total", "counter", "Lifetime charge out of the module, as reported by the BMS (1 Ah = 3600 C).", m => AhToCoulombs(m.TotalAhOut));
        PerModule("bms_module_charged_joules_total", "counter", "Lifetime energy into the module, as reported by the BMS (1 kWh = 3.6 MJ).", m => KwhToJoules(m.TotalKwhIn));
        PerModule("bms_module_discharged_joules_total", "counter", "Lifetime energy out of the module, as reported by the BMS (1 kWh = 3.6 MJ).", m => KwhToJoules(m.TotalKwhOut));

        w.Family("bms_cell_voltage_volts", "gauge", "Cell voltage.");
        foreach (var m in modules)
            for (var c = 0; c < m.CellVoltages.Length; c++)
                w.Sample("bms_cell_voltage_volts", m.CellVoltages[c], [.. L(m), ("cell", I(c + 1))]);

        w.Family("bms_module_temperature_celsius", "gauge", "Module temperature sensor reading.");
        foreach (var m in modules)
            for (var t = 0; t < m.Temperatures.Length; t++)
                w.Sample("bms_module_temperature_celsius", m.Temperatures[t], [.. L(m), ("sensor", I(t + 1))]);
    }

    // Prometheus convention is base units. The BMS reports 0.01 Ah / 0.01 kWh steps, so both
    // conversions land on whole numbers; rounding only removes floating-point noise.
    private static double AhToCoulombs(double ah) => Math.Round(ah * 3600);
    private static double KwhToJoules(double kwh) => Math.Round(kwh * 3_600_000);

    private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();

        public void Family(string name, string type, string help) =>
            _sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n')
               .Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

        public void Sample(string name, double value, params (string Key, string Value)[] labels)
        {
            _sb.Append(name);
            if (labels.Length > 0)
            {
                _sb.Append('{');
                for (var i = 0; i < labels.Length; i++)
                {
                    if (i > 0)
                        _sb.Append(',');
                    // Label values here are numbers or fixed identifiers; nothing needs escaping.
                    _sb.Append(labels[i].Key).Append("=\"").Append(labels[i].Value).Append('"');
                }
                _sb.Append('}');
            }
            _sb.Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        public void Gauge(string name, string help, double value)
        {
            Family(name, "gauge", help);
            Sample(name, value);
        }

        public void Counter(string name, string help, double value)
        {
            Family(name, "counter", help);
            Sample(name, value);
        }

        public override string ToString() => _sb.ToString();
    }
}
