using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Real-Time Command Stream page (Part 2 — 70 marks).
///
/// Data structures surfaced to the UI:
///   • Critical queue (PriorityQueue<T, int>) — shown in red at top of feed
///   • Standard queue (Queue<T>)              — shown below in FIFO order
///   • Command history (Stack<T>)             — LIFO order with Undo
///   • Active alerts (HashSet<T>)             — unique active error states, deduplicated
///   • Devices (Dictionary<K, V>)             — O(1) MAC lookup, filtered by category/anomaly
///   • Chronological Timeline (SortedDict)    — sorted sequentially by timestamp for graphs
///   • Automated Insights (PredictionEngine)  — pattern analysis and proactive recommendations
/// </summary>
public partial class CommandStreamViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly DispatcherTimer _pollTimer;

    // ── Observable collections bound to the view ───────────────

    [ObservableProperty] private ObservableCollection<IncomingPacketModel>  _criticalPackets  = new();
    [ObservableProperty] private ObservableCollection<IncomingPacketModel>  _standardPackets  = new();
    [ObservableProperty] private ObservableCollection<CommandRecordModel>   _commandHistory   = new();
    [ObservableProperty] private ObservableCollection<string>               _activeAlerts     = new();
    [ObservableProperty] private ObservableCollection<DeviceStateModel>     _devices          = new();
    [ObservableProperty] private ObservableCollection<TimelineEntryModel>   _timeline         = new();
    [ObservableProperty] private ObservableCollection<PredictionResultModel> _predictions    = new();

    // ── Timeline Sparkline for MiniGraph ───────────────────────
    [ObservableProperty] private double[] _timelineSparkline = Array.Empty<double>();

    // ── Available Command Types ────────────────────────────────
    [ObservableProperty] private ObservableCollection<string> _availableCommandTypes = new()
    {
        "ManualOverride",
        "TriggerActuator",
        "ShutdownDevice",
        "RestartDevice",
        "AdjustThreshold"
    };

    // ── Queue depth stats for the header ──────────────────────
    [ObservableProperty] private int    _standardQueueDepth;
    [ObservableProperty] private int    _criticalQueueDepth;
    [ObservableProperty] private int    _activeAlertCount;
    [ObservableProperty] private string _statusMessage = "Connecting…";

    // ── Command issue form ─────────────────────────────────────
    [ObservableProperty] private string _targetDeviceId = "AA:BB:CC:DD:00:00";
    [ObservableProperty] private string _commandType    = "ManualOverride";
    [ObservableProperty] private string _commandPayload = string.Empty;

    // ── Search / filter ────────────────────────────────────────
    [ObservableProperty] private string _filterCategory = string.Empty;
    [ObservableProperty] private bool   _showAnomalyOnly;

    // ── Last undo result ──────────────────────────────────────
    [ObservableProperty] private string _undoResult = string.Empty;

    public CommandStreamViewModel(ApiClient api)
    {
        _api = api;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += async (_, _) => await RefreshAllAsync();
        _pollTimer.Start();

        _ = RefreshAllAsync();
    }

    // ── Polling & Data Retrieval ───────────────────────────────

    private async Task RefreshAllAsync()
    {
        try
        {
            // 1. Queue snapshot (Queue<T> FIFO + PriorityQueue<T,int> bypass)
            var stream = await _api.GetCommandStreamAsync();
            StandardQueueDepth = stream.StandardCount;
            CriticalQueueDepth = stream.CriticalCount;

            CriticalPackets.Clear();
            foreach (var p in stream.Critical) CriticalPackets.Add(p);

            StandardPackets.Clear();
            foreach (var p in stream.Standard) StandardPackets.Add(p);

            // 2. Command history (Stack<T> LIFO snapshot)
            var history = await _api.GetCommandHistoryAsync();
            CommandHistory.Clear();
            foreach (var c in history) CommandHistory.Add(c);

            // 3. Active alerts (HashSet<T> deduplicated snapshot)
            var alerts = await _api.GetActiveAlertsAsync();
            ActiveAlertCount = alerts.Count;
            ActiveAlerts.Clear();
            foreach (var a in alerts) ActiveAlerts.Add(a);

            // 4. Automated Insights (PredictionEngine recommendations)
            var preds = await _api.GetPredictionsAsync();
            Predictions.Clear();
            foreach (var p in preds) Predictions.Add(p);

            // 5. Historical logs from SortedDictionary (ordered by timestamp)
            var timeline = await _api.GetTimelineAsync(40);
            Timeline.Clear();
            foreach (var t in timeline) Timeline.Add(t);
            if (timeline.Count > 1)
            {
                TimelineSparkline = timeline.Select(t => (double)t.Value).ToArray();
            }

            // 6. Devices from Dictionary (O(1) lookups)
            await LoadDevicesAsync();

            StatusMessage = $"Synchronised at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline — {ex.Message[..Math.Min(60, ex.Message.Length)]}";
        }
    }

    // ── Commands ───────────────────────────────────────────────

    /// <summary>Sends a manual override command → API pushes it onto Stack.</summary>
    [RelayCommand]
    private async Task SendCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetDeviceId)) return;
        try
        {
            var result = await _api.SendCommandAsync(TargetDeviceId, CommandType, CommandPayload);
            if (result is not null)
            {
                StatusMessage  = $"✅ Command {result.Id} issued to {result.DeviceId}";
                CommandPayload = string.Empty;
            }
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    /// <summary>Undoes the last command → API pops from Stack.</summary>
    [RelayCommand]
    private async Task UndoCommandAsync()
    {
        try
        {
            UndoResult = await _api.UndoLastCommandAsync();
            StatusMessage = $"↩ {UndoResult}";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    /// <summary>Simulates an immediate critical alert to demonstrate PriorityQueue bypass.</summary>
    [RelayCommand]
    private async Task SimulateCriticalAlertAsync()
    {
        try
        {
            var target = string.IsNullOrWhiteSpace(TargetDeviceId) ? "AA:BB:CC:DD:00:00" : TargetDeviceId;
            await _api.SimulateAlertAsync(target, 4850.0f, "SPIKE", "PowerConsumption");
            StatusMessage = $"⚡ Simulated Priority-0 alert on {target}!";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    /// <summary>Selects a device from the list to populate the command form.</summary>
    [RelayCommand]
    private void SelectDevice(DeviceStateModel? device)
    {
        if (device is null) return;
        TargetDeviceId = device.Mac;
        StatusMessage  = $"Selected: {device.NodeId} ({device.Mac})";
    }

    /// <summary>Applies a target device from a predictive insight recommendation.</summary>
    [RelayCommand]
    private void ApplyRecommendation(PredictionResultModel? prediction)
    {
        if (prediction is null) return;
        if (!string.IsNullOrWhiteSpace(prediction.RelatedDevice))
        {
            TargetDeviceId = prediction.RelatedDevice;
            StatusMessage  = $"Focused on {prediction.RelatedDevice} from insight.";
        }
    }

    /// <summary>Applies category/query filter → records search for Prediction Engine.</summary>
    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(FilterCategory))
                await _api.RecordSearchAsync(FilterCategory);

            await LoadDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    /// <summary>Clears active filter and reloads all devices.</summary>
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterCategory  = string.Empty;
        ShowAnomalyOnly = false;
        try
        {
            await LoadDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    /// <summary>Resolves (removes) a specific alert from the HashSet.</summary>
    [RelayCommand]
    private async Task ResolveAlertAsync(string alertKey)
    {
        if (string.IsNullOrWhiteSpace(alertKey)) return;
        try
        {
            await _api.ResolveAlertAsync(alertKey);
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"API offline: {ex.Message}";
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task LoadDevicesAsync()
    {
        try
        {
            var query      = string.IsNullOrWhiteSpace(FilterCategory) ? null : FilterCategory;
            bool? anomOnly = ShowAnomalyOnly ? true : null;
            var list       = await _api.GetDevicesAsync(query, anomOnly);

            Devices.Clear();
            foreach (var d in list) Devices.Add(d);
        }
        catch { /* handled by polling loop */ }
    }
}
