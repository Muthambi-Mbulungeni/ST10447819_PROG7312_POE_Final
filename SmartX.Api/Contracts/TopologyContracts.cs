namespace SmartX.Api.Contracts;

public sealed record RouteRequest(
    string SourceMac,
    string DestinationMac);

public sealed record NodeStatusRequest(
    bool IsOnline);
