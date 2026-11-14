# PROG7312 Portfolio of Evidence (PoE) — Part 3 Comprehensive Project Completion & Technical Report

**Course Module:** PROG7312 — Advanced Programming  
**Student Name:** Muthambi Mbulungeni  
**Student Identifier:** ST10447819  
**Platform:** Smart-X Industrial IoT Edge-to-Cloud Ecosystem  
**Target Milestone:** PoE Part 3 Final Full Functioning App (100 Marks)  
**Institution:** The Independent Institute of Education (IIE) / Varsity College  

---

## 1. Executive Summary & Ecosystem Architecture

The **Smart-X Industrial IoT Platform** represents an enterprise-grade distributed edge-to-cloud ecosystem developed to monitor, orchestrate, and optimize dispersed fleets of ESP32 microcontrollers operating across mission-critical facilities (smart agriculture, industrial factories, and renewable energy grids).

Across three incremental milestones, the project has evolved into a unified, high-performance software suite:
- **Part 1 (Sensor Ingestion & Telemetry):** Built high-efficiency generic packet models (`TelemetryPacket<T>`) guaranteeing zero memory boxing/unboxing overhead, raw jagged array buffering for burst ingestion, recursive parent-chain validation for deployment trees, and AES-256 encrypted file attachment storage for hardware diagnostics.
- **Part 2 (Real-Time Command Stream & History):** Integrated foundational collections conforming strictly to lecturer specifications (`TelemetryQueue` via `Queue<T>`, `CriticalAlertQueue` via `PriorityQueue<T, P>`, `CommandUndoStack` via `Stack<T>` with 1-click reversible Undo, `DeviceRegistry` via `Dictionary<TKey, TValue>` for $O(1)$ lookups, `SensorTimeline` via `SortedDictionary<DateTime, T>`, and `DisconnectedNodes` via `HashSet<string>`), paired with an automated predictive recommendation engine.
- **Part 3 (Network Topology & Mesh Routing):** Finalized the ecosystem by mathematically modeling decentralized wireless mesh communication (Zigbee, Thread, and Espressif ESP-NOW topologies) using an undirected weighted `MeshGraph`. We engineered custom, from-scratch data structures including a self-balancing `AVLTree<TKey, TValue>`, binary `MinHeap<TElement, TPriority>`, Prim's Minimum Spanning Tree (MST) algorithm, Breadth-First/Depth-First Search (BFS/DFS) subnet reachability routines, and Dijkstra shortest path routing with dynamic self-healing failover.

---

## 2. Implementation Report: Data Structures Deep Dive (50 Marks)

### 2.1 Self-Balancing AVL Tree for Historical Telemetry Querying (30 Marks)

#### Role & Architectural Purpose
In mission-critical IoT systems, sensors generate thousands of continuous timestamped data points. Storing historical telemetry in naive unorganized arrays or linked lists incurs $O(N)$ linear scan latency when operators query specific time intervals (e.g., *"Retrieve all moisture readings between 14:00 and 16:00 for Node A"*). Furthermore, simple Binary Search Trees (BST) run the critical risk of degenerating into an $O(N)$ linked list when incoming telemetry packets arrive in strictly ascending chronological order.

To eliminate this bottleneck, Smart-X implements a custom, from-scratch self-balancing **AVL Tree** (`AVLTree<TKey, TValue>`).
- **Guarantee:** Enforces the strict height-balance property where the height difference between left and right subtrees is at most 1 ($|\text{Balance Factor}| \le 1$).
- **Rotations:** Employs single (Left-Left, Right-Right) and double (Left-Right, Right-Left) rotations upon node insertion to maintain logarithmic depth.
- **Complexity:** Insertion runs in $O(\log N)$; Range queries run in $O(\log N + K)$ where $K$ is the number of records within the window.

#### C# Implementation Example:
```csharp
public List<TValue> QueryRange(TKey fromKey, TKey toKey)
{
    lock (_lock)
    {
        var results = new List<TValue>();
        QueryRangeRecursive(_root, fromKey, toKey, results);
        return results;
    }
}

private void QueryRangeRecursive(AVLNode? node, TKey fromKey, TKey toKey, List<TValue> results)
{
    if (node == null) return;

    // Prune left subtree if current key is strictly greater than fromKey
    if (node.Key.CompareTo(fromKey) > 0)
        QueryRangeRecursive(node.Left, fromKey, toKey, results);

    // Collect values within the [fromKey, toKey] interval
    if (node.Key.CompareTo(fromKey) >= 0 && node.Key.CompareTo(toKey) <= 0)
        results.AddRange(node.Values);

    // Prune right subtree if current key is strictly less than toKey
    if (node.Key.CompareTo(toKey) < 0)
        QueryRangeRecursive(node.Right, fromKey, toKey, results);
}
```

---

### 2.2 Custom Binary Min-Heap Data Structure

#### Role & Architectural Purpose
A Min-Heap is a complete binary tree satisfying the heap invariant: the key of any parent node is less than or equal to the keys of its children. In Smart-X, `MinHeap<TElement, TPriority>` serves as the high-speed priority engine driving Prim's MST algorithm and Dijkstra shortest path calculations.

- **Peek:** Returns lowest weight edge in $O(1)$.
- **Enqueue:** Appends element and bubbles up in $O(\log V)$.
- **Dequeue:** Removes root, replaces with last element, and sifts down in $O(\log V)$.

---

### 2.3 IoT Mesh Graph, BFS/DFS Traversal & Subnet Reachability (30 Marks)

#### Role & Architectural Purpose
The `MeshGraph` models physical and wireless interactions across IoT devices:
- **Vertices ($V$):** Represent physical microcontrollers (ESP32 nodes), relay repeaters, and the Central Gateway Hub.
- **Edges ($E$):** Wireless communication links characterized by latency (ms), RF signal attenuation (RSSI in dBm), and transmission power costs.

#### Subnet Traversal (BFS & DFS)
- **Breadth-First Search (BFS):** Explores nodes layer-by-layer using a FIFO queue. Used by the Central Gateway to identify all actively reachable nodes on a subnet and immediately detect partitioned or isolated clusters.
- **Depth-First Search (DFS):** Traverses deeply along each branch using recursion. Used for cycle detection and boundary mapping.

```csharp
public List<string> BreadthFirstSearch(string startMac, bool onlineOnly = true)
{
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var order = new List<string>();
    var queue = new Queue<string>();

    queue.Enqueue(startMac);
    visited.Add(startMac);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        order.Add(current);

        foreach (var edge in _adjacencyList[current])
        {
            if (!edge.IsActive) continue;
            var neighbor = edge.DestinationMac;
            if (onlineOnly && !_nodes[neighbor].IsOnline) continue;

            if (visited.Add(neighbor))
                queue.Enqueue(neighbor);
        }
    }
    return order;
}
```

---

### 2.4 Minimum Spanning Tree (Prim's Algorithm) & Self-Healing Routing

#### Minimum Spanning Tree (MST)
Battery life is the primary constraint of field-deployed wireless microcontrollers. Broadcasting at maximum power directly to a distant gateway drains batteries and causes RF packet collisions. Smart-X employs Prim's Algorithm backed by our custom `MinHeap<TElement, TPriority>` to construct the Minimum Spanning Tree. Starting at the Central Gateway, the algorithm greedily adds the minimum-weight wireless link that connects an unvisited node, establishing the global power-optimal backbone spanning all $V$ nodes across $V - 1$ links in $O(E \log V)$ time.

#### Dynamic Mesh Self-Healing ("Holding Hands" Analogy)
To replicate physical mesh resilience, the platform provides Dijkstra shortest path route tracing with self-healing failover. When an operator or physical fault disables a repeater (e.g. Repeater Alpha goes offline), the routing engine automatically avoids the failed node and dynamically discovers alternate redundant paths through neighboring repeaters without service interruption.

---

## 3. Project Completion Report: Architectural Roadblocks, Debugging & Learnings (10 Marks)

### 3.1 Addressing All Part 2 Feedback Points Directly
In this milestone, we methodically resolved every issue raised by the examiner:
1. **Leftover Scratch Projects:** Removed the legacy `WpfTest` directory completely to ensure clear project focus.
2. **Clear 3-Pillar Interface:** Restructured navigation to prominently present Pillar 1 (Sensor Ingestion), Pillar 2 (Command Stream), and Pillar 3 (Network Topology), maintaining ViewModel state caching across all view transitions.
3. **Automated Unit Testing:** Added `SmartX.Tests` containing 12 unit tests validating PriorityQueue bypass, capacity eviction rules, and Stack Undo.
4. **Single Source of Truth:** Refactored `DeviceDirectory` to act as the single source of truth for both hardware profiles and live states, eliminating duplicate storage.
5. **Dual HashSets:** Added a second `HashSet<string> _disconnectedNodes` in `AlertTracker` for tracking disconnected nodes without duplicates.
6. **Learned Follow-Up Prediction Engine:** Transformed the prediction engine from a naive search popularity counter into a Markov transition model linking searches to subsequent commands with exact percentage confidence counts.

### 3.2 Key Technical Learnings Linked to PROG7312
- **Memory Efficiency & Generic Constraints:** Constraining generics `where T : struct` prevents runtime boxing/unboxing overhead in high-frequency IoT pipelines.
- **Data Structure Trade-Offs:** Moving from linear scans ($O(N)$) to balanced AVL trees ($O(\log N + K)$) and Priority Queues/Min-Heaps demonstrated how theoretical time complexities directly translate into real-world responsiveness.
- **Asynchronous GUI & Thread Safety:** Coordinating asynchronous API calls with Avalonia UI dispatchers and thread locks reinforced best practices for thread-safe concurrent programming.

---

## 4. Technology Recommendations & Future Scaling (10 Marks)

### Recommendation 1: Google Cloud Platform (Cloud Run & Cloud SQL)
- **Tool:** Containerized backend deployment via Google Cloud Run with distributed PostgreSQL / TimescaleDB via Cloud SQL.
- **Rationale & Compatibility:** Cloud Run offers serverless auto-scaling that scales down to zero when sensor activity is dormant, dramatically reducing hosting costs. .NET 8/10 has first-class container support (`Microsoft.NET.Build.Containers`), enabling seamless single-command cloud deployment without Docker daemon dependencies.

### Recommendation 2: GitHub Actions Automated CI/CD Pipeline
- **Tool:** Automated GitHub Actions workflow executing on pull request and push events.
- **Rationale & Compatibility:** Runs `dotnet restore`, `dotnet build --configuration Release`, and `dotnet test` automatically on every commit. Catches regressions in AVL Tree balance or queue eviction rules before code merges into main.

### Recommendation 3: LLM-Powered Natural Language Daily Telemetry Summarization
- **Tool:** Google Gemini API / Semantic Kernel embedded via .NET SDK.
- **Rationale & Compatibility:** Consolidates thousands of raw telemetry readings and active alerts into a concise, human-readable executive briefing (e.g. *"Zone A moisture dropped 22% over 6 hours; valve manual override recommended"*). Semantic Kernel provides native C# integration with dependency injection.

### Recommendation 4: Home Assistant & Zigbee2MQTT Integration
- **Tool:** Home Assistant (https://www.home-assistant.io/) with MQTT discovery.
- **Rationale & Compatibility:** Allows Smart-X to interface with standard off-the-shelf industrial IoT sensors and actuators. Home Assistant publishes telemetry via MQTT, which Smart-X ingests through high-throughput MQTT .NET clients (`MQTTnet`).

---

## 5. Verification & Compliance Checklist

- [x] **AVL Tree Historical Range Query:** Tested and verified in `Part3DataStructuresTests.cs`.
- [x] **Mesh Graph with BFS/DFS & Prim's MST:** Fully implemented with custom `MinHeap`.
- [x] **Dynamic Self-Healing Simulation:** Verified via UI failover toggles and automated test.
- [x] **All Part 2 Feedback Resolved:** Documented in `FEEDBACK_UPDATES_LOG.md`.
- [x] **Automated Test Suite:** 12 unit tests passing with zero failures.
- [x] **Git Commit History:** Over 50 commits with descriptive messages.
