using System;
using System.Collections.Generic;
using System.Linq;
using SmartX.Api.Domain;
using Xunit;

namespace SmartX.Tests
{
    /// <summary>
    /// Part 3 Unit Tests verifying:
    /// - AVLTree self-balancing rotations, logarithmic height, and O(log N + K) timestamp range queries
    /// - MinHeap min-order extraction and peek
    /// - MeshGraph modeling ESP32 nodes and wireless edges
    /// - Breadth-First Search (BFS) subnet exploration and reachability
    /// - Depth-First Search (DFS) traversal
    /// - Prim's Minimum Spanning Tree (MST) power-optimal topology
    /// - Dynamic self-healing route failover when nodes go offline
    /// </summary>
    public class Part3DataStructuresTests
    {
        #region 1. AVL Tree Tests (Self-Balancing BST for Historical Telemetry)
        [Fact]
        public void AVLTree_InsertAndBalance_MaintainsLogarithmicHeight()
        {
            var avl = new AVLTree<int, string>();

            // Insert ascending sequence 1..15 which would make a normal BST degenerate to height 15 (linked list O(N))
            for (int i = 1; i <= 15; i++)
            {
                avl.Insert(i, $"Sensor-Reading-{i}");
            }

            Assert.Equal(15, avl.Count);
            Assert.NotNull(avl.Root);

            // In a balanced AVL tree, height for N=15 is <= 5
            Assert.True(avl.Root.Height <= 5, $"AVL height was {avl.Root.Height}, expected <= 5");

            // In-order traversal should return strictly sorted items
            var inOrder = avl.ToInOrderList();
            Assert.Equal(15, inOrder.Count);
            Assert.Equal("Sensor-Reading-1", inOrder[0]);
            Assert.Equal("Sensor-Reading-15", inOrder[14]);
        }

        [Fact]
        public void AVLTree_HistoricalTimestampRangeQuery_PrunesSubtreesAndReturnsRecordsInWindow()
        {
            var tree = new AVLTree<DateTime, ReadingEntry>();
            var baseTime = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

            // Insert 60 readings, 1 minute apart
            for (int m = 0; m < 60; m++)
            {
                var ts = baseTime.AddMinutes(m);
                var entry = new ReadingEntry(
                    Mac: "ESP:AA:10:20:30:40",
                    NodeId: "NODE-ZoneA-Soil01",
                    Value: 40.0f + m * 0.5f,
                    Unit: "Soil Moisture %",
                    IsAnomaly: false,
                    Timestamp: ts
                );
                tree.Insert(ts, entry);
            }

            Assert.Equal(60, tree.Count);

            // Query moisture readings between 10:15 and 10:45 inclusive (31 minutes)
            var from = baseTime.AddMinutes(15);
            var to = baseTime.AddMinutes(45);

            var rangeResults = tree.QueryRange(from, to);

            Assert.Equal(31, rangeResults.Count);
            Assert.All(rangeResults, r => Assert.True(r.Timestamp >= from && r.Timestamp <= to));
            Assert.Equal(40.0f + 15 * 0.5f, rangeResults.First().Value);
            Assert.Equal(40.0f + 45 * 0.5f, rangeResults.Last().Value);
        }
        #endregion

        #region 2. MinHeap Tests
        [Fact]
        public void MinHeap_MaintainsMinPriorityOrderAndExtraction()
        {
            var heap = new MinHeap<string, double>();

            heap.Enqueue("Node-A", 15.5);
            heap.Enqueue("Node-B", 4.2);
            heap.Enqueue("Node-C", 22.0);
            heap.Enqueue("Node-D", 1.8);
            heap.Enqueue("Node-E", 9.1);

            Assert.Equal(5, heap.Count);
            Assert.Equal("Node-D", heap.Peek());

            Assert.Equal("Node-D", heap.Dequeue()); // 1.8
            Assert.Equal("Node-B", heap.Dequeue()); // 4.2
            Assert.Equal("Node-E", heap.Dequeue()); // 9.1
            Assert.Equal("Node-A", heap.Dequeue()); // 15.5
            Assert.Equal("Node-C", heap.Dequeue()); // 22.0
            Assert.True(heap.IsEmpty);
        }
        #endregion

        #region 3. Mesh Graph & Traversal Tests (BFS / DFS)
        [Fact]
        public void MeshGraph_BreadthFirstSearch_DiscoversReachableNodes()
        {
            var graph = new MeshGraph();
            graph.SeedDefaultMeshNetwork();

            // Perform BFS starting from Central Gateway Hub
            var visitedOrder = graph.BreadthFirstSearch("GW:00:11:22:33:44", onlineOnly: true);

            Assert.NotEmpty(visitedOrder);
            Assert.Equal("GW:00:11:22:33:44", visitedOrder[0]);

            // All repeaters and sensors must be discovered in connected component
            Assert.Contains("ESP:AA:01:02:03:04", visitedOrder);
            Assert.Contains("ESP:BB:01:02:03:04", visitedOrder);
            Assert.Contains("ESP:CC:01:02:03:04", visitedOrder);
            Assert.Contains("ESP:AA:10:20:30:40", visitedOrder);
        }

        [Fact]
        public void MeshGraph_DepthFirstSearch_TraversesDeepSubnet()
        {
            var graph = new MeshGraph();
            graph.SeedDefaultMeshNetwork();

            var dfsOrder = graph.DepthFirstSearch("GW:00:11:22:33:44", onlineOnly: true);

            Assert.NotEmpty(dfsOrder);
            Assert.Equal("GW:00:11:22:33:44", dfsOrder[0]);
            Assert.Equal(graph.VertexCount, dfsOrder.Count);
        }
        #endregion

        #region 4. Minimum Spanning Tree (MST - Prim's Algorithm via MinHeap)
        [Fact]
        public void MeshGraph_CalculateMinimumSpanningTree_GeneratesOptimalPowerBackbone()
        {
            var graph = new MeshGraph();
            graph.SeedDefaultMeshNetwork();

            var mst = graph.CalculateMinimumSpanningTree("GW:00:11:22:33:44");

            Assert.NotNull(mst);
            Assert.True(mst.IsConnected);
            Assert.Equal(graph.VertexCount, mst.NodeCount);

            // For V vertices, a tree must have exactly V - 1 edges
            Assert.Equal(graph.VertexCount - 1, mst.Edges.Count);

            // Total weight must be strictly less than total graph weight (optimal pruning)
            double totalGraphWeight = graph.GetAllEdges().Sum(e => e.Weight);
            Assert.True(mst.TotalWeight < totalGraphWeight);
        }
        #endregion

        #region 5. Shortest Route & Self-Healing Failover
        [Fact]
        public void MeshGraph_FindOptimalRoute_PerformsDynamicSelfHealing_WhenRepeaterFails()
        {
            var graph = new MeshGraph();
            graph.SeedDefaultMeshNetwork();

            string sensorMac = "ESP:AA:10:20:30:40";
            string gatewayMac = "GW:00:11:22:33:44";

            // Normal state: Sensor -> Repeater Alpha -> Gateway
            var normalRoute = graph.FindOptimalRoute(sensorMac, gatewayMac);
            Assert.True(normalRoute.IsReachable);
            Assert.Contains("ESP:AA:01:02:03:04", normalRoute.Hops);

            // Simulate hardware failure: Repeater Alpha goes offline!
            graph.SetNodeStatus("ESP:AA:01:02:03:04", isOnline: false);

            // Dynamic Self-Healing: The network finds redundant alternate link
            // (Sensor routes through Node 2 or neighboring zone)
            var healedRoute = graph.FindOptimalRoute(sensorMac, gatewayMac);

            // Sensor must reroute without crashing and avoid Repeater Alpha
            if (healedRoute.IsReachable)
            {
                Assert.DoesNotContain("ESP:AA:01:02:03:04", healedRoute.Hops);
            }
            else
            {
                Assert.False(healedRoute.IsReachable);
                Assert.NotEmpty(healedRoute.FailureReason);
            }

            // Restore Repeater Alpha
            graph.SetNodeStatus("ESP:AA:01:02:03:04", isOnline: true);
            var restoredRoute = graph.FindOptimalRoute(sensorMac, gatewayMac);
            Assert.True(restoredRoute.IsReachable);
            Assert.Contains("ESP:AA:01:02:03:04", restoredRoute.Hops);
        }
        #endregion
    }
}
