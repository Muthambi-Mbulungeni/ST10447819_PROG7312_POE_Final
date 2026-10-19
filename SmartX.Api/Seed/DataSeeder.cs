namespace SmartX.Api.Seed;

using SmartX.Api.Domain;
using SmartX.Api.Services;

// Seeds 50 mock sensor profiles across 5 zones, 3 categories.
// Each sensor gets 40 readings of clean baseline data followed by 2 deliberate
// spike anomalies so the 2-sigma detector has something to flag from the start.
public static class DataSeeder
{
    private static readonly Random Rng = new(42);

    public static void Seed(SensorRegistry registry, TelemetryStore store)
    {
        var zones = new[] { "Zone-A", "Zone-B", "Zone-C", "Zone-D", "Zone-E" };
        var rooms = new[] { "Room-01", "Room-02", "Room-03", "Room-04" };
        var categories = Enum.GetValues<SensorCategory>();

        int index = 0;

        foreach (string zone in zones)
        {
            foreach (string room in rooms)
            {
                foreach (SensorCategory cat in categories)
                {
                    if (index >= 50) return;

                    string mac    = BuildMac(index);
                    string nodeId = $"NODE-{zone}-{index:D3}";

                    var profile = new SensorProfile(mac, zone, room, nodeId, cat);
                    registry.Register(profile);
                    SeedReadings(store, mac, cat);

                    index++;
                }
            }
        }
    }

    private static void SeedReadings(TelemetryStore store, string deviceId, SensorCategory cat)
    {
        (float baseline, string unit) = cat switch
        {
            SensorCategory.Environmental    => (1013.25f, "hPa"),
            SensorCategory.PowerConsumption => (220.0f,   "W"),
            SensorCategory.Actuator         => (1.0f,     "state"),
            _                               => (0.0f,     "raw")
        };

        // 40 readings within tight normal variance to establish a clean mean.
        for (int i = 0; i < 40; i++)
        {
            float jitter = (float)(Rng.NextDouble() * 6.0 - 3.0);
            store.Ingest(deviceId, baseline + jitter, unit);
        }

        // Two readings that sit well outside the normal band to trigger anomaly flags.
        store.Ingest(deviceId, baseline * 3.2f, unit);
        store.Ingest(deviceId, baseline * 0.08f, unit);
    }

    // Build a stable, unique MAC from a zero-based index.
    private static string BuildMac(int index)
    {
        byte hi = (byte)((index >> 4) & 0xFF);
        byte lo = (byte)(index & 0x0F);
        return $"AA:BB:CC:DD:{hi:X2}:{lo:X2}";
    }
}
