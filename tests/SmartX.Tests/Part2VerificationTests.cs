using System;
using System.Collections.Generic;
using SmartX.Api.Domain;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Tests
{
    /// <summary>
    /// Explicit unit tests directly verifying the lecturer feedback requirements for Part 2:
    /// 1. Priority lane served before standard lane
    /// 2. Bounded standard queue capacity rule (evicting oldest packet when full)
    /// 3. Command stack LIFO undo reversal
    /// 4. Single source of truth for devices and chronological sorted timeline
    /// 5. Dual HashSet for active alerts and disconnected nodes
    /// 6. Learned search-to-command follow-up with statistical confidence scoring
    /// </summary>
    public class Part2VerificationTests
    {
        #region 1. Priority Lane Served First (Lecturer Feedback #3)
        [Fact]
        public void PriorityQueue_ShouldServeCriticalPacketsBeforeStandardLane()
        {
            // Arrange: 500 capacity queue
            var queue = new TelemetryProcessingQueue(maxCapacity: 50);

            var standard1 = new IncomingPacket("DEV-01", "NODE-01", 10.0f, "C", "Environmental", false, 2, DateTime.UtcNow);
            var standard2 = new IncomingPacket("DEV-02", "NODE-02", 12.0f, "C", "Environmental", false, 2, DateTime.UtcNow);
            var standard3 = new IncomingPacket("DEV-03", "NODE-03", 14.0f, "C", "Environmental", false, 2, DateTime.UtcNow);

            var criticalSurge = new IncomingPacket("DEV-PWR-01", "NODE-PWR", 9999.0f, "W", "PowerConsumption", true, 0, DateTime.UtcNow);
            var highWarning = new IncomingPacket("DEV-PWR-02", "NODE-PWR-WARN", 4500.0f, "W", "PowerConsumption", true, 1, DateTime.UtcNow);

            // Act: Enqueue standard packets first
            queue.Enqueue(standard1);
            queue.Enqueue(standard2);
            queue.Enqueue(standard3);

            // Critical alert arrives later
            queue.EnqueueCritical(highWarning, priority: 1);
            queue.EnqueueCritical(criticalSurge, priority: 0); // priority 0 = most urgent

            // Assert: Dequeue must serve priority 0 first, then priority 1, then standard FIFO
            var firstOut = queue.Dequeue();
            var secondOut = queue.Dequeue();
            var thirdOut = queue.Dequeue();
            var fourthOut = queue.Dequeue();
            var fifthOut = queue.Dequeue();

            Assert.NotNull(firstOut);
            Assert.NotNull(secondOut);
            Assert.NotNull(thirdOut);

            // Critical (Priority 0) served first!
            Assert.Equal("DEV-PWR-01", firstOut.DeviceId);
            Assert.Equal(0, firstOut.Priority);

            // Warning (Priority 1) served second!
            Assert.Equal("DEV-PWR-02", secondOut.DeviceId);
            Assert.Equal(1, secondOut.Priority);

            // Standard packets emerge in FIFO order
            Assert.Equal("DEV-01", thirdOut.DeviceId);
            Assert.Equal("DEV-02", fourthOut?.DeviceId);
            Assert.Equal("DEV-03", fifthOut?.DeviceId);
        }
        #endregion

        #region 2. Bounded Capacity Rule (Lecturer Feedback #3)
        [Fact]
        public void StandardQueue_CapacityRule_ShouldDropOldestPacketWhenFull()
        {
            // Arrange: bounded capacity of exactly 5
            int capacity = 5;
            var queue = new TelemetryProcessingQueue(maxCapacity: capacity);

            for (int i = 1; i <= capacity; i++)
            {
                queue.Enqueue(new IncomingPacket($"DEV-{i}", $"NODE-{i}", i * 10f, "V", "Power", false, 2, DateTime.UtcNow));
            }

            Assert.Equal(capacity, queue.StandardCount);

            // Act: Enqueue 6th and 7th packets.
            // Per requirement: "drops the oldest standard packet when it is full"
            queue.Enqueue(new IncomingPacket("DEV-6", "NODE-6", 60f, "V", "Power", false, 2, DateTime.UtcNow));
            queue.Enqueue(new IncomingPacket("DEV-7", "NODE-7", 70f, "V", "Power", false, 2, DateTime.UtcNow));

            // Assert: Count should still be exactly 5
            Assert.Equal(capacity, queue.StandardCount);

            // Packets DEV-1 and DEV-2 must have been evicted.
            // DEV-3 must now be at the head of the queue!
            var next = queue.Dequeue();
            Assert.NotNull(next);
            Assert.Equal("DEV-3", next.DeviceId);

            var nextAfter = queue.Dequeue();
            Assert.Equal("DEV-4", nextAfter?.DeviceId);
        }
        #endregion

        #region 3. Command Stack Undo Reversibility (LIFO)
        [Fact]
        public void CommandStack_ShouldSupportUndoReversibility_LIFO()
        {
            // Arrange
            var stack = new CommandStack(maxSize: 100);
            var cmd1 = new CommandRecord("CMD-1", "ESP-01", CommandType.RestartDevice, "REBOOT", DateTime.UtcNow);
            var cmd2 = new CommandRecord("CMD-2", "ESP-02", CommandType.AdjustThreshold, "THRESHOLD_50", DateTime.UtcNow);
            var cmd3 = new CommandRecord("CMD-3", "ESP-03", CommandType.ShutdownDevice, "EMERGENCY_SHUTDOWN", DateTime.UtcNow);

            // Act
            stack.Push(cmd1);
            stack.Push(cmd2);
            stack.Push(cmd3);

            Assert.Equal(3, stack.Count);

            // Assert LIFO Pop (Undo)
            var undone1 = stack.Pop();
            Assert.NotNull(undone1);
            Assert.Equal("CMD-3", undone1.Id);
            Assert.Equal(CommandType.ShutdownDevice, undone1.Type);

            var undone2 = stack.Pop();
            Assert.NotNull(undone2);
            Assert.Equal("CMD-2", undone2.Id);

            var undone3 = stack.Pop();
            Assert.NotNull(undone3);
            Assert.Equal("CMD-1", undone3.Id);

            // Non-crashing empty pop
            var empty = stack.Pop();
            Assert.Null(empty);
            Assert.Equal(0, stack.Count);
        }
        #endregion

        #region 4. Single Source of Truth for Devices (Lecturer Feedback #4)
        [Fact]
        public void DeviceDirectory_ShouldActAsSingleSourceOfTruth_ForProfilesAndStates()
        {
            var directory = new DeviceDirectory();
            var registry = new SensorRegistry(directory);

            var profile = new SensorProfile(
                "AA:BB:CC:11:22:33",
                "Zone-Alpha",
                "Room-101",
                "NODE-ZA-01",
                SensorCategory.Environmental
            );

            // Act: Register through registry facade
            bool registered = registry.Register(profile);
            Assert.True(registered);

            // Check single source of truth in directory
            Assert.True(directory.ProfileExists("AA:BB:CC:11:22:33"));
            var fetchedProfile = directory.GetProfile("AA:BB:CC:11:22:33");
            Assert.NotNull(fetchedProfile);
            Assert.Equal("NODE-ZA-01", fetchedProfile.NodeId);

            // State is also managed in the same single source of truth
            var state = directory.GetDevice("AA:BB:CC:11:22:33");
            Assert.NotNull(state);

            // Upsert state and verify timeline
            directory.UpsertDevice("AA:BB:CC:11:22:33", "NODE-ZA-01", "Environmental", 24.5f, "C", false);
            var updatedState = directory.GetDevice("AA:BB:CC:11:22:33");
            Assert.Equal(24.5f, updatedState?.LastValue);

            var timeline = directory.GetTimeline(10);
            Assert.NotEmpty(timeline);
        }
        #endregion

        #region 5. Dual HashSet: Alerts and Disconnected Nodes (Lecturer Feedback #5)
        [Fact]
        public void AlertTracker_ShouldMaintainDualHashSets_DeduplicatingAlertsAndDisconnects()
        {
            var tracker = new AlertTracker();

            // 1. Active Alerts Set
            Assert.True(tracker.AddAlert("ESP-01:CRITICAL"));
            Assert.False(tracker.AddAlert("ESP-01:CRITICAL")); // Duplicate suppressed!
            Assert.Equal(1, tracker.Count);

            // 2. Disconnected Nodes Set (Second HashSet)
            Assert.True(tracker.MarkNodeDisconnected("ESP-01"));
            Assert.False(tracker.MarkNodeDisconnected("ESP-01")); // Duplicate disconnect suppressed!
            Assert.True(tracker.MarkNodeDisconnected("ESP-02"));
            Assert.Equal(2, tracker.DisconnectedCount);

            Assert.True(tracker.IsNodeDisconnected("ESP-01"));
            Assert.True(tracker.IsNodeDisconnected("ESP-02"));
            Assert.False(tracker.IsNodeDisconnected("ESP-03"));

            // Reconnecting node removes from second set
            Assert.True(tracker.MarkNodeReconnected("ESP-01"));
            Assert.False(tracker.IsNodeDisconnected("ESP-01"));
            Assert.Equal(1, tracker.DisconnectedCount);
        }
        #endregion

        #region 6. Learned Follow-Up Prediction Engine (Lecturer Feedback #6)
        [Fact]
        public void PredictionEngine_ShouldLinkSearchToNextCommand_AndComputeConfidence()
        {
            var engine = new PredictionEngine();

            // Step 1: User searches for "IrrigationFlow"
            engine.RecordSearch("IrrigationFlow");

            // Step 2: Operator subsequently issues command to increase pressure 4 times
            var cmdRestart = new CommandRecord("C1", "ESP-IRR-01", CommandType.RestartDevice, "REBOOT", DateTime.UtcNow);
            var cmdOverride = new CommandRecord("C2", "ESP-IRR-01", CommandType.ManualOverride, "VALVE_100", DateTime.UtcNow);

            engine.RecordCommand(cmdOverride, Array.Empty<DeviceState>());
            engine.RecordCommand(cmdOverride, Array.Empty<DeviceState>());
            engine.RecordCommand(cmdOverride, Array.Empty<DeviceState>());
            engine.RecordCommand(cmdRestart, Array.Empty<DeviceState>());

            // Total follow-up commands = 4. ManualOverride = 3 (75% confidence), RestartDevice = 1 (25% confidence)
            double overrideConfidence = engine.CalculateSearchCommandConfidence("IrrigationFlow", "ManualOverride");
            double restartConfidence = engine.CalculateSearchCommandConfidence("IrrigationFlow", "RestartDevice");

            Assert.Equal(0.75, overrideConfidence, precision: 2);
            Assert.Equal(0.25, restartConfidence, precision: 2);

            // Recommendations should include the learned follow-up
            var recs = engine.GetRecommendations(Array.Empty<DeviceState>(), Array.Empty<string>());
            Assert.Contains(recs, r => r.InsightType == "Learned Follow-Up" && r.Description.Contains("75% confidence"));
        }
        #endregion
    }
}
