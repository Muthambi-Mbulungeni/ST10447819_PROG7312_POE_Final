using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using SmartX.Desktop.Models;

namespace SmartX.Desktop.Services;

/// <summary>Wrapper over HttpClient to communicate with the local SmartX API.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _client;
    private const string BaseUrl = "http://127.0.0.1:5050/api";

    public ApiClient()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl + "/") };
    }

    // ── Part 1 Methods (unchanged) ─────────────────────────────────────────

    public async Task<List<SensorProfile>> GetSensorsAsync()
    {
        var response = await _client.GetAsync("sensors");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SensorProfile>>()
               ?? new List<SensorProfile>();
    }

    public async Task<ValidationResult> ValidateDeploymentAsync()
    {
        var response = await _client.GetAsync("sensors/validate");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ValidationResult>()
               ?? new ValidationResult { IsValid = false };
    }

    public async Task<List<HeatmapEntry>> GetHeatmapAsync()
    {
        var response = await _client.GetAsync("telemetry/heatmap");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HeatmapEntry>>()
               ?? new List<HeatmapEntry>();
    }

    public async Task<bool> UploadFirmwareAsync(string macAddress, string filePath)
    {
        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        var response = await _client.PostAsync($"sensors/{macAddress}/upload", form);
        return response.IsSuccessStatusCode;
    }

    // ── Part 2 Methods ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/commands/stream — returns current Queue and PriorityQueue snapshots.
    /// </summary>
    public async Task<CommandStreamResponse> GetCommandStreamAsync()
    {
        var response = await _client.GetAsync("commands/stream");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommandStreamResponse>()
               ?? new CommandStreamResponse();
    }

    /// <summary>
    /// POST /api/commands/send — pushes a command onto the Stack history.
    /// </summary>
    public async Task<CommandRecordModel?> SendCommandAsync(
        string deviceId, string commandType, string payload)
    {
        var body = new { DeviceId = deviceId, CommandType = commandType, Payload = payload };
        var json = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("commands/send", json);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CommandRecordModel>();
    }

    /// <summary>
    /// POST /api/commands/undo — pops the last command from the Stack.
    /// </summary>
    public async Task<string> UndoLastCommandAsync()
    {
        var response = await _client.PostAsync("commands/undo",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return "Undo failed.";
        var obj = await response.Content.ReadFromJsonAsync<JsonElement>();
        return obj.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Done."
             : obj.TryGetProperty("Message", out var msg2) ? msg2.GetString() ?? "Done."
             : "Done.";
    }

    /// <summary>
    /// GET /api/commands/history — full Stack snapshot (most-recent first).
    /// </summary>
    public async Task<List<CommandRecordModel>> GetCommandHistoryAsync()
    {
        var response = await _client.GetAsync("commands/history");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CommandRecordModel>>()
               ?? new List<CommandRecordModel>();
    }

    /// <summary>
    /// GET /api/alerts/active — current HashSet<string> snapshot.
    /// </summary>
    public async Task<List<string>> GetActiveAlertsAsync()
    {
        var response = await _client.GetAsync("alerts/active");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<string>>()
               ?? new List<string>();
    }

    /// <summary>POST /api/alerts/resolve/{alertKey}</summary>
    public async Task ResolveAlertAsync(string alertKey)
    {
        var encoded = HttpUtility.UrlEncode(alertKey);
        await _client.PostAsync($"alerts/resolve/{encoded}",
            new StringContent("{}", Encoding.UTF8, "application/json"));
    }

    /// <summary>
    /// GET /api/devices — Dictionary lookup, optionally filtered by category / anomaly.
    /// </summary>
    public async Task<List<DeviceStateModel>> GetDevicesAsync(
        string? category = null, bool? anomalyOnly = null)
    {
        var url = "devices";
        var qs  = new System.Collections.Specialized.NameValueCollection();
        if (!string.IsNullOrWhiteSpace(category))  qs["category"]   = category;
        if (anomalyOnly.HasValue)                   qs["anomalyOnly"] = anomalyOnly.Value.ToString();

        if (qs.Count > 0)
        {
            var parts = new List<string>();
            foreach (string k in qs) parts.Add($"{k}={HttpUtility.UrlEncode(qs[k])}");
            url += "?" + string.Join("&", parts);
        }

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DeviceStateModel>>()
               ?? new List<DeviceStateModel>();
    }

    /// <summary>GET /api/predictions — AI recommendation engine output.</summary>
    public async Task<List<PredictionResultModel>> GetPredictionsAsync()
    {
        var response = await _client.GetAsync("predictions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<PredictionResultModel>>()
               ?? new List<PredictionResultModel>();
    }

    /// <summary>
    /// GET /api/devices/timeline — retrieves chronologically ordered readings from the SortedDictionary.
    /// </summary>
    public async Task<List<TimelineEntryModel>> GetTimelineAsync(int n = 60)
    {
        var response = await _client.GetAsync($"devices/timeline?n={n}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TimelineEntryModel>>()
               ?? new List<TimelineEntryModel>();
    }

    /// <summary>
    /// POST /api/commands/simulate-alert — injects a critical alert directly to test the PriorityQueue bypass.
    /// </summary>
    public async Task<bool> SimulateAlertAsync(string deviceId, float value = 9999f, string? unit = null, string? category = null)
    {
        var body = new { DeviceId = deviceId, Value = value, Unit = unit, Category = category };
        var json = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("commands/simulate-alert", json);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// POST /api/devices/search — records a category/device search for pattern analysis.
    /// </summary>
    public async Task RecordSearchAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        await _client.PostAsync(
            $"devices/search?category={HttpUtility.UrlEncode(category)}",
            new StringContent("{}", Encoding.UTF8, "application/json"));
    }

    // ── Part 3 Topology & Historical AVL Tree Methods ─────────────────────────

    public async Task<JsonElement?> GetMeshTopologyAsync()
    {
        var response = await _client.GetAsync("topology/mesh");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement?> GetMstAsync(string? startMac = null)
    {
        string uri = string.IsNullOrEmpty(startMac) ? "topology/mst" : $"topology/mst?startMac={HttpUtility.UrlEncode(startMac)}";
        var response = await _client.GetAsync(uri);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement?> GetBfsAsync(string? startMac = null)
    {
        string uri = string.IsNullOrEmpty(startMac) ? "topology/bfs" : $"topology/bfs?startMac={HttpUtility.UrlEncode(startMac)}";
        var response = await _client.GetAsync(uri);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement?> GetHistoricalRangeAsync(DateTime from, DateTime to, string? nodeId = null)
    {
        string uri = $"topology/historical/range?from={from:O}&to={to:O}";
        if (!string.IsNullOrEmpty(nodeId))
            uri += $"&nodeId={HttpUtility.UrlEncode(nodeId)}";

        var response = await _client.GetAsync(uri);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<bool> ToggleNodeStatusAsync(string mac, bool isOnline)
    {
        var body = new { IsOnline = isOnline };
        var json = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"topology/nodes/{HttpUtility.UrlEncode(mac)}/status", json);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetLinkStatusAsync(string a, string b, bool active)
    {
        var body = new { SourceMac = a, DestinationMac = b, IsActive = active };
        var json = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("topology/links/status", json);
        return response.IsSuccessStatusCode;
    }
}
