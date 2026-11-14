using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

public partial class TopologyViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private string _statusText = "Mesh Topology Ready";

    [ObservableProperty]
    private int _nodeCount = 0;

    [ObservableProperty]
    private int _edgeCount = 0;

    [ObservableProperty]
    private string _mstSummary = "Click 'Compute Prim MST' to calculate optimal tree.";

    [ObservableProperty]
    private string _bfsSummary = "Click 'Run BFS Scan' to discover subnet hops.";

    [ObservableProperty]
    private string _historicalQuerySummary = "Select time window and query AVL tree.";

    [ObservableProperty]
    private ObservableCollection<string> _meshNodesList = new();

    [ObservableProperty]
    private ObservableCollection<string> _mstEdgesList = new();

    [ObservableProperty]
    private ObservableCollection<string> _historicalResultsList = new();

    [ObservableProperty]
    private ObservableCollection<string> _brokenPaths = new();

    public TopologyViewModel(ApiClient api)
    {
        _api = api;
        _ = LoadTopologyAsync();
    }

    [RelayCommand]
    public async Task LoadTopologyAsync()
    {
        try
        {
            var json = await _api.GetMeshTopologyAsync();
            if (json.HasValue && json.Value.ValueKind == JsonValueKind.Object)
            {
                var root = json.Value;
                NodeCount = root.TryGetProperty("nodeCount", out var nc) ? nc.GetInt32() : 0;
                EdgeCount = root.TryGetProperty("edgeCount", out var ec) ? ec.GetInt32() : 0;

                MeshNodesList.Clear();
                if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in nodes.EnumerateArray())
                    {
                        string mac = n.GetProperty("macAddress").GetString() ?? "";
                        string name = n.GetProperty("deviceName").GetString() ?? "";
                        string role = n.GetProperty("role").GetString() ?? "";
                        bool online = n.GetProperty("isOnline").GetBoolean();
                        MeshNodesList.Add($"[{role}] {name} ({mac}) - {(online ? "ONLINE" : "OFFLINE")}");
                    }
                }
                StatusText = $"Loaded {NodeCount} nodes and {EdgeCount} wireless links.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading topology: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ComputeMstAsync()
    {
        try
        {
            StatusText = "Calculating Prim's Minimum Spanning Tree...";
            var json = await _api.GetMstAsync();
            if (json.HasValue && json.Value.ValueKind == JsonValueKind.Object)
            {
                var root = json.Value;
                double weight = root.TryGetProperty("totalWeight", out var tw) ? tw.GetDouble() : 0.0;
                int count = root.TryGetProperty("nodeCount", out var nc) ? nc.GetInt32() : 0;
                bool connected = root.TryGetProperty("isConnected", out var ic) && ic.GetBoolean();

                MstSummary = $"Prim's MST: {count} nodes connected | Total Energy/Latency Weight: {weight:F1}ms | Connected: {connected}";

                MstEdgesList.Clear();
                if (root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in edges.EnumerateArray())
                    {
                        string src = e.GetProperty("sourceMac").GetString() ?? "";
                        string dst = e.GetProperty("destinationMac").GetString() ?? "";
                        double w = e.GetProperty("weight").GetDouble();
                        MstEdgesList.Add($"Hop: {src}  <--->  {dst}  (Cost: {w:F1}ms)");
                    }
                }
                StatusText = "Prim's MST computed successfully.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"MST Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RunBfsAsync()
    {
        try
        {
            StatusText = "Running Breadth-First Search (BFS)...";
            var json = await _api.GetBfsAsync();
            if (json.HasValue && json.Value.ValueKind == JsonValueKind.Object)
            {
                var root = json.Value;
                int total = root.TryGetProperty("totalVisited", out var tv) ? tv.GetInt32() : 0;
                var list = new System.Collections.Generic.List<string>();

                if (root.TryGetProperty("visitedOrder", out var vo) && vo.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in vo.EnumerateArray())
                    {
                        list.Add(item.GetString() ?? "");
                    }
                }

                BfsSummary = $"BFS Reached {total} nodes: {string.Join(" -> ", list)}";
                StatusText = $"BFS completed across {total} reachable subnet nodes.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"BFS Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task QueryHistoricalRangeAsync()
    {
        try
        {
            StatusText = "Querying historical telemetry via self-balancing AVL Tree...";
            var from = DateTime.UtcNow.AddHours(-24);
            var to = DateTime.UtcNow.AddMinutes(5);

            var json = await _api.GetHistoricalRangeAsync(from, to);
            if (json.HasValue && json.Value.ValueKind == JsonValueKind.Object)
            {
                var root = json.Value;
                int count = root.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                HistoricalQuerySummary = $"AVL Tree Range Query returned {count} timestamped readings in O(log N + K) time.";

                HistoricalResultsList.Clear();
                if (root.TryGetProperty("readings", out var readings) && readings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in readings.EnumerateArray())
                    {
                        string mac = r.GetProperty("mac").GetString() ?? "";
                        string node = r.GetProperty("nodeId").GetString() ?? "";
                        float val = r.GetProperty("value").GetSingle();
                        string unit = r.GetProperty("unit").GetString() ?? "";
                        string ts = r.GetProperty("timestamp").GetString() ?? "";
                        HistoricalResultsList.Add($"[{ts}] {node} ({mac}): {val:F2} {unit}");
                    }
                }
                StatusText = $"AVL Tree query finished: {count} records retrieved.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"AVL Query Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ToggleFailoverAsync()
    {
        try
        {
            StatusText = "Simulating Repeater Alpha failure (dynamic self-healing)...";
            // Toggle Repeater Alpha status
            bool ok = await _api.SetLinkStatusAsync("GW:00:11:22:33:44", "ESP:AA:01:02:03:04", false);
            if (ok)
            {
                StatusText = "Repeater Alpha taken OFFLINE. Network dynamic self-healing activated!";
                await LoadTopologyAsync();
                await ComputeMstAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failover Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RestoreNetworkAsync()
    {
        try
        {
            StatusText = "Restoring Repeater Alpha online...";
            bool ok = await _api.SetLinkStatusAsync("GW:00:11:22:33:44", "ESP:AA:01:02:03:04", true);
            if (ok)
            {
                StatusText = "Repeater Alpha restored. Network reconnected!";
                await LoadTopologyAsync();
                await ComputeMstAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Restore Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ValidateTopology()
    {
        try
        {
            var result = await _api.ValidateDeploymentAsync();
            if (result.IsValid)
            {
                StatusText = "Validation Passed - Network is fully configured.";
                BrokenPaths.Clear();
            }
            else
            {
                StatusText = "Validation Failed - Broken paths found:";
                BrokenPaths.Clear();
                foreach (var path in result.FailedPaths)
                {
                    BrokenPaths.Add(path);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
}
