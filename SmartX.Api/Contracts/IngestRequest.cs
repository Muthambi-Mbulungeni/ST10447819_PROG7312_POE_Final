namespace SmartX.Api.Contracts;

public record IngestRequest(
    string DeviceId,
    float  Value,
    string Unit
);
