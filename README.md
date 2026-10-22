# Smart-X Mesh Ecosystem — POE Part 2

**Student:** ST10447819  
**Module:** PROG7312 — 2026   
**GitHub Repository:** https://github.com/EMGPRS/prog7312-2026-prog7312-part-1-muthambi-mbulungeni-main  

---

## 1. System Overview

The **Smart-X Mesh Ecosystem** is a high-performance IoT telemetry ingestion and real-time command processing platform built with:

| Component | Technology | Architecture & Role |
|---|---|---|
| **Backend API** | ASP.NET Core 8 Minimal API | High-throughput ingestion queues, priority dispatching, undo command stack, and automated recommendation engine. |
| **Desktop Client** | Avalonia UI (Cross-platform XAML/C#) | Hardware-accelerated telemetry dashboards, live sparkline graphs, device management, and real-time command dispatching. |
| **Containerisation** | Docker & Docker Compose | Containerised API environment for automated grading and reproducible deployment. |

---

## 2. Quick Start & Execution

### Option A: Local Execution (Recommended for Desktop UI)
Ensure the .NET 8 SDK is installed on your system.

```bash
chmod +x start.sh
./start.sh
```

- **Backend API & Swagger:** http://localhost:5050/swagger  
- **Desktop Client:** Automatically launches connected to `http://localhost:5050/api`

### Option B: Docker Compose (Backend API)
Run the backend in an isolated Docker container:

```bash
docker compose up --build
```

---

## 3. Part 1 Foundations (Maintained & Integrated)

All foundational OOP and algorithmic requirements from Part 1 remain fully active:

- **Generic Constraints (`TelemetryPacket<T>`):** Constrained `where T : struct` to eliminate boxing/unboxing overhead for primitive telemetry.
- **Operator Overloading (`SensorReading`):** Implements `+`, `-`, `>`, `<`, `>=`, `<=` for structural comparisons and sensor delta aggregation.
- **Jagged Arrays (`BatchBuffer`):** Utilises `float[]?[]` as a memory-efficient staging pipeline before packets enter queues.
- **Method Overloading (`TelemetryStore.Ingest`):** Single-reading ingestion vs. high-throughput batch ingestion overloads.
- **Recursive Network Validation (`DeploymentValidator.Walk`):** Depth-first recursive tree traversal over `Facility > Zone > Sub-Zone > Device`.
- **Thread-Safe Ingestion Queue (`IngestionQueue<T>`):** Mutex-synchronised circular buffer backed by a `LinkedList<T>`.

---

## 4. Part 2 Core Requirements & Data Structures (100 Marks)

### Pillar 1: Navigation & State Preservation (30 Marks)
- Updated frontend navigation presenting all required active modules:
  1. **Sensor Data Ingestion & Telemetry** (Part 1 Live Heatmap)
  2. **Real-Time Command Stream & History** (Part 2 Command Engine)
  3. **Network Topology & Mesh Routing** (Part 3 PoE Final Preview)
  4. **Sensor Registry** (Hardware & Encrypted Firmware)
- **Seamless State Preservation:** The `MainViewModel` caches and coordinates ViewModel instances so switching between views retains search filters, selected items, active inputs, and undo history without freezing or state loss.

---

### Pillar 2: Real-Time Command Stream & Advanced Collections (40 Marks)

| Data Structure | Implementation File | Role in the Ecosystem |
|---|---|---|
| **`Queue<T>`** | `SmartX.Api/Domain/TelemetryProcessingQueue.cs` | **FIFO Telemetry Queue:** Standard First-In-First-Out message queue processing routine telemetry packets arriving from devices. |
| **`PriorityQueue<T, int>`** | `SmartX.Api/Domain/TelemetryProcessingQueue.cs` | **Critical Alert Bypass:** Urgent sensor anomalies (e.g., severe power spikes, moisture loss) are assigned priority `0` and bypass the standard queue to be processed and surfaced immediately. |
| **`Stack<T>`** | `SmartX.Api/Domain/CommandStack.cs` | **Undo Command History:** Manages operator override commands (LIFO). Enables operators to quickly revert the last issued command (e.g. undoing an accidental shutdown). |
| **`Dictionary<TKey, TValue>`** | `SmartX.Api/Domain/DeviceDirectory.cs` | **O(1) Device Lookups:** Keyed by normalized MAC address for instantaneous constant-time device retrieval and updates when telemetry packets arrive. |
| **`SortedDictionary<TKey, TValue>`** | `SmartX.Api/Domain/DeviceDirectory.cs` | **Sequential Historical Timeline:** Stores historical logs ordered strictly by timestamp (`DateTime`). Enables the frontend to render live timeline graphs efficiently without sorting overhead. |
| **`HashSet<T>`** | `SmartX.Api/Domain/AlertTracker.cs` | **Deduplicated Alert Set:** Tracks active unique error states. Because sets only store unique elements, duplicate alerts from high-frequency sensor spikes are suppressed automatically. |

---

### Pillar 3: Predictive Action & Recommendation Engine (30 Marks)

- **Class:** `SmartX.Api/Services/PredictionEngine.cs`
- **Pattern Analysis:** Tracks search history, category query frequencies, and anomaly-to-command correlations (e.g., detecting what commands operators typically trigger when power anomalies occur).
- **Automated Insights:** Automatically produces ranked recommendations on the dashboard:
  - *Pattern Matches:* Predicts and suggests commands based on previous manual overrides.
  - *Monitoring Insights:* Highlights frequently searched categories that warrant proactive threshold alerts.
  - *Unresolved Alert Remediations:* Proactively recommends target commands for active `HashSet` alerts.
  - *Burst Warnings:* Detects runaway command bursts (>3 commands within 30 seconds) to prevent equipment damage.

---

## 5. Verification & Testing Instructions

### A. Simulating a High-Priority Critical Alert (PriorityQueue Bypass)

#### Via the Avalonia Desktop UI:
1. Open the **Command Stream** tab.
2. Click the red **"⚡ Simulate Spike"** button in the header bar.
3. Observe:
   - The **PRIORITY QUEUE** counter increments immediately.
   - The packet appears at the top of the **PriorityQueue (Critical Alerts)** feed in red.
   - The alert is tracked in the **HashSet** without duplicate warnings.
   - The **Automated Insights** panel suggests immediate remediation commands.

#### Via cURL / Terminal:
```bash
curl -X POST http://localhost:5050/api/commands/simulate-alert \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"AA:BB:CC:DD:00:00","value":4800.0,"unit":"W","category":"PowerConsumption"}'
```

#### Via Ingest Endpoint:
```bash
curl -X POST http://localhost:5050/api/telemetry/ingest \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"AA:BB:CC:DD:00:00","value":9999.0,"unit":"W"}'
```

---

### B. Testing the Command Stack & Undo (LIFO)

#### Via the Avalonia Desktop UI:
1. Under **Issue Manual Override Command**, select a device or click **Select** next to any device in the list.
2. Select a command type (e.g. `ShutdownDevice`), add a payload (e.g. `VALVES_OFF`), and click **Send Command**.
3. Confirm the command appears at the top of the **Stack History** panel on the right.
4. Click **↩ Undo Last**.
5. The command is popped from the `Stack<T>` and the UI shows: `Command [ID] reversed.`

#### Via cURL:
```bash
# 1. Send command
curl -X POST http://localhost:5050/api/commands/send \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"AA:BB:CC:DD:00:00","commandType":"ShutdownDevice","payload":"EMERGENCY_SHUTDOWN"}'

# 2. Inspect Stack history
curl -X GET http://localhost:5050/api/commands/history

# 3. Undo the command
curl -X POST http://localhost:5050/api/commands/undo
```

---

### C. Testing O(1) Dictionary Lookups & Multi-Field Search
1. In the search box, enter a category (e.g. `PowerConsumption`), a Node ID (e.g. `NODE-Zone-A`), or a MAC address.
2. Check the **Anomaly Only** checkbox to instantly filter for compromised nodes.
3. Notice that searching trains the **Prediction Engine** in real time, increasing the search frequency metric for that category.

---

### D. Testing the Chronological Timeline (SortedDictionary)
1. In the **Command Stream** view, observe the **Chronological Timeline** section.
2. The `MiniGraph` renders the sequence of historical values fetched directly from `GET /api/devices/timeline`.
3. Because the API uses a `SortedDictionary<DateTime, List<ReadingEntry>>`, timestamps are intrinsically ordered without sorting delays.

---

### E. Testing Alert Deduplication (HashSet)
1. Multiple spikes hitting the same node will not produce duplicate alert items in the **Active Alerts** list.
2. Click **Resolve** next to an alert to remove it from the `HashSet<T>` via `POST /api/alerts/resolve/{alertKey}`.

