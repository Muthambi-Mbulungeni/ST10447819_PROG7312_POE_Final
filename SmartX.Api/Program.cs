using SmartX.Api.Contracts;
using SmartX.Api.Domain;
using SmartX.Api.Seed;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Singleton services ─────────────────────────────────────────────────────
// DeviceDirectory is the SINGLE SOURCE OF TRUTH for all device profiles & states
builder.Services.AddSingleton<DeviceDirectory>();   // Dictionary + SortedDictionary
builder.Services.AddSingleton<SensorRegistry>();    // Facade delegating to DeviceDirectory
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<DeploymentValidator>();
builder.Services.AddSingleton(_ =>
    new FileService(Path.Combine(Directory.GetCurrentDirectory(), "uploads")));

builder.Services.AddSingleton<PredictionEngine>();  // Pattern analysis engine
builder.Services.AddSingleton<CommandService>();    // Stack, Queue, PriorityQueue, HashSet

// Part 3 Singletons — MeshGraph and SmartXDataStructures container
builder.Services.AddSingleton<MeshGraph>(sp =>
{
    var graph = new MeshGraph();
    graph.SeedDefaultMeshNetwork();
    return graph;
});
builder.Services.AddSingleton<SmartXDataStructures>();

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
app.UseDefaultFiles();
app.UseStaticFiles();
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

app.MapPost("/api/telemetry/ingest", (IngestRequest req, TelemetryStore store, SensorRegistry reg, CommandService cmdSvc) =>
{
    if (!reg.Exists(req.DeviceId))
        return Results.NotFound($"Device {req.DeviceId} is not registered.");

    store.Ingest(req.DeviceId, req.Value, req.Unit);

    var profile = reg.Get(req.DeviceId);
    string cat = profile?.Category.ToString() ?? "Environmental";
    string nodeId = profile?.NodeId ?? req.DeviceId;

    // Check if anomalous
    bool isAnomaly = req.Value > 500.0f || (profile != null && Math.Abs(req.Value - 220f) > 80f);
    int priority = isAnomaly ? 0 : 2;

    var packet = new IncomingPacket(
        DeviceId:   req.DeviceId,
        NodeId:     nodeId,
        Value:      req.Value,
        Unit:       req.Unit,
        Category:   cat,
        IsCritical: isAnomaly,
        Priority:   priority,
        ReceivedAt: DateTime.UtcNow);

    cmdSvc.IngestPacket(packet);

    return Results.Ok(new { req.DeviceId, req.Value, req.Unit, IsCritical = isAnomaly, Priority = priority, Timestamp = DateTime.UtcNow });
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

// ── Simulate a critical alert (PriorityQueue<T, int> bypass demonstration) ─
app.MapPost("/api/commands/simulate-alert", (SimulateAlertRequest req, CommandService svc, SensorRegistry reg) =>
{
    var profile = reg.Get(req.DeviceId);
    string nodeId = profile?.NodeId ?? req.DeviceId;
    string cat = profile?.Category.ToString() ?? req.Category ?? "PowerConsumption";

    var packet = new IncomingPacket(
        DeviceId:   req.DeviceId,
        NodeId:     nodeId,
        Value:      req.Value,
        Unit:       req.Unit ?? "W",
        Category:   cat,
        IsCritical: true,
        Priority:   0, // PriorityQueue top priority (bypasses FIFO queue)
        ReceivedAt: DateTime.UtcNow);

    svc.IngestPacket(packet);

    return Results.Ok(new
    {
        Message = $"Critical alert simulated for {nodeId} ({req.DeviceId}). Ingested to PriorityQueue.",
        Packet = packet
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

// ── Disconnected Nodes (Second HashSet<T> snapshot & controls) ───────────
app.MapGet("/api/alerts/disconnected", (CommandService svc) =>
    Results.Ok(svc.GetDisconnectedNodes()))
    .WithTags("Part 2 – Alerts");

app.MapPost("/api/alerts/disconnect/{nodeId}", (string nodeId, CommandService svc) =>
{
    bool added = svc.MarkNodeDisconnected(nodeId);
    return Results.Ok(new { NodeId = nodeId, IsDisconnected = true, NewlyAdded = added });
}).WithTags("Part 2 – Alerts");

app.MapPost("/api/alerts/reconnect/{nodeId}", (string nodeId, CommandService svc) =>
{
    bool removed = svc.MarkNodeReconnected(nodeId);
    return Results.Ok(new { NodeId = nodeId, IsDisconnected = false, WasRemoved = removed });
}).WithTags("Part 2 – Alerts");

// ── Devices (Dictionary<K,V> lookup) ──────────────────────────────────────
app.MapGet("/api/devices", (CommandService svc, string? category, bool? anomalyOnly) =>
{
    // Record search for prediction engine pattern analysis
    if (!string.IsNullOrWhiteSpace(category))
        svc.RecordSearch(category);

    var devices = svc.SearchDevices(category, anomalyOnly);
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

// =============================================================================
// PART 3 ENDPOINTS: Network Topology, Mesh Routing & Historical AVL Tree
// =============================================================================

// ── Graph Mesh Topology: Vertices and Edges ─────────────────────────────────
app.MapGet("/api/topology/mesh", (MeshGraph graph) =>
{
    return Results.Ok(new
    {
        Nodes = graph.GetAllNodes(),
        Edges = graph.GetAllEdges(),
        NodeCount = graph.VertexCount,
        EdgeCount = graph.EdgeCount
    });
}).WithTags("Part 3 – Network Topology");

// ── Minimum Spanning Tree (Prim's Algorithm via MinHeap) ───────────────────
app.MapGet("/api/topology/mst", (MeshGraph graph, string? startMac) =>
{
    var mst = graph.CalculateMinimumSpanningTree(startMac);
    return Results.Ok(mst);
}).WithTags("Part 3 – Network Topology");

// ── Breadth-First Search (Subnet device reachability discovery) ─────────────
app.MapGet("/api/topology/bfs", (MeshGraph graph, string? startMac, bool? onlineOnly) =>
{
    string start = startMac ?? "GW:00:11:22:33:44";
    var order = graph.BreadthFirstSearch(start, onlineOnly ?? true);
    return Results.Ok(new { StartNode = start, VisitedOrder = order, TotalVisited = order.Count });
}).WithTags("Part 3 – Network Topology");

// ── Depth-First Search (Cluster exploration & cycle detection) ──────────────
app.MapGet("/api/topology/dfs", (MeshGraph graph, string? startMac, bool? onlineOnly) =>
{
    string start = startMac ?? "GW:00:11:22:33:44";
    var order = graph.DepthFirstSearch(start, onlineOnly ?? true);
    return Results.Ok(new { StartNode = start, VisitedOrder = order, TotalVisited = order.Count });
}).WithTags("Part 3 – Network Topology");

// ── Power-Optimal Multi-Hop Route (Dijkstra via MinHeap) ───────────────────
app.MapPost("/api/topology/route", (RouteRequest req, MeshGraph graph) =>
{
    if (string.IsNullOrWhiteSpace(req.SourceMac) || string.IsNullOrWhiteSpace(req.DestinationMac))
        return Results.BadRequest("Both SourceMac and DestinationMac are required.");

    var route = graph.FindOptimalRoute(req.SourceMac, req.DestinationMac);
    return Results.Ok(route);
}).WithTags("Part 3 – Network Topology");

// ── Dynamic Self-Healing Node Status Toggle ─────────────────────────────────
app.MapPost("/api/topology/nodes/{mac}/status", (string mac, NodeStatusRequest req, MeshGraph graph, CommandService cmdSvc) =>
{
    bool updated = graph.SetNodeStatus(mac, req.IsOnline);
    if (!req.IsOnline)
    {
        cmdSvc.MarkNodeDisconnected(mac);
    }
    else
    {
        cmdSvc.MarkNodeReconnected(mac);
    }
    return Results.Ok(new { MacAddress = mac, IsOnline = req.IsOnline, Updated = updated });
}).WithTags("Part 3 – Network Topology");

// ── Historical Telemetry Range Query (AVL Tree O(log N + K)) ───────────────
app.MapGet("/api/topology/historical/range", (CommandService svc, DateTime? from, DateTime? to, string? nodeId) =>
{
    var start = from ?? DateTime.UtcNow.AddHours(-24);
    var end   = to   ?? DateTime.UtcNow.AddMinutes(5);

    var results = svc.QueryHistoricalRange(start, end, nodeId);
    return Results.Ok(new
    {
        From = start,
        To = end,
        NodeId = nodeId ?? "All",
        Count = results.Count,
        Readings = results
    });
}).WithTags("Part 3 – Network Topology");
// ── Operator auth (Supabase is tried first in the browser; these are the local fallback) ──
app.MapPost("/api/auth/register", (AuthReq r) =>
{
    if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
        return Results.BadRequest(new { message = "Email and password are required." });
    if (r.Password.Length < 6)
        return Results.BadRequest(new { message = "Password must be at least 6 characters." });
    return Results.Ok(new
    {
        token = "local-" + Guid.NewGuid().ToString("N"),
                      user = new { email = r.Email, fullName = r.FullName ?? r.Email.Split('@')[0] }
    });
}).WithTags("Auth");

app.MapPost("/api/auth/login", (AuthReq r) =>
{
    if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
        return Results.BadRequest(new { message = "Email and password are required." });
    return Results.Ok(new
    {
        token = "local-" + Guid.NewGuid().ToString("N"),
                      user = new { email = r.Email, fullName = r.Email.Split('@')[0] }
    });
}).WithTags("Auth");

app.MapPost("/api/auth/supabase-session", (AuthReq r) =>
Results.Ok(new
{
    token = "sb-" + Guid.NewGuid().ToString("N"),
           user = new { email = r.Email, fullName = r.FullName ?? r.Email?.Split('@')[0] }
})).WithTags("Auth");
app.MapPost("/api/topology/links/status", (LinkStatusRequest req, MeshGraph graph) =>
{
    bool updated = graph.SetEdgeStatus(req.SourceMac, req.DestinationMac, req.IsActive);
    return Results.Ok(new { req.SourceMac, req.DestinationMac, req.IsActive, Updated = updated });
}).WithTags("Part 3 – Network Topology");
app.Run("http://*:5050");
record AuthReq(string? Email, string? Password, string? FullName, string? ConfirmPassword, string? AccessToken);
record LinkStatusRequest(string SourceMac, string DestinationMac, bool IsActive);
