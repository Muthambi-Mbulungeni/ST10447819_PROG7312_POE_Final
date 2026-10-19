namespace SmartX.Api.Domain;

// ============================================================
// HASHSET<T> REQUIREMENT — Part 2
// ============================================================
// Maintains a set of currently active, UNIQUE alert keys.
// Because HashSet<T> only stores unique elements, calling
// AddAlert("NODE-Zone-A-001:ANOMALY") twice will only ever
// store that key once — the UI can never show duplicate alerts
// for the same sensor, even if the generator fires many times.
//
// Use-case: preventing duplicate "disconnected node" banners
// from spamming the dashboard when 100 packets arrive in a
// burst for the same failing sensor.
// ============================================================
public sealed class AlertTracker
{
    // Case-insensitive so "node-001:ANOMALY" == "NODE-001:anomaly"
    private readonly HashSet<string> _activeAlerts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();

    public int Count { get { lock (_gate) return _activeAlerts.Count; } }

    /// <summary>
    /// Adds an alert key. Returns true if the alert was new (not already tracked).
    /// Returns false if the set already contained this key — duplicate suppressed.
    /// </summary>
    public bool AddAlert(string alertKey)
    {
        if (string.IsNullOrWhiteSpace(alertKey)) return false;
        lock (_gate)
            return _activeAlerts.Add(alertKey); // HashSet.Add returns false on duplicate
    }

    /// <summary>Resolves (removes) an alert. Returns true if it was actually present.</summary>
    public bool ResolveAlert(string alertKey)
    {
        if (string.IsNullOrWhiteSpace(alertKey)) return false;
        lock (_gate)
            return _activeAlerts.Remove(alertKey);
    }

    /// <summary>O(1) existence check via hash — no linear scan needed.</summary>
    public bool IsActive(string alertKey)
    {
        if (string.IsNullOrWhiteSpace(alertKey)) return false;
        lock (_gate)
            return _activeAlerts.Contains(alertKey);
    }

    /// <summary>Thread-safe snapshot of all current unique active alerts.</summary>
    public IReadOnlyCollection<string> GetAllActive()
    {
        lock (_gate)
            return _activeAlerts.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
