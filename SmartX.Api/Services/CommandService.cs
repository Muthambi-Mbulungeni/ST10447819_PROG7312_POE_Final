namespace SmartX.Api.Services;

using SmartX.Api.Domain;

/// <summary>
/// Central service for the Real-Time Command Stream.
/// Aggregates all Part 2 data structures:
///   • TelemetryProcessingQueue  (Queue&lt;T&gt; + PriorityQueue&lt;T,int&gt;)
///   • CommandStack              (Stack&lt;T&gt;)
///   • AlertTracker              (HashSet&lt;T&gt;)
///   • DeviceDirectory           (Dictionary + SortedDictionary) — injected
///   • PredictionEngine          (pattern analysis)              — injected
/// </summary>
public sealed class CommandService
{
    private readonly CommandStack              _history     = new(200);
    private readonly TelemetryProcessingQueue  _queue       = new(500);
    private readonly AlertTracker              _alerts      = new();
    private readonly DeviceDirectory           _directory;
    private readonly PredictionEngine          _prediction;

    public CommandService(DeviceDirectory directory, PredictionEngine prediction)
    {
        _directory  = directory;
        _prediction = prediction;
    }

    // ── Telemetry ingestion (Queue / PriorityQueue) ─────────────

    /// <summary>
    /// Routes an incoming packet to the appropriate queue based on whether
    /// it represents a critical alert or a normal reading.
    /// Also updates the DeviceDirectory and, if critical, adds to the HashSet.
    /// </summary>
    public void IngestPacket(IncomingPacket packet)
    {
        if (packet is null) return;

        if (packet.IsCritical)
        {
            _queue.EnqueueCritical(packet, packet.Priority);

            // Add unique alert to HashSet — duplicates silently ignored by HashSet semantics
            string alertKey = $"{packet.DeviceId}:{(packet.Priority == 0 ? "CRITICAL" : "WARNING")}";
            _alerts.AddAlert(alertKey);
        }
        else
        {
            _queue.Enqueue(packet);
        }

        // Keep DeviceDirectory (Dictionary O(1) + SortedDictionary timeline) in sync
        _directory.UpsertDevice(
            packet.DeviceId, packet.NodeId, packet.Category,
            packet.Value, packet.Unit, packet.IsCritical);
    }

    public (IReadOnlyList<IncomingPacket> Standard, IReadOnlyList<IncomingPacket> Critical)
        GetQueueSnapshot() =>
        (_queue.StandardSnapshot(), _queue.CriticalSnapshot());

    public (int Standard, int Critical) GetQueueDepths() =>
        (_queue.StandardCount, _queue.CriticalCount);

    // ── Command history (Stack) ─────────────────────────────────

    /// <summary>Validates and pushes a new command onto the history Stack.</summary>
    public CommandRecord SendCommand(string deviceId, string commandTypeStr, string payload)
    {
        if (!Enum.TryParse<CommandType>(commandTypeStr, ignoreCase: true, out var cmdType))
            cmdType = CommandType.ManualOverride;

        var record = new CommandRecord(
            Id:       Guid.NewGuid().ToString("N")[..8].ToUpper(),
            DeviceId: deviceId,
            Type:     cmdType,
            Payload:  payload,
            IssuedAt: DateTime.UtcNow);

        _history.Push(record);

        // Inform the prediction engine about this command in context
        var anomalies = _directory.GetAnomalous();
        _prediction.RecordCommand(record, anomalies);

        return record;
    }

    /// <summary>Pops the most-recent command (Undo). Returns null if history is empty.</summary>
    public CommandRecord? UndoLastCommand() => _history.Pop();

    /// <summary>Returns the full command history snapshot (most-recent first).</summary>
    public IReadOnlyList<CommandRecord> GetCommandHistory() => _history.Snapshot();

    // ── Alert management (HashSet) ──────────────────────────────

    public bool ResolveAlert(string alertKey)            => _alerts.ResolveAlert(alertKey);
    public IReadOnlyCollection<string> GetActiveAlerts() => _alerts.GetAllActive();

    // ── Search recording + predictions ──────────────────────────

    public void RecordSearch(string query) => _prediction.RecordSearch(query);

    public IReadOnlyList<PredictionResult> GetPredictions()
    {
        var anomalies = _directory.GetAnomalous();
        var alerts    = _alerts.GetAllActive();
        return _prediction.GetRecommendations(anomalies, alerts);
    }

    // ── Device directory (Dictionary + SortedDictionary) ────────

    public IReadOnlyList<DeviceState>   GetAllDevices()                              => _directory.GetAllDevices();
    public IReadOnlyList<DeviceState>   GetDevicesByCategory(string cat)             => _directory.GetByCategory(cat);
    public IReadOnlyList<DeviceState>   GetAnomalousDevices()                        => _directory.GetAnomalous();
    public IReadOnlyList<DeviceState>   SearchDevices(string? query, bool? anomOnly) => _directory.Search(query, anomOnly);
    public IReadOnlyList<TimelineEntry> GetTimeline(int n = 100)                     => _directory.GetTimeline(n);
}
