# BmsSpy

Replaces a home battery BMS's manufacturer cloud server and exports the battery's telemetry to Prometheus.

The BMS's Wi-Fi module talks to the manufacturer's server (`47.88.9.64:9090`) over a small binary TCP
protocol. That server only ever acknowledges what the BMS sends. It is also unreliable: it often refuses
logins or drops sessions. BmsSpy speaks the protocol itself, so the BMS keeps a stable session with no
internet dependency. Everything it receives is exposed on `/metrics`.

## How it connects

Set the BMS Wi-Fi module to **TCP-server mode** (port 9090). BmsSpy connects to it, takes over the
cloud's side of the conversation, and reconnects whenever the connection drops.

Don't use the module's TCP-client mode pointed at BmsSpy: the module can fall back to dialling the
manufacturer's server directly.

## Install on Kubernetes

```sh
helm install bmsspy oci://ghcr.io/davidkarlas/charts/bmsspy \
  --namespace bmsspy --create-namespace \
  --set bms.host=<address of the BMS Wi-Fi module>
```

`bms.host` is the only required value. The chart creates:

- A Deployment with exactly one pod (`Recreate` strategy, so two clients never talk to the BMS at once).
  It runs as non-root with a read-only root filesystem and passes the `restricted` Pod Security Standard.
- A Service and, if the Prometheus Operator CRDs exist, a ServiceMonitor scraping every 10 s (the BMS
  reports every ~10 s).
- A Grafana dashboard as a ConfigMap labelled `grafana_dashboard: "1"` for the Grafana sidecar
  (grafana and kube-prometheus-stack charts). The grafana chart's sidecar only watches its own namespace
  unless `sidecar.dashboards.searchNamespace` is set; otherwise set `grafanaDashboard.namespace` to
  Grafana's namespace.
- Optionally, alerting rules (`prometheusRule.enabled=true`): BMS unreachable, a module stopped
  reporting, cell imbalance in the middle of the charge range, high temperature.

See [values.yaml](charts/bmsspy/values.yaml) for all settings.

## Run without Kubernetes

```sh
docker run -d --name bmsspy -p 8080:8080 -e BMS_HOST=<address of the BMS Wi-Fi module> ghcr.io/davidkarlas/bmsspy
```

| Variable | Default | |
|---|---|---|
| `BMS_HOST` | – | **Required.** Address of the BMS Wi-Fi module (`BMS_IP` also accepted) |
| `BMS_PORT` | `9090` | |
| `METRICS_PORT` | `8080` | Serves `/metrics` and `/healthz` |
| `IDLE_TIMEOUT_SECONDS` | `120` | Reconnect after this long without frames |
| `VERBOSE` | – | `1` logs every received frame |

## Grafana dashboard

[`charts/bmsspy/dashboards/bmsspy.json`](charts/bmsspy/dashboards/bmsspy.json) shows:

- power and state of charge
- cell voltages, for all modules and for one selected module
- temperatures
- current, with the limits the BMS requests from the inverter
- energy charged and discharged, per time range and per day
- charge/discharge permissions

To install it:

- **Helm:** installed automatically (see above).
- **Any other Grafana:** download the file, then go to Dashboards → New → Import and upload it. Pick
  your Prometheus data source in the *Data source* dropdown at the top. Tested with Grafana 12.

To change the dashboard:

1. Edit it in Grafana.
2. Export it as JSON. Leave "Export for sharing externally" ("Export the dashboard to use in another
   instance" in newer versions) **off**.
3. Replace the file.

The dashboard has to keep using its `datasource` variable instead of a fixed data source, so it works
in any Grafana. CI rejects a file that embeds one.

## Metrics

Telemetry series disappear if the BMS sends nothing for 60 s, so outages show up as gaps rather than
flat lines. Units follow Prometheus conventions (ratios 0–1, coulombs, joules). Divide by 3600 to get
Ah or Wh.

| Metric | Labels | |
|---|---|---|
| `bms_connected` | | 1 while a TCP session with the BMS is open |
| `bms_connections_total` | | Sessions opened |
| `bms_frames_received_total` | `type` | Valid frames received |
| `bms_last_frame_timestamp_seconds` | | Time of the last valid frame |
| `bms_info` | `device_id`, `protocol_version` | From the BMS's login |
| `bms_pack_voltage_volts`, `bms_pack_current_amperes`, `bms_pack_power_watts` | | Current is positive while charging |
| `bms_pack_state_of_charge_ratio`, `bms_pack_state_of_health_ratio` | | |
| `bms_pack_cell_voltage_max_volts`, `bms_pack_cell_voltage_min_volts` | | |
| `bms_pack_temperature_max_celsius`, `bms_pack_temperature_min_celsius` | | |
| `bms_pack_state` | | 1 charging, 0 idle, -1 discharging |
| `bms_pack_charge_voltage_volts`, `bms_pack_charge_current_limit_amperes` | | Requested from the inverter |
| `bms_pack_discharge_voltage_volts`, `bms_pack_discharge_current_limit_amperes` | | Requested from the inverter |
| `bms_pack_charge_allowed`, `bms_pack_discharge_allowed`, `bms_pack_force_charge_requested` | | 0/1 |
| `bms_pack_{charged,discharged}_{coulombs,joules}_total` | | Lifetime totals reported by the BMS |
| `bms_modules` | | Number of modules reported |
| `bms_module_voltage_volts`, `bms_module_current_amperes` | `module`, `serial` | |
| `bms_module_state_of_charge_ratio`, `bms_module_state_of_health_ratio`, `bms_module_state` | `module`, `serial` | |
| `bms_module_{charged,discharged}_{coulombs,joules}_total` | `module`, `serial` | |
| `bms_cell_voltage_volts` | `module`, `serial`, `cell` | |
| `bms_module_temperature_celsius` | `module`, `serial`, `sensor` | |

`module` is the module's position as reported by the BMS. When a module goes offline, the remaining
modules shift positions. `serial` keeps each physical module's series apart.

## Releasing

Pushing a tag `vX.Y.Z` publishes the image `ghcr.io/davidkarlas/bmsspy:X.Y.Z` and the chart
`oci://ghcr.io/davidkarlas/charts/bmsspy` version `X.Y.Z`. Every push to `main` publishes the image
tagged `main`. New GHCR packages start out private: make both packages public once, in their
package settings on GitHub.

## Protocol

Reverse engineered from captured traffic. All integers are little-endian.

### Framing

```
0x68 | class | len (u16) | payload[len] | crc (u16) | 0x16
```

- `crc` is CRC-16/MODBUS (poly 0xA001 reflected, init 0xFFFF) over every byte from `0x68` to the end of the payload.
- `payload[0]` is the command.
- Frames from the server use the request's class with bit 7 set.
- The BMS always speaks first, whichever side opened the TCP connection. The server only ever replies.

| BMS → server | Server → BMS | When |
|---|---|---|
| class `00` cmd `01` login | class `80`: `01, time(u32), status` | On connect. Repeated every ~11 s until status `00` arrives |
| class `02` cmd `0b` module data | class `82`: `02 01` | Every 10 s |
| class `02` cmd `0a` pack status | class `82`: `01 01 00 00, time(u32)` | Every 10 s |
| class `02` cmd `0c` module status | class `82`: `03 01` | Every 10 s |
| class `01` cmd `01` poll: `01, device_id(u32), 0a 01 00 00 00 00 04` | class `81`: `01, device_id(u32), 0a 01 00 00`, 9 × `00` | Every 60 s |

`time` is the server's UTC unix time. The BMS sets its clock from it.

The cloud sometimes answered a login with status 1, 2 or 3, often two replies in a row (3 then 2). The BMS
then retried the login. BmsSpy always answers 0. The poll request was identical in every captured frame,
and the cloud's reply was always the same zero-filled frame, so its meaning is unknown. When the cloud
dropped a session that had already exchanged frames, the last frame was most often a poll it never answered.

### Login (class 00, cmd 01)

Payload length 15 + 13·N bytes for N modules.

| Offset | Size | |
|---|---|---|
| 0 | 1 | cmd `01` |
| 1 | 4 | device id (serial of the master module) |
| 5 | 1 | `0x12`, probably firmware/protocol version |
| 6 | 1 | module count N |
| 7 | 13·N | per module: serial (u32), index (u8), u32 `1`, u32 `2538` |
| 7+13N | 4 | BMS clock (unix time once synced, a small counter before that) |
| 11+13N | 4 | random nonce |

### Pack status (class 02, cmd 0a)

Payload length 66 bytes.

| Offset | Size | |
|---|---|---|
| 1 | 4 | device id |
| 5 | 4 | BMS clock (unreliable: jumps around) |
| 9 | 2 | pack voltage /100 V |
| 11 | 2 | pack current /100 A (signed, + = charging) |
| 13, 17 | 4, 4 | total Ah in / out /100 |
| 21, 25 | 4, 4 | total kWh in / out /100 |
| 29 | 1 | SoC % |
| 30 | 1 | SoH % |
| 31, 33 | 2, 2 | max / min cell voltage mV |
| 35, 36 | 1, 1 | module index holding max / min cell |
| 37, 38 | 1, 1 | max / min temperature °C |
| 39, 40 | 1, 1 | module index holding max / min temperature |
| 41..48 | 8 | unknown: u16, module count, u16, module count (the u16s increase slowly; possibly cycle counts) |
| 49 | 1 | state: 2 idle, 3 discharging, 4 charging |
| 54 | 2 | requested charge voltage /10 V |
| 56 | 2 | charge current limit /10 A |
| 58 | 2 | requested discharge (cut-off) voltage /10 V |
| 60 | 2 | discharge current limit /10 A |
| 62 | 1 | charge allowed (seen dropping to 0 near full) |
| 63 | 1 | discharge allowed (seen dropping to 0 near empty) |
| 64 | 1 | force-charge request (seen near empty) |
| 65 | 1 | unknown, always 1 |

### Module data (class 02, cmd 0b)

Header: cmd, device id (4), BMS clock (4), module count N (1). Then N records of 79 bytes, then 1 trailing byte (0/1).

| Offset in record | Size | |
|---|---|---|
| 0 | 4 | module serial |
| 4 | 1 | index (1-based) |
| 5 | 2 | voltage /10 V |
| 7 | 2 | current /100 A (signed) |
| 9, 13 | 4, 4 | total Ah in / out /100 |
| 17, 21 | 4, 4 | total kWh in / out /100 |
| 25 | 1 | SoC % |
| 26 | 1 | SoH % |
| 27 | 1 | state: 0 discharging, 1 charging, 2 idle |
| 30 | 1 | cell count |
| 31 | 32 | 16 × cell voltage mV |
| 63, 65 | 2, 2 | max / min cell mV |
| 67, 68 | 1, 1 | max / min cell number |
| 69 | 1 | temperature sensor count |
| 70, 71 | 1, 1 | max / min temperature °C |
| 72 | 3 | sensor temperatures °C |
| 75..78 | 4 | unknown: two small status bytes (0–2) and two values that look like extra temperatures |

### Module status (class 02, cmd 0c)

Header as for module data, then N records of 9 bytes: serial (4), index (1), 4 status bytes. These were
`00 02 00 00` in almost every captured frame, so they are probably alarm/protection flags.
