using SmartX.Api.Contracts;
using SmartX.Api.Domain;
using SmartX.Api.Seed;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Singleton services ─────────────────────────────────────────────────────
// Part 1 singletons (unchanged)
builder.Services.AddSingleton<SensorRegistry>();
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<DeploymentValidator>();
builder.Services.AddSingleton(_ =>
    new FileService(Path.Combine(Directory.GetCurrentDirectory(), "uploads")));

// Part 2 singletons — each wraps a distinct data structure
builder.Services.AddSingleton<DeviceDirectory>();   // Dictionary + SortedDictionary
builder.Services.AddSingleton<PredictionEngine>();  // Pattern analysis engine
builder.Services.AddSingleton<CommandService>();    // Stack, Queue, PriorityQueue, HashSet

// Background telemetry generator (now also feeds Part 2 structures)
builder.Services.AddHostedService<TelemetryGeneratorService>();

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()));

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartX API v1"));

// ── Seed ───────────────────────────────────────────────────────────────────
var registry  = app.Services.GetRequiredService<SensorRegistry>();
var telemetry = app.Services.GetRequiredService<TelemetryStore>();
DataSeeder.Seed(registry, telemetry);

// =============================================================================
// PART 1 ENDPOINTS (unchanged)
// =============================================================================

// Sensor Endpoints
app.MapGet("/api/sensors", (SensorRegistry reg) =>
    Results.Ok(reg.GetAll()))
    .WithTags("Part 1 – Sensors");

app.MapGet("/api/sensors/validate", (DeploymentValidator validator) =>
{
    var root  = new DeploymentNode("Facility-A", "FAC-001") { IsConfigured = true };
    var zone1 = new DeploymentNode("Zone-1",     "ZN-001")  { IsConfigured = true };
    var zone2 = new DeploymentNode("Zone-2",     "ZN-002")  { IsConfigured = true };
    var subA  = new DeploymentNode("Sub-Zone-A", "SZA-001") { IsConfigured = true };
    var subB  = new DeploymentNode("Sub-Zone-B", "SZB-001") { IsConfigured = true };

    zone1.AddChild(subA);
    zone1.AddChild(subB);
    root.AddChild(zone1);
    root.AddChild(zone2);

    return Results.Ok(validator.Validate(root));
}).WithTags("Part 1 – Sensors");

app.MapGet("/api/sensors/{mac}", (string mac, SensorRegistry reg) =>
{
    var profile = reg.Get(mac);
    return profile is not null ? Results.Ok(profile) : Results.NotFound();
}).WithTags("Part 1 – Sensors");

app.MapPost("/api/sensors/register", (RegisterSensorRequest req, SensorRegistry reg) =>
{
    if (string.IsNullOrWhiteSpace(req.MacAddress))
        return Results.BadRequest("MAC address is required.");

    var profile = new SensorProfile(req.MacAddress, req.Zone, req.Room, req.NodeId, req.Category);
    bool added  = reg.Register(profile);

    return added
        ? Results.Created($"/api/sensors/{profile.MacAddress}", profile)
        : Results.Conflict($"Sensor {req.MacAddress} is already registered.");
}).WithTags("Part 1 – Sensors");

app.MapPost("/api/sensors/{mac}/upload",
    async (string mac, IFormFile file, SensorRegistry reg, FileService fileService) =>
{
    var profile = reg.Get(mac);
    if (profile is null) return Results.NotFound($"Sensor {mac} not registered.");
    if (file is null || file.Length == 0) return Results.BadRequest("No file provided.");

    await using var stream = file.OpenReadStream();
    string savedPath = await fileService.SaveEncryptedAsync(mac, file.FileName, stream);
    profile.AttachFile(savedPath);

    return Results.Ok(new { mac, savedPath, encrypted = true, sizeBytes = file.Length });
}).DisableAntiforgery()
  .WithTags("Part 1 – Sensors");

// Telemetry Endpoints
app.MapGet("/api/telemetry/heatmap", (TelemetryStore store) =>
{
    var devices = store.GetKnownDevices();
    var entries = new List<HeatmapEntry>(devices.Count);

    foreach (string id in devices)
    {
        var latest = store.GetLatest(id);
        if (latest is null) continue;

        var historyList = store.GetHistory(id);
        string status   = latest.IsAnomaly      ? "ANOMALY"
                        : historyList.Count < 10 ? "INITIALISING"
                        : "NOMINAL";

        var historyArray = historyList.Select(x => x.Value).TakeLast(30).ToArray();
        entries.Add(new HeatmapEntry(id, latest.Value, latest.Unit, latest.IsAnomaly, status, latest.Timestamp, historyArray));
    }

    return Results.Ok(entries);
}).WithTags("Part 1 – Telemetry");

app.MapGet("/api/telemetry/{deviceId}", (string deviceId, TelemetryStore store) =>
    Results.Ok(store.GetHistory(deviceId)))
    .WithTags("Part 1 – Telemetry");

app.MapPost("/api/telemetry/ingest", (IngestRequest req, TelemetryStore store, SensorRegistry reg) =>
{
    if (!reg.Exists(req.DeviceId))
        return Results.NotFound($"Device {req.DeviceId} is not registered.");

    store.Ingest(req.DeviceId, req.Value, req.Unit);
    return Results.Ok(new { req.DeviceId, req.Value, req.Unit, Timestamp = DateTime.UtcNow });
}).WithTags("Part 1 – Telemetry");

// =============================================================================
// PART 2 ENDPOINTS
// =============================================================================

// ── Command Stream (Queue / PriorityQueue status) ──────────────────────────
app.MapGet("/api/commands/stream", (CommandService svc) =>
{
    var (standard, critical) = svc.GetQueueSnapshot();
    var (stdCount, critCount) = svc.GetQueueDepths();
    return Results.Ok(new
    {
        StandardCount  = stdCount,
        CriticalCount  = critCount,
        Standard       = standard.TakeLast(50),   // last 50 for the dashboard feed
        Critical       = critical
    });
}).WithTags("Part 2 – Command Stream");

// ── Issue a manual override command (Stack<T> push) ───────────────────────
app.MapPost("/api/commands/send", (SendCommandRequest req, CommandService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest("DeviceId is required.");

    var record = svc.SendCommand(req.DeviceId, req.CommandType, req.Payload);
    return Results.Ok(record);
}).WithTags("Part 2 – Command Stream");

// ── Undo last command (Stack<T> pop) ──────────────────────────────────────
app.MapPost("/api/commands/undo", (CommandService svc) =>
{
    var undone = svc.UndoLastCommand();
    return undone is not null
        ? Results.Ok(new { Undone = undone, Message = $"Command {undone.Id} reversed." })
        : Results.Ok(new { Message = "No commands in history to undo." });
}).WithTags("Part 2 – Command Stream");

// ── Command history (full Stack snapshot) ─────────────────────────────────
app.MapGet("/api/commands/history", (CommandService svc) =>
    Results.Ok(svc.GetCommandHistory()))
    .WithTags("Part 2 – Command Stream");

// ── Active alerts (HashSet<T> snapshot) ───────────────────────────────────
app.MapGet("/api/alerts/active", (CommandService svc) =>
    Results.Ok(svc.GetActiveAlerts()))
    .WithTags("Part 2 – Alerts");

app.MapPost("/api/alerts/resolve/{alertKey}", (string alertKey, CommandService svc) =>
{
    bool resolved = svc.ResolveAlert(alertKey);
    return Results.Ok(new { AlertKey = alertKey, Resolved = resolved });
}).WithTags("Part 2 – Alerts");

// ── Devices (Dictionary<K,V> lookup) ──────────────────────────────────────
app.MapGet("/api/devices", (CommandService svc, string? category, bool? anomalyOnly) =>
{
    // Record the search for the prediction engine's pattern analysis
    if (!string.IsNullOrWhiteSpace(category))
        svc.RecordSearch(category);

    var devices = string.IsNullOrWhiteSpace(category)
        ? svc.GetAllDevices()
        : svc.GetDevicesByCategory(category);

    if (anomalyOnly == true)
        devices = devices.Where(d => d.IsAnomaly).ToList();

    return Results.Ok(devices);
}).WithTags("Part 2 – Devices");

// ── Timeline (SortedDictionary snapshot — pre-sorted, no extra sort needed) ─
app.MapGet("/api/devices/timeline", (CommandService svc, int n = 150) =>
    Results.Ok(svc.GetTimeline(n)))
    .WithTags("Part 2 – Devices");

// ── Search recording (triggers prediction engine's pattern analysis) ───────
app.MapPost("/api/devices/search", (CommandService svc, string category) =>
{
    svc.RecordSearch(category);
    return Results.Ok(new { Recorded = true, Category = category });
}).WithTags("Part 2 – Devices");

// ── Predictions / Recommendations ─────────────────────────────────────────
app.MapGet("/api/predictions", (CommandService svc) =>
    Results.Ok(svc.GetPredictions()))
    .WithTags("Part 2 – Predictions");

app.Run();
