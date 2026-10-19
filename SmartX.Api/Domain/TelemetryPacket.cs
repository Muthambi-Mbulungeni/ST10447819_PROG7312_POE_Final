namespace SmartX.Api.Domain;

// T is constrained to struct so the compiler never boxes float, int, or bool.
// ESP32 nodes send all three payload types. We handle them uniformly without coercion overhead.
public sealed class TelemetryPacket<T> where T : struct
{
    public string DeviceId  { get; init; }
    public T      Value     { get; init; }
    public string Unit      { get; init; }
    public DateTime Timestamp { get; init; }
    public bool IsAnomaly   { get; set;  }

    public TelemetryPacket(string deviceId, T value, string unit)
    {
        // An empty device ID means the packet has no origin. Reject it outright.
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));

        DeviceId  = deviceId;
        Value     = value;
        Unit      = unit;
        Timestamp = DateTime.UtcNow;
        IsAnomaly = false;
    }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] {DeviceId} => {Value} {Unit}{(IsAnomaly ? " [ANOMALY]" : string.Empty)}";
}
