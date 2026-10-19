namespace SmartX.Api.Domain;

// Value type. Two readings on the call stack cost the same as two floats.
// Operator overloading lets the calling code express aggregation and delta cleanly
// without allocating intermediate objects: Meter3 = Meter1 + Meter2.
public readonly record struct SensorReading(string DeviceId, float Value)
{
    // Aggregate load across two meters. The resulting device ID signals it is a computed value.
    public static SensorReading operator +(SensorReading a, SensorReading b)
        => new("AGGREGATE", a.Value + b.Value);

    // Delta between readings. Useful for spotting drift or sudden spikes.
    public static SensorReading operator -(SensorReading a, SensorReading b)
        => new("DELTA", a.Value - b.Value);

    public static bool operator >(SensorReading a, SensorReading b)  => a.Value > b.Value;
    public static bool operator <(SensorReading a, SensorReading b)  => a.Value < b.Value;
    public static bool operator >=(SensorReading a, SensorReading b) => a.Value >= b.Value;
    public static bool operator <=(SensorReading a, SensorReading b) => a.Value <= b.Value;
}
