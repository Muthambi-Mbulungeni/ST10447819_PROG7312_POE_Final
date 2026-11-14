namespace SmartX.Api.Services;

using SmartX.Api.Domain;

/// <summary>
/// SensorRegistry acts as a domain service over DeviceDirectory, ensuring a
/// SINGLE SOURCE OF TRUTH for all device profiles and states without duplicate in-memory dictionaries.
/// </summary>
public sealed class SensorRegistry
{
    private readonly DeviceDirectory _directory;

    public SensorRegistry(DeviceDirectory directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public bool Register(SensorProfile profile) => _directory.RegisterProfile(profile);

    public SensorProfile? Get(string macAddress) => _directory.GetProfile(macAddress);

    public IReadOnlyList<SensorProfile> GetAll() => _directory.GetAllProfiles();

    public bool Exists(string macAddress) => _directory.ProfileExists(macAddress);
}
