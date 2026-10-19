namespace SmartX.Api.Domain;

/// <summary>
/// Represents the type of manual override command an operator can issue to a device.
/// </summary>
public enum CommandType
{
    TriggerActuator,
    ShutdownDevice,
    RestartDevice,
    AdjustThreshold,
    ManualOverride
}

/// <summary>
/// An immutable record of a single command issued by an operator.
/// Stored inside the Stack&lt;CommandRecord&gt; in CommandStack for undo functionality.
/// </summary>
public sealed record CommandRecord(
    string      Id,
    string      DeviceId,
    CommandType Type,
    string      Payload,
    DateTime    IssuedAt,
    string      IssuedBy = "Operator");
