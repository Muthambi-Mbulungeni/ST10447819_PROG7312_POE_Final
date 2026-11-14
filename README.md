# Smart-X Mesh Ecosystem — POE Part 3 (Final Full Functioning App)

**Student Name:** Muthambi Mbulungeni  
**Student Identifier:** ST10447819  
**Course Module:** PROG7312 — Advanced Programming (2026)  
**Target Milestone:** Portfolio of Evidence (PoE) Part 3 (100 Marks)  
**GitHub Repository:** https://github.com/Muthambi-Mbulungeni/ST10447819_PROG7312_POE_Final.git 

---

## 1. System Overview

The **Smart-X Mesh Ecosystem** is an enterprise-grade industrial IoT platform designed to monitor, orchestrate, and optimize distributed fleets of ESP32 microcontrollers across mission-critical facilities. 

The system incorporates advanced data structures across three interconnected architectural pillars:
1. **Pillar 1: Sensor Data Ingestion & Telemetry:** Generic packet processing (`where T : struct`), memory-efficient jagged arrays (`float[]?[]`), operator overloading, recursive deployment tree validation, and thread-safe ingestion queues.
2. **Pillar 2: Real-Time Command Stream & History:** Standard FIFO queues (`Queue<T>`), critical alert bypass (`PriorityQueue<T, int>`), reversible command undo stack (`Stack<T>`), single source of truth device directory (`Dictionary<string, DeviceState>`), chronologically ordered historical timeline (`SortedDictionary<DateTime, List<ReadingEntry>>`), dual deduplicating sets (`HashSet<string>` for alerts and disconnected nodes), and a Markov-style learned follow-up prediction engine.
3. **Pillar 3: Network Topology & Mesh Routing (Final PoE Phase):** Physical wireless mesh modeling using an undirected weighted `MeshGraph`, custom self-balancing `AVLTree<TKey, TValue>` for logarithmic $O(\log N + K)$ historical timestamp range querying, custom binary `MinHeap<TElement, TPriority>`, **Breadth-First Search (BFS)** for subnet reachability, **Depth-First Search (DFS)** for cycle exploration, **Prim's Algorithm** for power-optimal Minimum Spanning Tree (MST) computation, and dynamic **self-healing** routing failover.

---

## 2. Quick Start & Execution

### Prerequisites
- .NET 8.0 SDK or .NET 10.0 SDK installed.
- (Optional) Docker & Docker Compose for containerized deployment.

### Option A: Local Execution (API + Dual Web & Desktop Clients)
```bash
# Make the startup script executable and launch
chmod +x start.sh
./start.sh
```

- **Backend Web API & Swagger UI:** http://localhost:5050/swagger  
- **Interactive 3-Pillar Web Dashboard:** http://localhost:5050  
- **Avalonia Cross-Platform Desktop Client:** Automatically launches connected to `http://localhost:5050/api`

### Option B: Docker Compose (Backend API Container)
```bash
docker compose up --build
```
The API boots in an isolated container mapped to `http://localhost:5050`.

### Option C: Automated Unit Test Suite
```bash
dotnet test
```
Executes all 12 automated unit tests across Parts 1, 2, and 3, verifying queue priority rules, capacity eviction, stack undo, single source of truth, dual hash sets, AVL tree balancing, MinHeap extraction, BFS/DFS traversal, and Prim's MST.

---

## 3. Data Structure Asymptotic Complexity Analysis

| Data Structure | Implementation File | Role & Purpose in Ecosystem | Search / Query | Insertion | Deletion | Space |
|---|---|---|---|---|---|---|
| **`AVLTree<TKey, TValue>`** | `SmartX.Api/Domain/AVLTree.cs` | **Self-Balancing BST:** Stores historical timestamped telemetry. Executes range queries (`QueryRange(from, to)`) via subtree pruning without scanning unorganized lists. | $O(\log N + K)$ | $O(\log N)$ | $O(\log N)$ | $O(N)$ |
| **`MinHeap<TElement, TPriority>`** | `SmartX.Api/Domain/MinHeap.cs` | **Binary Min-Heap:** Powers Prim's MST algorithm and Dijkstra shortest-path calculations with $O(1)$ minimum peek. | $O(1)$ (peek) | $O(\log V)$ | $O(\log V)$ | $O(V)$ |
| **`MeshGraph`** | `SmartX.Api/Domain/MeshGraph.cs` | **Weighted Mesh Graph:** Models physical ESP32 nodes (vertices) and wireless links (edges) with BFS, DFS, Prim's MST, and self-healing failover. | BFS: $O(V + E)$<br>DFS: $O(V + E)$ | $O(1)$ (vertex)<br>$O(1)$ (edge) | $O(E)$ | $O(V + E)$ |
| **`PriorityQueue<T, int>`** | `SmartX.Api/Domain/TelemetryProcessingQueue.cs` | **Critical Alert Lane:** Urgent sensor spikes bypass the standard queue and are dequeued before normal telemetry. | $O(1)$ (peek) | $O(\log N)$ | $O(\log N)$ | $O(N)$ |
| **`Queue<T>`** | `SmartX.Api/Domain/TelemetryProcessingQueue.cs` | **Standard Ingestion Queue:** Bounded FIFO queue for routine telemetry; evicts oldest packet when full. | $O(1)$ (peek) | $O(1)$ | $O(1)$ | $O(N)$ |
| **`Stack<T>`** | `SmartX.Api/Domain/CommandStack.cs` | **Undo Command History:** LIFO storage for operator overrides enabling non-crashing 1-click Undo. | $O(1)$ (peek) | $O(1)$ | $O(1)$ | $O(N)$ |
| **`Dictionary<K, V>`** | `SmartX.Api/Domain/DeviceDirectory.cs` | **O(1) Device Registry:** Single source of truth for device hardware profiles and live states keyed by MAC. | $O(1)$ | $O(1)$ | $O(1)$ | $O(N)$ |
| **`SortedDictionary<K, V>`** | `SmartX.Api/Domain/DeviceDirectory.cs` | **Sequential Timeline:** Chronological ordering by `DateTime` for simultaneous identical timestamp grouping. | $O(\log N)$ | $O(\log N)$ | $O(\log N)$ | $O(N)$ |
| **`HashSet<T>`** | `SmartX.Api/Domain/AlertTracker.cs` | **Dual Sets:** Set 1 deduplicates active alerts; Set 2 deduplicates disconnected hardware nodes. | $O(1)$ | $O(1)$ | $O(1)$ | $O(N)$ |

---

## 4. Verification & Testing Guide

### A. Testing Historical Telemetry Range Querying (AVL Tree $O(\log N + K)$)
1. Open the Desktop app (or Web Dashboard at `http://localhost:5050`).
2. Navigate to **Network Topology & Mesh Routing (Pillar 3)**.
3. Click **"🌲 Query AVL Tree (Historical)"**.
4. Observe:
   - The self-balancing AVL Tree queries timestamped telemetry within the specified window in logarithmic time by pruning out-of-range subtrees.
   - Retaining multiple readings at identical timestamps without data loss.

### B. Testing Prim's Minimum Spanning Tree (MST) & Power Optimization
1. In the **Network Topology** view, click **"⚡ Compute Prim's MST"**.
2. Observe:
   - Prim's algorithm backed by the custom `MinHeap` computes the global power-optimal backbone spanning all $V$ nodes across $V - 1$ wireless links.
   - Total transmission weight is minimized, preventing packet collisions and extending battery longevity.

### C. Testing Dynamic Mesh Self-Healing ("Holding Hands" Analogy)
1. In the **Network Topology** view, click **"🚨 Simulate Node Failover (Self-Healing)"**.
2. Repeater Alpha (`ESP:AA:01:02:03:04`) is marked offline.
3. Observe:
   - The network dynamically reroutes telemetry around the failed node through redundant cross-zone repeaters.
   - Disconnected nodes are tracked in the secondary `HashSet<string>`.
4. Click **"🔄 Restore Node"** to reconnect Repeater Alpha and verify route re-establishment.

### D. Testing PriorityQueue Critical Bypass & Capacity Rule
1. Under **Real Time Command Stream (Pillar 2)**, click **"⚡ Simulate Spike"**.
2. Observe:
   - High-priority alert immediately emerges before routine packets.
   - Run `dotnet test --filter Part2VerificationTests` to verify automated proof of priority order and queue eviction rules.

### E. Testing Learned Follow-Up Prediction Engine
1. In the search box, search for `PowerConsumption`.
2. Issue a manual override command.
3. Observe that the engine records the transition and calculates confidence:
   $$\text{Confidence} = \frac{\text{Count}(\text{Search } \to \text{ Command } X)}{\sum \text{Count}(\text{Search } \to \text{ All Commands})} \times 100\%$$
4. Check the **Learned Actions** panel showing: *"Operators searching for 'PowerConsumption' subsequently issued 'ManualOverride' with 85% confidence."*

---

## 5. Summary of Formative Feedback Resolutions

All critique points from the Part 2 evaluation have been resolved and documented in [`docs/FEEDBACK_UPDATES_LOG.md`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/docs/FEEDBACK_UPDATES_LOG.md):
- **Removed Scratch Projects:** Completely removed `WpfTest`.
- **Clear 3-Pillar UI:** Prominently presented Pillar 1, Pillar 2, and Pillar 3 across both Desktop and Web frontends with persistent state.
- **Automated Proofs:** Added `SmartX.Tests` unit test suite proving priority lane behavior and capacity eviction.
- **Single Source of Truth:** Consolidated device profiles and states into `DeviceDirectory`.
- **Dual HashSets:** Added secondary set for disconnected nodes.
- **Learned Prediction Engine:** Replaced search popularity list with Markov transition follow-ups and percentage confidence scoring.

---

## 6. Submission Deliverables

1. **Source Code:** Complete functioning application suite on GitHub with over 50 atomic, conventional commits.
2. **Implementation & Completion Report (DOCX):** [`docs/SmartX_Part3_Final_Comprehensive_Report.docx`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/docs/SmartX_Part3_Final_Comprehensive_Report.docx).
3. **Implementation & Completion Report (Markdown):** [`docs/PROJECT_COMPLETION_REPORT_PART3.md`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/docs/PROJECT_COMPLETION_REPORT_PART3.md).
4. **Lecturer Feedback Action Log:** [`docs/FEEDBACK_UPDATES_LOG.md`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/docs/FEEDBACK_UPDATES_LOG.md).
5. **Docker Deployment:** [`Dockerfile`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/Dockerfile) and [`docker-compose.yml`](file:///home/gaylordrobinson7000/Documents/PROG312/prog7312-2026-prog7312-part-2-muthambi-mbulungeni/docker-compose.yml).
