namespace SmartX.Api.Domain;

public enum SensorCategory
{
    Environmental,
    PowerConsumption,
    Actuator
}

// Immutable after registration. A sensor's identity does not change once it is on the network.
// The attached file list is the one mutable piece since files accumulate over the device lifetime.
public sealed class SensorProfile
{
    public string         MacAddress      { get; init; }
    public string         Zone            { get; init; }
    public string         Room            { get; init; }
    public string         NodeId          { get; init; }
    public SensorCategory Category        { get; init; }
    public DateTime       RegisteredAt    { get; init; }
    public List<string>   AttachedFiles   { get; }

    public SensorProfile(
        string macAddress,
        string zone,
        string room,
        string nodeId,
        SensorCategory category)
    {
        // MAC address is the primary identifier. Empty means we cannot route to this device.
        if (string.IsNullOrWhiteSpace(macAddress))
            throw new ArgumentException("MAC address cannot be empty.", nameof(macAddress));

        // Normalize to uppercase so AA:BB:CC and aa:bb:cc resolve to the same device.
        MacAddress    = macAddress.ToUpperInvariant().Trim();
        Zone          = zone;
        Room          = room;
        NodeId        = nodeId;
        Category      = category;
        RegisteredAt  = DateTime.UtcNow;
        AttachedFiles = new List<string>();
    }

    public void AttachFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        AttachedFiles.Add(filePath);
    }
}
