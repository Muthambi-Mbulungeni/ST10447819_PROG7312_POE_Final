# Smart-X Mesh Ecosystem — POE Part 2

**Student:** ST10447819
**Module:** PROG7312 — 2026
**YouTube Demo:** *(add link before submission)*
**GitHub:** https://github.com/EMGPRS/prog7312-2026-prog7312-part-1-muthambi-mbulungeni-main

---

## Overview

The Smart-X Mesh Ecosystem is a full-stack IoT telemetry platform consisting of:

| Layer | Technology | Purpose |
|---|---|---|
| **Backend API** | ASP.NET Core 8 Minimal API | Telemetry ingestion, command processing, predictions |
| **Desktop Client** | Avalonia UI (C#) | Real-time dashboard with live charts and command stream |

---

## Quick Start

```bash
chmod +x start.sh
./start.sh
```

This launches both the API and the Avalonia desktop simultaneously. Swagger UI is available at `http://localhost:5050/swagger`.

> **Prerequisites:** .NET 8 SDK installed and on your PATH.

---

## Part 1 Features — Sensor Data Ingestion & Telemetry

| Feature | Implementation |
|---|---|
| Generic class `TelemetryPacket<T>` | `where T : struct` — avoids boxing overhead |
| Operator overloading `SensorReading` | `+`, `-`, `>`, `<`, `>=`, `<=` operators |
| Jagged array `BatchBuffer` | `float[]?[]` staging pipeline for bursts |
| Method overloading `TelemetryStore.Ingest()` | Single-value vs batch overloads |
| Recursive `DeploymentValidator.Walk()` | Depth-first tree validation |
| Thread-safe `IngestionQueue<T>` | `LinkedList<T>` + `lock` |
| Live Avalonia heatmap | Auto-refreshing sensor cards with `MiniGraph` |

**Test:** Open Swagger → `GET /api/telemetry/heatmap` to see all 50 seeded devices.

---

## Part 2 Features — Real-Time Command Stream & History

Navigate to **"⚡ Command Stream"** in the desktop nav bar.

### Data Structures Implemented

#### Queue\<T\> — Standard FIFO Telemetry Ingestion
- **Class:** `TelemetryProcessingQueue` in `SmartX.Api/Domain/TelemetryProcessingQueue.cs`
- **Endpoint:** `GET /api/commands/stream` → `StandardCount` shows how many packets are queued
- **Test:** Watch `StandardQueueDepth` counter in the desktop header increment every second

#### PriorityQueue\<T, int\> — Critical Alert Bypass
- **Class:** `TelemetryProcessingQueue` (same file, `_priorityQueue` field)
- When a sensor spikes >40% above baseline the packet gets priority 0 (CRITICAL)
- **Test:** Watch the red "CRITICAL" counter in the header. Critical packets appear at the top of the feed regardless of when they arrived.
- **Simulate manually:**
  ```http
  POST http://localhost:5050/api/commands/send
  Content-Type: application/json
  { "DeviceId": "AA:BB:CC:DD:00:00", "CommandType": "TriggerActuator", "Payload": "PUMP_A_ON" }
  ```

#### Stack\<T\> — Command History & Undo
- **Class:** `CommandStack` in `SmartX.Api/Domain/CommandStack.cs`
- Every command pushed via `POST /api/commands/send` is pushed onto the stack
- `POST /api/commands/undo` pops the last command (LIFO — Undo Last)
- **Test in Swagger:**
  1. `POST /api/commands/send` with any payload → note the command ID
  2. `GET /api/commands/history` → see it at position 0 (top of stack)
  3. `POST /api/commands/undo` → it is removed; response shows the undone record
- **Test in desktop:** Send a command → click "↩ Undo Last" → command disappears from history

#### Dictionary\<TKey, TValue\> — O(1) Device Lookup
- **Class:** `DeviceDirectory` in `SmartX.Api/Domain/DeviceDirectory.cs`
- Keyed by MAC address — no linear scan needed regardless of fleet size
- **Endpoint:** `GET /api/devices?category=PowerConsumption`
- **Test:** Open Swagger → `GET /api/devices` → all devices returned instantly from Dictionary

#### SortedDictionary\<DateTime, List\<ReadingEntry\>\> — Chronological Timeline
- **Class:** `DeviceDirectory` (same file, `_timeline` field)
- Inserts are O(log n), reads are pre-sorted — no explicit sort ever needed
- **Endpoint:** `GET /api/devices/timeline`
- **Test:** `GET /api/devices/timeline?n=50` → readings returned in ascending timestamp order

#### HashSet\<T\> — Unique Active Alerts (No Duplicates)
- **Class:** `AlertTracker` in `SmartX.Api/Domain/AlertTracker.cs`
- `HashSet.Add()` returns `false` for duplicates — same sensor can never produce two identical alerts
- **Endpoint:** `GET /api/alerts/active`
- **Test:** Run for 30 seconds, then check `GET /api/alerts/active` — each sensor ID appears at most **once** even though the generator fires hundreds of packets per device

#### Predictive Action & Recommendation Engine
- **Class:** `PredictionEngine` in `SmartX.Api/Services/PredictionEngine.cs`
- Tracks search frequency, anomaly-command correlations, and command burst detection
- **Endpoint:** `GET /api/predictions`
- **Test the pattern analysis:**
  1. Filter by `PowerConsumption` in the desktop search bar 3+ times
  2. Issue a command to a `PowerConsumption` device
  3. Check `GET /api/predictions` → you'll see a "High Monitoring Frequency" insight and a pattern-match recommendation

---

## Testing All Part 2 Features via Swagger

1. Start the system: `./start.sh`
2. Open `http://localhost:5050/swagger`
3. All Part 2 endpoints are tagged **"Part 2 –"** in Swagger UI

### Simulate a High-Priority Critical Alert
```bash
# The generator automatically creates critical packets (2% spike probability)
# Or force one immediately by posting a large ingest value:
curl -X POST http://localhost:5050/api/telemetry/ingest \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"AA:BB:CC:DD:00:00","value":9999.0,"unit":"W"}'
```
Then `GET /api/commands/stream` — the packet appears under `Critical[]`.

### Test the Undo Stack
```bash
# Send 3 commands
curl -X POST http://localhost:5050/api/commands/send \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"AA:BB:CC:DD:00:00","commandType":"ShutdownDevice","payload":"all-valves"}'

# Undo the last one
curl -X POST http://localhost:5050/api/commands/undo
```

---

## Project Structure

```
SmartX.Api/
  Domain/
    TelemetryPacket.cs         ← Generic<T>  (Part 1)
    SensorReading.cs           ← Operator overloading (Part 1)
    BatchBuffer.cs             ← Jagged array (Part 1)
    IngestionQueue.cs          ← LinkedList<T> (Part 1)
    CommandStack.cs            ← Stack<T>  ★ Part 2
    TelemetryProcessingQueue.cs← Queue<T> + PriorityQueue<T,int>  ★ Part 2
    AlertTracker.cs            ← HashSet<T>  ★ Part 2
    DeviceDirectory.cs         ← Dictionary + SortedDictionary  ★ Part 2
    CommandRecord.cs           ← Command record type  ★ Part 2
    IncomingPacket.cs          ← Packet type  ★ Part 2
  Services/
    TelemetryStore.cs          ← History management (Part 1)
    SensorRegistry.cs          ← Sensor registry (Part 1)
    DeploymentValidator.cs     ← Recursive validator (Part 1)
    CommandService.cs          ← Orchestrates all Part 2 structures  ★ Part 2
    PredictionEngine.cs        ← Pattern analysis engine  ★ Part 2

SmartX.Desktop/
  ViewModels/
    MainViewModel.cs           ← Navigation (updated Part 2)
    HeatmapViewModel.cs        ← Telemetry dashboard (Part 1)
    CommandStreamViewModel.cs  ← Command stream (Part 2)  ★ Part 2
  Views/
    MainWindow.axaml           ← 4-tab nav (updated Part 2)
    HeatmapView.axaml          ← Sensor heatmap (Part 1)
    CommandStreamView.axaml    ← Full Part 2 UI  ★ Part 2
```

---

## GitHub Commit Guidelines

Aim for **25+ commits** with meaningful messages, e.g.:

```
feat: add Stack<CommandRecord> for LIFO command undo history
feat: implement PriorityQueue for critical alert bypass
feat: add HashSet<string> AlertTracker for unique alert deduplication
feat: implement SortedDictionary timeline in DeviceDirectory
feat: wire PredictionEngine with anomaly-command pattern analysis
fix: call FlushBuffer in TelemetryGeneratorService to prevent data staging bug
feat: add CommandStreamView with three-panel layout
feat: update MainWindow navigation with 4-module tabs
```
