namespace SmartX.Api.Services;

using SmartX.Api.Domain;

// Manages one IngestionQueue and one BatchBuffer per device.
// Anomaly detection uses mean +- 2 standard deviations against the device's own history.
// We need at least 10 readings before the check is meaningful.
public sealed class TelemetryStore
{
    private readonly Dictionary<string, IngestionQueue<float>> _queues  = new();
    private readonly Dictionary<string, BatchBuffer>           _buffers = new();
    private readonly object _gate = new();

    private const int QueueCapacity = 200;
    private const int BufferBatches = 50;

    public void Ingest(string deviceId, float value, string unit)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        lock (_gate)
        {
            EnsureDevice(deviceId);

            var packet = new TelemetryPacket<float>(deviceId, value, unit)
            {
                IsAnomaly = IsOutlier(deviceId, value)
            };

            _queues[deviceId].Enqueue(packet);
        }
    }

    // Method Overloading: Ingest a batch of readings at once
    public void Ingest(string deviceId, float[] readings, string unit)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || readings is null || readings.Length == 0) return;

        lock (_gate)
        {
            EnsureDevice(deviceId);
            _buffers[deviceId].WriteBatch(readings);
        }
    }

    // Flush moves every staged batch into the typed queue then returns the transferred packets.
    public List<TelemetryPacket<float>> FlushBuffer(string deviceId, string unit)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return new List<TelemetryPacket<float>>();

        lock (_gate)
        {
            if (!_buffers.ContainsKey(deviceId)) return new List<TelemetryPacket<float>>();

            var packets = _buffers[deviceId].Flush(deviceId, unit);
            foreach (var p in packets)
                _queues[deviceId].Enqueue(p);

            return packets;
        }
    }

    public IReadOnlyList<TelemetryPacket<float>> GetHistory(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return Array.Empty<TelemetryPacket<float>>();

        lock (_gate)
            return _queues.TryGetValue(deviceId, out var q)
                ? q.Snapshot()
                : Array.Empty<TelemetryPacket<float>>();
    }

    public TelemetryPacket<float>? GetLatest(string deviceId)
    {
        var history = GetHistory(deviceId);
        return history.Count > 0 ? history[history.Count - 1] : null;
    }

    public IReadOnlyList<string> GetKnownDevices()
    {
        lock (_gate)
            return _queues.Keys.ToList();
    }

    // Lazy initialization. First packet from a device creates its structures.
    private void EnsureDevice(string deviceId)
    {
        if (_queues.ContainsKey(deviceId)) return;
        _queues[deviceId]  = new IngestionQueue<float>(QueueCapacity);
        _buffers[deviceId] = new BatchBuffer(BufferBatches);
    }

    // Reading is an outlier when it falls outside mean +- 2 stddev of existing history.
    // The IngestionQueue lock is separate from _gate so there is no deadlock.
    private bool IsOutlier(string deviceId, float incoming)
    {
        var history = _queues[deviceId].Snapshot();
        if (history.Count < 10) return false;

        float mean     = history.Average(p => p.Value);
        float variance = history.Average(p => (p.Value - mean) * (p.Value - mean));
        float stddev   = (float)Math.Sqrt(variance);

        if (stddev < float.Epsilon) return false;

        return Math.Abs(incoming - mean) > 2.0f * stddev;
    }
}
