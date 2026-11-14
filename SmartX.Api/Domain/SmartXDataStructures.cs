using System;
using System.Collections.Generic;

namespace SmartX.Api.Domain
{
    /// <summary>
    /// Dedicated Data Structure Container Class specifically meeting the lecturer brief:
    /// "I highly recommend creating a dedicated class in your API that holds your data structures,
    /// with each one named according to its purpose in the brief:
    /// TelemetryQueue, CriticalAlertQueue, CommandUndoStack, DeviceRegistry, SensorTimeline, DisconnectedNodes,
    /// HistoricalTelemetryTree and MeshNetworkTopology."
    /// </summary>
    public class SmartXDataStructures
    {
        // 1. TelemetryQueue (Queue<T>)
        public Queue<IncomingPacket> TelemetryQueue { get; } = new();

        // 2. CriticalAlertQueue (PriorityQueue<T, int>)
        public PriorityQueue<IncomingPacket, int> CriticalAlertQueue { get; } = new();

        // 3. CommandUndoStack (Stack<T>)
        public Stack<CommandRecord> CommandUndoStack { get; } = new();

        // 4. DeviceRegistry (Dictionary<string, DeviceState>) - O(1) Lookups
        public Dictionary<string, DeviceState> DeviceRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

        // 5. SensorTimeline (SortedDictionary<DateTime, List<ReadingEntry>>) - Chronological Ordered Timeline
        public SortedDictionary<DateTime, List<ReadingEntry>> SensorTimeline { get; } = new();

        // 6. DisconnectedNodes (HashSet<string>) - Deduplicated Set of Offline/Disconnected Nodes
        public HashSet<string> DisconnectedNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

        // 7. HistoricalTelemetryTree (AVLTree<DateTime, ReadingEntry>) - Self-Balancing O(log N + K) Range Queries
        public AVLTree<DateTime, ReadingEntry> HistoricalTelemetryTree { get; } = new();

        // 8. MeshNetworkTopology (MeshGraph) - Graph modeling nodes, edges, BFS/DFS & Prim's MST
        public MeshGraph MeshNetworkTopology { get; } = new();

        private readonly object _lock = new();

        public SmartXDataStructures()
        {
            MeshNetworkTopology.SeedDefaultMeshNetwork();
        }

        public void IngestReading(string mac, string nodeId, float value, string unit, bool isCritical, int priority = 0)
        {
            var now = DateTime.UtcNow;
            var packet = new IncomingPacket(
                DeviceId: mac,
                NodeId: nodeId,
                Value: value,
                Unit: unit,
                Category: "Telemetry",
                IsCritical: isCritical,
                Priority: priority,
                ReceivedAt: now
            );

            lock (_lock)
            {
                // Queue vs PriorityQueue
                if (isCritical)
                {
                    CriticalAlertQueue.Enqueue(packet, priority);
                }
                else
                {
                    TelemetryQueue.Enqueue(packet);
                }

                // Dictionary O(1)
                DeviceRegistry[mac] = new DeviceState(mac, nodeId, "Telemetry", value, unit, isCritical, now);

                // SortedDictionary Chronological Timeline
                if (!SensorTimeline.TryGetValue(now, out var list))
                {
                    list = new List<ReadingEntry>();
                    SensorTimeline[now] = list;
                }
                var reading = new ReadingEntry(mac, nodeId, value, unit, isCritical);
                list.Add(reading);

                // AVL Tree Historical Indexing
                HistoricalTelemetryTree.Insert(now, reading);
            }
        }
    }
}
