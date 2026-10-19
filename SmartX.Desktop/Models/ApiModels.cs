using System.Collections.Generic;

namespace SmartX.Desktop.Models;

// ── Part 1 Models (unchanged) ──────────────────────────────────────────────

public class SensorProfile
{
    public string MacAddress          { get; set; } = string.Empty;
    public string NodeId              { get; set; } = string.Empty;
    public string Zone                { get; set; } = string.Empty;
    public string Room                { get; set; } = string.Empty;
    public int    Category            { get; set; }
    public string? FirmwareManifestPath { get; set; }
}

public class HeatmapEntry
{
    public string   NodeId    { get; set; } = string.Empty;
    public string   Zone      { get; set; } = string.Empty;
    public string   Room      { get; set; } = string.Empty;
    public double   LastValue { get; set; }
    public double   Sigma     { get; set; }
    public bool     IsAnomaly { get; set; }
    public string   Status    { get; set; } = string.Empty;
    public double[] History   { get; set; } = System.Array.Empty<double>();
}

public class ValidationResult
{
    public bool         IsValid     { get; set; }
    public List<string> FailedPaths { get; set; } = new();
}

// ── Part 2 Models ──────────────────────────────────────────────────────────

/// <summary>Mirrors API's IncomingPacket — a raw packet in the Queue or PriorityQueue.</summary>
public class IncomingPacketModel
{
    public string   DeviceId   { get; set; } = string.Empty;
    public float    Value      { get; set; }
    public string   Unit       { get; set; } = string.Empty;
    public string   Category   { get; set; } = string.Empty;
    public bool     IsCritical { get; set; }
    public int      Priority   { get; set; }
    public string   ReceivedAt { get; set; } = string.Empty;
}

/// <summary>Mirrors API's CommandRecord — one entry in the Stack history.</summary>
public class CommandRecordModel
{
    public string Id        { get; set; } = string.Empty;
    public string DeviceId  { get; set; } = string.Empty;
    public string Type      { get; set; } = string.Empty;
    public string Payload   { get; set; } = string.Empty;
    public string IssuedAt  { get; set; } = string.Empty;
    public string IssuedBy  { get; set; } = string.Empty;
}

/// <summary>Mirrors API's DeviceState — live device from the Dictionary.</summary>
public class DeviceStateModel
{
    public string Mac       { get; set; } = string.Empty;
    public string NodeId    { get; set; } = string.Empty;
    public string Category  { get; set; } = string.Empty;
    public float  LastValue { get; set; }
    public string Unit      { get; set; } = string.Empty;
    public bool   IsAnomaly { get; set; }
    public string LastSeen  { get; set; } = string.Empty;
}

/// <summary>Mirrors API's PredictionResult — one recommendation from the engine.</summary>
public class PredictionResultModel
{
    public string Title         { get; set; } = string.Empty;
    public string Description   { get; set; } = string.Empty;
    public string InsightType   { get; set; } = string.Empty;
    public string RelatedDevice { get; set; } = string.Empty;
    public int    Priority      { get; set; }
}

/// <summary>Wraps the /api/commands/stream response envelope.</summary>
public class CommandStreamResponse
{
    public int                      StandardCount { get; set; }
    public int                      CriticalCount { get; set; }
    public List<IncomingPacketModel> Standard     { get; set; } = new();
    public List<IncomingPacketModel> Critical     { get; set; } = new();
}
