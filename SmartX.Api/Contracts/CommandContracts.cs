namespace SmartX.Api.Contracts;

/// <summary>Request body for the POST /api/commands/send endpoint.</summary>
public sealed record SendCommandRequest(
    string DeviceId,
    string CommandType,  // maps to CommandType enum
    string Payload);

/// <summary>Request body for the POST /api/commands/simulate-alert endpoint.</summary>
public sealed record SimulateAlertRequest(
    string  DeviceId,
    float   Value,
    string? Unit     = null,
    string? Category = null);

/// <summary>Query parameters for device filtering.</summary>
public sealed record FilterDevicesQuery(
    string? Category    = null,
    bool?   AnomalyOnly = null);
