using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartX.Api.Domain
{
    /// <summary>
    /// Represents a network node vertex in the IoT Mesh network.
    /// Can be an IoT Sensor node, a Repeater/Relay node, or the Central Gateway Hub.
    /// </summary>
    public class MeshNode
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Role { get; set; } = "Sensor"; // "Gateway", "Repeater", "Sensor"
        public string Zone { get; set; } = "Default";
        public double BatteryLevel { get; set; } = 100.0;
        public bool IsOnline { get; set; } = true;
        public double X { get; set; } // 2D layout coordinates for visual dashboard
        public double Y { get; set; }

        public MeshNode() { }

        public MeshNode(string macAddress, string deviceName, string role = "Sensor", string zone = "Default", double batteryLevel = 100.0, bool isOnline = true, double x = 0, double y = 0)
        {
            MacAddress = macAddress;
            DeviceName = deviceName;
            Role = role;
            Zone = zone;
            BatteryLevel = batteryLevel;
            IsOnline = isOnline;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Represents a wireless connection edge between two IoT nodes in the mesh.
    /// Weight represents link cost (latency in ms, RSSI signal attenuation, or RF transmission energy).
    /// </summary>
    public class MeshEdge
    {
        public string SourceMac { get; set; } = string.Empty;
        public string DestinationMac { get; set; } = string.Empty;
        public double Weight { get; set; } // Power/Latency metric (lower is more efficient)
        public double SignalStrengthRssi { get; set; } // dBm, e.g. -45 dBm
        public bool IsActive { get; set; } = true;

        public MeshEdge() { }

        public MeshEdge(string sourceMac, string destinationMac, double weight, double signalStrengthRssi = -50.0, bool isActive = true)
        {
            SourceMac = sourceMac;
            DestinationMac = destinationMac;
            Weight = weight;
            SignalStrengthRssi = signalStrengthRssi;
            IsActive = isActive;
        }
    }

    /// <summary>
    /// Path result representing multi-hop routing from a sensor node to the destination/gateway.
    /// </summary>
    public class RoutingPathResult
    {
        public string SourceMac { get; set; } = string.Empty;
        public string DestinationMac { get; set; } = string.Empty;
        public List<string> Hops { get; set; } = new();
        public double TotalCost { get; set; }
        public int HopCount => Hops.Count > 1 ? Hops.Count - 1 : 0;
        public bool IsReachable { get; set; }
        public string FailureReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Minimum Spanning Tree calculation result.
    /// </summary>
    public class MstResult
    {
        public List<MeshEdge> Edges { get; set; } = new();
        public double TotalWeight { get; set; }
        public int NodeCount { get; set; }
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// Graph structure modelling the physical IoT Mesh Network.
    /// Implements Breadth-First Search (BFS), Depth-First Search (DFS),
    /// Prim's Minimum Spanning Tree (MST), and Dijkstra Shortest/Power-efficient Routing with self-healing failover.
    /// </summary>
    public class MeshGraph
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, MeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<MeshEdge>> _adjacencyList = new(StringComparer.OrdinalIgnoreCase);

        public int VertexCount
        {
            get { lock (_lock) return _nodes.Count; }
        }

        public int EdgeCount
        {
            get
            {
                lock (_lock)
                {
                    int total = 0;
                    foreach (var list in _adjacencyList.Values)
                    {
                        total += list.Count;
                    }
                    return total / 2; // Undirected graph
                }
            }
        }

        /// <summary>
        /// Adds a node to the mesh graph.
        /// </summary>
        public bool AddNode(MeshNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.MacAddress)) return false;

            lock (_lock)
            {
                if (_nodes.ContainsKey(node.MacAddress))
                {
                    _nodes[node.MacAddress] = node;
                    return true;
                }

                _nodes[node.MacAddress] = node;
                _adjacencyList[node.MacAddress] = new List<MeshEdge>();
                return true;
            }
        }

        /// <summary>
        /// Adds or updates an undirected wireless link between two mesh nodes with a given weight (transmission cost).
        /// </summary>
        public bool AddEdge(string sourceMac, string destMac, double weight, double rssi = -50.0)
        {
            if (string.IsNullOrWhiteSpace(sourceMac) || string.IsNullOrWhiteSpace(destMac) || sourceMac.Equals(destMac, StringComparison.OrdinalIgnoreCase))
                return false;

            lock (_lock)
            {
                if (!_nodes.ContainsKey(sourceMac) || !_nodes.ContainsKey(destMac))
                    return false;

                // Remove existing edge if any
                _adjacencyList[sourceMac].RemoveAll(e => e.DestinationMac.Equals(destMac, StringComparison.OrdinalIgnoreCase));
                _adjacencyList[destMac].RemoveAll(e => e.DestinationMac.Equals(sourceMac, StringComparison.OrdinalIgnoreCase));

                // Add bidirectional edge
                _adjacencyList[sourceMac].Add(new MeshEdge(sourceMac, destMac, weight, rssi, true));
                _adjacencyList[destMac].Add(new MeshEdge(destMac, sourceMac, weight, rssi, true));
                return true;
            }
        }

        /// <summary>
        /// Sets node online status (e.g. for simulating node hardware failure or power loss).
        /// </summary>
        public bool SetNodeStatus(string macAddress, bool isOnline)
        {
            lock (_lock)
            {
                if (_nodes.TryGetValue(macAddress, out var node))
                {
                    node.IsOnline = isOnline;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Sets edge link status (e.g. interference, link severed).
        /// </summary>
        public bool SetEdgeStatus(string sourceMac, string destMac, bool isActive)
        {
            lock (_lock)
            {
                bool updated = false;
                if (_adjacencyList.TryGetValue(sourceMac, out var srcEdges))
                {
                    var edge = srcEdges.FirstOrDefault(e => e.DestinationMac.Equals(destMac, StringComparison.OrdinalIgnoreCase));
                    if (edge != null) { edge.IsActive = isActive; updated = true; }
                }
                if (_adjacencyList.TryGetValue(destMac, out var destEdges))
                {
                    var edge = destEdges.FirstOrDefault(e => e.DestinationMac.Equals(sourceMac, StringComparison.OrdinalIgnoreCase));
                    if (edge != null) { edge.IsActive = isActive; updated = true; }
                }
                return updated;
            }
        }

        public MeshNode? GetNode(string macAddress)
        {
            lock (_lock)
            {
                _nodes.TryGetValue(macAddress, out var node);
                return node;
            }
        }

        public List<MeshNode> GetAllNodes()
        {
            lock (_lock)
            {
                return _nodes.Values.Select(n => new MeshNode
                {
                    MacAddress = n.MacAddress,
                    DeviceName = n.DeviceName,
                    Role = n.Role,
                    Zone = n.Zone,
                    BatteryLevel = n.BatteryLevel,
                    IsOnline = n.IsOnline,
                    X = n.X,
                    Y = n.Y
                }).ToList();
            }
        }

        public List<MeshEdge> GetAllEdges()
        {
            lock (_lock)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<MeshEdge>();

                foreach (var kvp in _adjacencyList)
                {
                    foreach (var edge in kvp.Value)
                    {
                        var key = string.Compare(edge.SourceMac, edge.DestinationMac, StringComparison.OrdinalIgnoreCase) < 0
                            ? $"{edge.SourceMac}-{edge.DestinationMac}"
                            : $"{edge.DestinationMac}-{edge.SourceMac}";

                        if (seen.Add(key))
                        {
                            result.Add(edge);
                        }
                    }
                }
                return result;
            }
        }

        #region Breadth-First Search (BFS)
        /// <summary>
        /// BFS Traversal to discover all reachable devices in the mesh subnet starting from the specified root/gateway.
        /// Also detects unreachable/down nodes.
        /// </summary>
        public List<string> BreadthFirstSearch(string startMac, bool onlineOnly = true)
        {
            lock (_lock)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var order = new List<string>();

                if (!_nodes.ContainsKey(startMac)) return order;
                if (onlineOnly && !_nodes[startMac].IsOnline) return order;

                var queue = new Queue<string>();
                queue.Enqueue(startMac);
                visited.Add(startMac);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    order.Add(current);

                    if (_adjacencyList.TryGetValue(current, out var edges))
                    {
                        foreach (var edge in edges)
                        {
                            if (!edge.IsActive) continue;

                            var neighbor = edge.DestinationMac;
                            if (onlineOnly && _nodes.TryGetValue(neighbor, out var nNode) && !nNode.IsOnline)
                                continue;

                            if (!visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                return order;
            }
        }
        #endregion

        #region Depth-First Search (DFS)
        /// <summary>
        /// DFS Traversal to explore subnet cluster depths or cycle detection.
        /// </summary>
        public List<string> DepthFirstSearch(string startMac, bool onlineOnly = true)
        {
            lock (_lock)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var order = new List<string>();

                if (!_nodes.ContainsKey(startMac)) return order;
                if (onlineOnly && !_nodes[startMac].IsOnline) return order;

                DfsRecursive(startMac, visited, order, onlineOnly);
                return order;
            }
        }

        private void DfsRecursive(string currentMac, HashSet<string> visited, List<string> order, bool onlineOnly)
        {
            visited.Add(currentMac);
            order.Add(currentMac);

            if (_adjacencyList.TryGetValue(currentMac, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (!edge.IsActive) continue;

                    var neighbor = edge.DestinationMac;
                    if (onlineOnly && _nodes.TryGetValue(neighbor, out var nNode) && !nNode.IsOnline)
                        continue;

                    if (!visited.Contains(neighbor))
                    {
                        DfsRecursive(neighbor, visited, order, onlineOnly);
                    }
                }
            }
        }
        #endregion

        #region Minimum Spanning Tree (MST) - Prim's Algorithm with Min-Heap
        /// <summary>
        /// Calculates the Minimum Spanning Tree (MST) using Prim's algorithm backed by our custom MinHeap.
        /// Connects all online nodes with minimum total transmission power / latency cost.
        /// </summary>
        public MstResult CalculateMinimumSpanningTree(string? startGatewayMac = null)
        {
            lock (_lock)
            {
                var result = new MstResult();
                var onlineNodes = _nodes.Values.Where(n => n.IsOnline).Select(n => n.MacAddress).ToList();

                if (onlineNodes.Count == 0)
                {
                    result.IsConnected = true;
                    return result;
                }

                string start = startGatewayMac != null && _nodes.ContainsKey(startGatewayMac) && _nodes[startGatewayMac].IsOnline
                    ? startGatewayMac
                    : (_nodes.Values.FirstOrDefault(n => n.Role.Equals("Gateway", StringComparison.OrdinalIgnoreCase) && n.IsOnline)?.MacAddress ?? onlineNodes[0]);

                var inMst = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var minHeap = new MinHeap<MeshEdge, double>();

                inMst.Add(start);
                if (_adjacencyList.TryGetValue(start, out var initialEdges))
                {
                    foreach (var edge in initialEdges)
                    {
                        if (edge.IsActive && _nodes.TryGetValue(edge.DestinationMac, out var dNode) && dNode.IsOnline)
                        {
                            minHeap.Enqueue(edge, edge.Weight);
                        }
                    }
                }

                while (!minHeap.IsEmpty && inMst.Count < onlineNodes.Count)
                {
                    var (minEdge, weight) = minHeap.DequeueWithPriority();

                    string nextNode = minEdge.DestinationMac;
                    if (inMst.Contains(nextNode))
                        continue;

                    inMst.Add(nextNode);
                    result.Edges.Add(minEdge);
                    result.TotalWeight += weight;

                    if (_adjacencyList.TryGetValue(nextNode, out var nextEdges))
                    {
                        foreach (var edge in nextEdges)
                        {
                            if (edge.IsActive && !inMst.Contains(edge.DestinationMac) &&
                                _nodes.TryGetValue(edge.DestinationMac, out var targetNode) && targetNode.IsOnline)
                            {
                                minHeap.Enqueue(edge, edge.Weight);
                            }
                        }
                    }
                }

                result.NodeCount = inMst.Count;
                result.IsConnected = inMst.Count == onlineNodes.Count;
                return result;
            }
        }
        #endregion

        #region Shortest/Power-Optimal Routing Path with Dynamic Self-Healing
        /// <summary>
        /// Finds the optimal hop route from source to destination (or central gateway)
        /// using Dijkstra's algorithm with custom MinHeap.
        /// If a node along the path is offline, the algorithm automatically routes around it (self-healing).
        /// </summary>
        public RoutingPathResult FindOptimalRoute(string sourceMac, string destinationMac)
        {
            lock (_lock)
            {
                var result = new RoutingPathResult
                {
                    SourceMac = sourceMac,
                    DestinationMac = destinationMac
                };

                if (!_nodes.ContainsKey(sourceMac))
                {
                    result.IsReachable = false;
                    result.FailureReason = $"Source node {sourceMac} not found in mesh.";
                    return result;
                }

                if (!_nodes.ContainsKey(destinationMac))
                {
                    result.IsReachable = false;
                    result.FailureReason = $"Destination node {destinationMac} not found in mesh.";
                    return result;
                }

                if (!_nodes[sourceMac].IsOnline)
                {
                    result.IsReachable = false;
                    result.FailureReason = $"Source node {sourceMac} is currently OFFLINE.";
                    return result;
                }

                if (!_nodes[destinationMac].IsOnline)
                {
                    result.IsReachable = false;
                    result.FailureReason = $"Destination node {destinationMac} is currently OFFLINE.";
                    return result;
                }

                var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var minHeap = new MinHeap<string, double>();

                foreach (var node in _nodes.Values)
                {
                    distances[node.MacAddress] = double.PositiveInfinity;
                }

                distances[sourceMac] = 0;
                minHeap.Enqueue(sourceMac, 0);

                var settled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (!minHeap.IsEmpty)
                {
                    var (currentMac, currentDist) = minHeap.DequeueWithPriority();

                    if (settled.Contains(currentMac)) continue;
                    settled.Add(currentMac);

                    if (currentMac.Equals(destinationMac, StringComparison.OrdinalIgnoreCase))
                        break;

                    if (_adjacencyList.TryGetValue(currentMac, out var edges))
                    {
                        foreach (var edge in edges)
                        {
                            if (!edge.IsActive) continue;

                            var neighbor = edge.DestinationMac;
                            if (_nodes.TryGetValue(neighbor, out var nNode) && !nNode.IsOnline)
                                continue; // Self-healing: skip offline nodes

                            double newDist = currentDist + edge.Weight;
                            if (newDist < distances[neighbor])
                            {
                                distances[neighbor] = newDist;
                                previous[neighbor] = currentMac;
                                minHeap.Enqueue(neighbor, newDist);
                            }
                        }
                    }
                }

                if (double.IsPositiveInfinity(distances[destinationMac]))
                {
                    result.IsReachable = false;
                    result.FailureReason = $"No active route path exists between {sourceMac} and {destinationMac}. Path severed or isolated.";
                    return result;
                }

                // Reconstruct path
                var path = new List<string>();
                string curr = destinationMac;
                while (curr != null)
                {
                    path.Add(curr);
                    if (curr.Equals(sourceMac, StringComparison.OrdinalIgnoreCase)) break;
                    curr = previous.TryGetValue(curr, out var prev) ? prev : null!;
                }

                path.Reverse();
                result.Hops = path;
                result.TotalCost = distances[destinationMac];
                result.IsReachable = true;
                return result;
            }
        }
        #endregion

        /// <summary>
        /// Populates default mesh network nodes and links if empty.
        /// </summary>
        public void SeedDefaultMeshNetwork()
        {
            lock (_lock)
            {
                if (_nodes.Count > 0) return;

                // Central Gateway Hub
                AddNode(new MeshNode("GW:00:11:22:33:44", "Central Gateway Hub", "Gateway", "Operations Center", 100.0, true, 450, 250));

                // Zone A Repeaters & Sensors (North-West)
                AddNode(new MeshNode("ESP:AA:01:02:03:04", "Repeater Alpha (ESP32)", "Repeater", "Zone A - North", 94.5, true, 250, 150));
                AddNode(new MeshNode("ESP:AA:10:20:30:40", "Soil Moisture Node 1", "Sensor", "Zone A - North", 88.0, true, 120, 100));
                AddNode(new MeshNode("ESP:AA:11:22:33:44", "Ambient Temp Node 2", "Sensor", "Zone A - North", 79.0, true, 180, 230));

                // Zone B Repeaters & Sensors (North-East)
                AddNode(new MeshNode("ESP:BB:01:02:03:04", "Repeater Beta (ESP32)", "Repeater", "Zone B - East", 91.0, true, 650, 150));
                AddNode(new MeshNode("ESP:BB:10:20:30:40", "Greenhouse Humidity 1", "Sensor", "Zone B - East", 84.0, true, 780, 110));
                AddNode(new MeshNode("ESP:BB:11:22:33:44", "Solar Power Node 2", "Sensor", "Zone B - East", 95.0, true, 720, 240));

                // Zone C Repeaters & Sensors (South)
                AddNode(new MeshNode("ESP:CC:01:02:03:04", "Repeater Gamma (ESP32)", "Repeater", "Zone C - South", 97.0, true, 450, 420));
                AddNode(new MeshNode("ESP:CC:10:20:30:40", "Irrigation Flow Node 1", "Sensor", "Zone C - South", 73.0, true, 300, 480));
                AddNode(new MeshNode("ESP:CC:11:22:33:44", "Pressure Tank Node 2", "Sensor", "Zone C - South", 82.0, true, 600, 480));

                // Edges with realistic latency/power weights (e.g. 5ms - 35ms) and RSSI
                // Gateway to Repeaters (Backbone)
                AddEdge("GW:00:11:22:33:44", "ESP:AA:01:02:03:04", 8.5, -48);
                AddEdge("GW:00:11:22:33:44", "ESP:BB:01:02:03:04", 9.2, -50);
                AddEdge("GW:00:11:22:33:44", "ESP:CC:01:02:03:04", 11.0, -53);

                // Inter-repeater mesh cross-links (Self-healing redundant paths!)
                AddEdge("ESP:AA:01:02:03:04", "ESP:BB:01:02:03:04", 18.0, -68);
                AddEdge("ESP:AA:01:02:03:04", "ESP:CC:01:02:03:04", 16.5, -65);
                AddEdge("ESP:BB:01:02:03:04", "ESP:CC:01:02:03:04", 17.2, -67);

                // Zone A internal links
                AddEdge("ESP:AA:01:02:03:04", "ESP:AA:10:20:30:40", 6.0, -42);
                AddEdge("ESP:AA:01:02:03:04", "ESP:AA:11:22:33:44", 5.5, -40);
                AddEdge("ESP:AA:10:20:30:40", "ESP:AA:11:22:33:44", 14.0, -62);

                // Zone B internal links
                AddEdge("ESP:BB:01:02:03:04", "ESP:BB:10:20:30:40", 6.5, -44);
                AddEdge("ESP:BB:01:02:03:04", "ESP:BB:11:22:33:44", 5.0, -39);
                AddEdge("ESP:BB:10:20:30:40", "ESP:BB:11:22:33:44", 12.5, -60);

                // Zone C internal links
                AddEdge("ESP:CC:01:02:03:04", "ESP:CC:10:20:30:40", 7.0, -45);
                AddEdge("ESP:CC:01:02:03:04", "ESP:CC:11:22:33:44", 6.8, -43);
                AddEdge("ESP:CC:10:20:30:40", "ESP:CC:11:22:33:44", 15.0, -64);
            }
        }
    }
}
