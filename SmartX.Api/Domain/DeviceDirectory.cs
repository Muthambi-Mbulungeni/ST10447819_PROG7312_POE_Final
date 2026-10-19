namespace SmartX.Api.Domain;

// ============================================================
// DICTIONARY<TKey,TValue> REQUIREMENT — Part 2
// ============================================================
// Dictionary<string, DeviceState> stores one entry per device,
// keyed by MAC address. Lookups are O(1) — when a new telemetry
// packet arrives we instantly find the device without scanning
// any list, regardless of how many devices are registered.
//
// SORTED DICTIONARY<TKey,TValue> REQUIREMENT — Part 2
// ============================================================
// SortedDictionary<DateTime, List<ReadingEntry>> stores all
// historical readings. Because SortedDictionary keeps its keys
// in ascending order at all times, the frontend can render a
// timeline graph by simply iterating the dictionary — no
// post-sort required, and inserts remain O(log n).
// ============================================================
public sealed class DeviceDirectory
{
    // O(1) device state lookup keyed by normalized MAC address
    private readonly Dictionary<string, DeviceState> _devices =
        new(StringComparer.OrdinalIgnoreCase);

    // Chronologically ordered historical readings for timeline graphs
    private readonly SortedDictionary<DateTime, List<ReadingEntry>> _timeline = new();

    private readonly object _gate = new();

    /// <summary>
    /// Upserts the live state of a device into the O(1) dictionary and
    /// appends a timestamped reading to the sorted timeline.
    /// </summary>
    public void UpsertDevice(
        string mac, string nodeId, string category,
        float lastValue, string unit, bool isAnomaly)
    {
        if (string.IsNullOrWhiteSpace(mac)) return;

        var now = DateTime.UtcNow;

        lock (_gate)
        {
            // Dictionary upsert — O(1) average
            _devices[mac] = new DeviceState(mac, nodeId, category, lastValue, unit, isAnomaly, now);

            // SortedDictionary insert — O(log n), keys auto-sorted by DateTime
            // Use a key with microsecond precision to avoid collisions
            var key = now;
            if (_timeline.ContainsKey(key))
                key = key.AddTicks(1); // nudge to guarantee uniqueness

            _timeline[key] = new List<ReadingEntry>
            {
                new ReadingEntry(mac, nodeId, lastValue, unit, isAnomaly)
            };

            // Cap timeline at 2000 entries to bound memory
            while (_timeline.Count > 2000)
                _timeline.Remove(_timeline.Keys.First());
        }
    }

    // ── Dictionary lookups (O(1)) ──────────────────────────────

    /// <summary>O(1) lookup by MAC address via the underlying Dictionary.</summary>
    public DeviceState? GetDevice(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        lock (_gate)
            return _devices.TryGetValue(mac, out var d) ? d : null;
    }

    public IReadOnlyList<DeviceState> GetAllDevices()
    {
        lock (_gate)
            return _devices.Values.ToList();
    }

    public IReadOnlyList<DeviceState> GetByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return Array.Empty<DeviceState>();
        lock (_gate)
            return _devices.Values
                .Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    public IReadOnlyList<DeviceState> GetAnomalous()
    {
        lock (_gate)
            return _devices.Values.Where(d => d.IsAnomaly).ToList();
    }

    // ── SortedDictionary timeline (O(log n) insert, O(1) ordered iteration) ──

    /// <summary>
    /// Returns the last <paramref name="lastN"/> timeline entries in ascending
    /// timestamp order. Because SortedDictionary keeps keys sorted, no explicit
    /// sort is ever needed here.
    /// </summary>
    public IReadOnlyList<TimelineEntry> GetTimeline(int lastN = 150)
    {
        lock (_gate)
        {
            return _timeline
                .TakeLast(lastN)
                .SelectMany(kv =>
                    kv.Value.Select(r =>
                        new TimelineEntry(kv.Key, r.Mac, r.NodeId, r.Value, r.Unit, r.IsAnomaly)))
                .ToList();
        }
    }
}

// ── Supporting records ──────────────────────────────────────────────────────

public sealed record DeviceState(
    string   Mac,
    string   NodeId,
    string   Category,
    float    LastValue,
    string   Unit,
    bool     IsAnomaly,
    DateTime LastSeen);

public sealed record ReadingEntry(
    string Mac,
    string NodeId,
    float  Value,
    string Unit,
    bool   IsAnomaly);

public sealed record TimelineEntry(
    DateTime Timestamp,
    string   Mac,
    string   NodeId,
    float    Value,
    string   Unit,
    bool     IsAnomaly);
