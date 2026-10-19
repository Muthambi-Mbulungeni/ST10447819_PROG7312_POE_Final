namespace SmartX.Api.Domain;

/// <summary>
/// Represents a raw telemetry packet arriving at the API boundary,
/// before it is routed into either the standard Queue or the PriorityQueue.
/// Priority = 0 means CRITICAL (processed first), 1 = WARNING, 2 = NORMAL.
/// </summary>
public sealed record IncomingPacket(
    string   DeviceId,
    string   NodeId,
    float    Value,
    string   Unit,
    string   Category,
    bool     IsCritical,
    int      Priority,      // lower = more urgent for PriorityQueue<T, int>
    DateTime ReceivedAt);
