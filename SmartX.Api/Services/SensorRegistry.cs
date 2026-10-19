namespace SmartX.Api.Services;

using SmartX.Api.Domain;

// Thread-safe in-memory sensor registry keyed by normalized MAC address.
// Every public method acquires the gate before touching the dictionary.
public sealed class SensorRegistry
{
    private readonly Dictionary<string, SensorProfile> _store = new();
    private readonly object _gate = new();

    public bool Register(SensorProfile profile)
    {
        if (profile is null) return false;

        lock (_gate)
        {
            if (_store.ContainsKey(profile.MacAddress)) return false;
            _store[profile.MacAddress] = profile;
            return true;
        }
    }

    public SensorProfile? Get(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return null;

        lock (_gate)
            return _store.TryGetValue(macAddress.ToUpperInvariant().Trim(), out var p) ? p : null;
    }

    public IReadOnlyList<SensorProfile> GetAll()
    {
        lock (_gate)
            return _store.Values.ToList();
    }

    public bool Exists(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return false;
        lock (_gate)
            return _store.ContainsKey(macAddress.ToUpperInvariant().Trim());
    }
}
