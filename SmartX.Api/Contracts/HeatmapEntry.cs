namespace SmartX.Api.Contracts;

public record HeatmapEntry(
    string   DeviceId,
    float    LastValue,
    string   Unit,
    bool     IsAnomaly,
    string   Status,
    DateTime LastSeen,
    float[]  History
);
