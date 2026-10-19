namespace SmartX.Api.Contracts;

using SmartX.Api.Domain;

public record RegisterSensorRequest(
    string         MacAddress,
    string         Zone,
    string         Room,
    string         NodeId,
    SensorCategory Category
);
