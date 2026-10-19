using SmartX.Api.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace SmartX.Api.Services;

/// <summary>
/// Background service that continuously generates simulated sensor telemetry.
/// Every tick it feeds data into:
///   • TelemetryStore  (Part 1 — IngestionQueue per device)
///   • CommandService  (Part 2 — Queue/PriorityQueue + DeviceDirectory)
/// </summary>
public class TelemetryGeneratorService : BackgroundService
{
    private readonly TelemetryStore  _store;
    private readonly SensorRegistry  _registry;
    private readonly CommandService  _commandService;
    private readonly Random          _rng = new(Guid.NewGuid().GetHashCode());

    public TelemetryGeneratorService(
        TelemetryStore store,
        SensorRegistry registry,
        CommandService commandService)
    {
        _store          = store;
        _registry       = registry;
        _commandService = commandService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var profiles = _registry.GetAll();

            foreach (var profile in profiles)
            {
                var mac = profile.MacAddress;

                (float baseline, string unit) = profile.Category switch
                {
                    SensorCategory.Environmental    => (1013.25f, "hPa"),
                    SensorCategory.PowerConsumption => (220.0f,   "W"),
                    SensorCategory.Actuator         => (1.0f,     "state"),
                    _                               => (0.0f,     "raw")
                };

                // Generate a batch of readings
                float[] batch = new float[5];
                bool hasCritical = false;

                for (int i = 0; i < batch.Length; i++)
                {
                    float jitter = (float)(_rng.NextDouble() * 10.0 - 5.0);
                    batch[i] = baseline + jitter;

                    // 2% chance to produce a critical spike
                    if (_rng.NextDouble() < 0.02)
                    {
                        batch[i]    = baseline * (float)(_rng.NextDouble() * 3.0 + 2.0);
                        hasCritical = true;
                    }
                }

                // ── Part 1: feed the IngestionQueue via TelemetryStore (batch overload) ──
                _store.Ingest(mac, batch, unit);
                // Flush the staged batch into the store's typed queue so readings appear
                _store.FlushBuffer(mac, unit);

                // ── Part 2: feed the processing Queue / PriorityQueue ──
                float representativeValue = batch[batch.Length - 1];
                bool  isAnomaly           = hasCritical || Math.Abs(representativeValue - baseline) > baseline * 0.4f;

                var packet = new IncomingPacket(
                    DeviceId:   mac,
                    Value:      representativeValue,
                    Unit:       unit,
                    Category:   profile.Category.ToString(),
                    IsCritical: isAnomaly,
                    Priority:   isAnomaly ? 0 : 2,
                    ReceivedAt: DateTime.UtcNow);

                _commandService.IngestPacket(packet);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
