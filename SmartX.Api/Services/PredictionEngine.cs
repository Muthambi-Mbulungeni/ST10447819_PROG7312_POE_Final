namespace SmartX.Api.Services;

using SmartX.Api.Domain;

// ============================================================
// PREDICTIVE ACTION & RECOMMENDATION ENGINE — Part 2 (30 Marks)
// ============================================================
// The engine performs two types of analysis:
//
// 1. PATTERN ANALYSIS (search & command frequency)
//    Uses a Dictionary<string, int> to count how often each
//    device category is searched. Detects "frequently monitored"
//    categories and surfaces proactive suggestions.
//
// 2. ANOMALY-COMMAND CORRELATION
//    Uses a nested Dictionary<anomalyKey, Dictionary<commandKey, count>>
//    to track which commands operators typically issue when a
//    specific category shows an anomaly.
//    e.g. "Whenever PowerConsumption is anomalous, you usually
//    trigger ManualOverride on AA:BB:CC..." → suggested action.
//
// 3. ACTIVE ALERT REMEDIATION
//    For every unresolved alert in the HashSet, the engine
//    recommends a targeted remediation action.
// ============================================================
public sealed class PredictionEngine
{
    // Search frequency: category → count of times it was searched
    private readonly Dictionary<string, int> _searchFrequency =
        new(StringComparer.OrdinalIgnoreCase);

    // Command-pattern tracking: anomalyStateKey → { commandKey → count }
    private readonly Dictionary<string, Dictionary<string, int>> _commandPatterns = new();

    // Bounded queue of recent pattern events for time-window analysis
    private readonly Queue<PatternEvent> _recentEvents = new();

    private readonly object _gate = new();
    private const int MaxRecentEvents = 500;

    // ── Recording ───────────────────────────────────────────────

    /// <summary>
    /// Called whenever the user searches / filters by a category.
    /// Increments the frequency counter in the Dictionary.
    /// </summary>
    public void RecordSearch(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        lock (_gate)
        {
            _searchFrequency[category] =
                _searchFrequency.GetValueOrDefault(category, 0) + 1;
        }
    }

    /// <summary>
    /// Called whenever an operator issues a command while certain devices are anomalous.
    /// Correlates the command with the current anomaly state for future recommendations.
    /// </summary>
    public void RecordCommand(CommandRecord command, IReadOnlyList<DeviceState> currentAnomalies)
    {
        if (command is null) return;
        lock (_gate)
        {
            foreach (var anomaly in currentAnomalies)
            {
                string stateKey  = $"{anomaly.Category}:anomaly";
                string cmdKey    = $"{command.Type}:{command.DeviceId}";

                if (!_commandPatterns.ContainsKey(stateKey))
                    _commandPatterns[stateKey] = new Dictionary<string, int>();

                _commandPatterns[stateKey][cmdKey] =
                    _commandPatterns[stateKey].GetValueOrDefault(cmdKey, 0) + 1;
            }

            // Keep event log bounded
            if (_recentEvents.Count >= MaxRecentEvents)
                _recentEvents.Dequeue();

            _recentEvents.Enqueue(
                new PatternEvent(command.DeviceId, command.Type.ToString(), DateTime.UtcNow));
        }
    }

    // ── Recommendation generation ───────────────────────────────

    /// <summary>
    /// Produces a ranked list of predictive recommendations based on:
    ///   • Current anomalous devices vs historical command patterns
    ///   • Most-searched categories
    ///   • Unresolved active alerts
    /// </summary>
    public IReadOnlyList<PredictionResult> GetRecommendations(
        IReadOnlyList<DeviceState>         currentAnomalies,
        IReadOnlyCollection<string>        activeAlerts)
    {
        var results = new List<PredictionResult>();

        lock (_gate)
        {
            // ── 1. Anomaly-command pattern matching ──────────────────
            foreach (var anomaly in currentAnomalies.Take(5))
            {
                string stateKey = $"{anomaly.Category}:anomaly";

                if (_commandPatterns.TryGetValue(stateKey, out var cmds) && cmds.Count > 0)
                {
                    var top = cmds.OrderByDescending(kv => kv.Value).First();
                    var parts = top.Key.Split(':', 2);
                    string cmdType   = parts.Length > 0 ? parts[0] : top.Key;
                    string targetDev = parts.Length > 1 ? parts[1] : "N/A";

                    results.Add(new PredictionResult(
                        Title:         $"⚡ Anomaly on {anomaly.NodeId}",
                        Description:   $"Historical pattern: when {anomaly.Category} is anomalous you typically issue '{cmdType}' on {targetDev} (×{top.Value}). Consider acting now.",
                        InsightType:   "Pattern Match",
                        RelatedDevice: anomaly.NodeId,
                        Priority:      0));
                }
                else
                {
                    // No history yet — generic recommendation
                    results.Add(new PredictionResult(
                        Title:         $"⚠ Anomaly Detected: {anomaly.NodeId}",
                        Description:   $"Value {anomaly.LastValue:F2} {anomaly.Unit} is outside normal bounds. Consider issuing a ManualOverride or RestartDevice command.",
                        InsightType:   "Automated Insight",
                        RelatedDevice: anomaly.NodeId,
                        Priority:      1));
                }
            }

            // ── 2. Search behaviour insight ──────────────────────────
            if (_searchFrequency.Count > 0)
            {
                var topSearch = _searchFrequency
                    .OrderByDescending(kv => kv.Value)
                    .First();

                results.Add(new PredictionResult(
                    Title:         $"🔍 High Monitoring Frequency: {topSearch.Key}",
                    Description:   $"Category '{topSearch.Key}' has been searched {topSearch.Value}× — consider setting an automated threshold alert for this category.",
                    InsightType:   "Behaviour Insight",
                    RelatedDevice: topSearch.Key,
                    Priority:      2));
            }

            // ── 3. Active alert remediations ─────────────────────────
            foreach (var alert in activeAlerts.Take(3))
            {
                results.Add(new PredictionResult(
                    Title:         $"🚨 Unresolved Alert: {alert}",
                    Description:   "This alert has not been resolved. Recommended action: issue a RestartDevice or ManualOverride command to the affected node.",
                    InsightType:   "Alert Remediation",
                    RelatedDevice: alert,
                    Priority:      0));
            }

            // ── 4. Burst detection from recent event queue ───────────
            var now   = DateTime.UtcNow;
            var burst = _recentEvents
                .Where(e => (now - e.OccurredAt).TotalSeconds < 30)
                .GroupBy(e => e.DeviceId)
                .Where(g => g.Count() >= 3)
                .ToList();

            foreach (var group in burst.Take(2))
            {
                results.Add(new PredictionResult(
                    Title:         $"🔄 Command Burst on {group.Key}",
                    Description:   $"{group.Count()} commands issued to {group.Key} in the last 30 seconds. Possible runaway automation — review and consider a Shutdown.",
                    InsightType:   "Burst Warning",
                    RelatedDevice: group.Key,
                    Priority:      1));
            }
        }

        // Return sorted by priority (0 = most urgent)
        return results.OrderBy(r => r.Priority).ToList();
    }

    public IReadOnlyDictionary<string, int> GetSearchFrequency()
    {
        lock (_gate)
            return _searchFrequency.ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public sealed record PatternEvent(
    string   DeviceId,
    string   CommandTypeName,
    DateTime OccurredAt);

public sealed record PredictionResult(
    string Title,
    string Description,
    string InsightType,
    string RelatedDevice,
    int    Priority);
