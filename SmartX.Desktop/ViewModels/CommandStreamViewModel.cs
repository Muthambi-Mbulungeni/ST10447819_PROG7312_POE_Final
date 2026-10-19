using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Real-Time Command Stream page (Part 2 — 70 marks).
///
/// Data structures surfaced to the UI:
///   • Critical queue (PriorityQueue) — shown red at top of feed
///   • Standard queue (Queue)         — shown below in FIFO order
///   • Command history (Stack)        — most-recent first, with Undo
///   • Active alerts (HashSet)        — unique, no duplicates
///   • Devices (Dictionary)           — filtered by category/anomaly
///   • Predictions                    — recommendation engine output
/// </summary>
public partial class CommandStreamViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly DispatcherTimer _pollTimer;

    // ── Observable collections bound to the view ───────────────

    [ObservableProperty] private ObservableCollection<IncomingPacketModel> _criticalPackets = new();
    [ObservableProperty] private ObservableCollection<IncomingPacketModel> _standardPackets = new();
    [ObservableProperty] private ObservableCollection<CommandRecordModel>  _commandHistory  = new();
    [ObservableProperty] private ObservableCollection<string>              _activeAlerts    = new();
    [ObservableProperty] private ObservableCollection<DeviceStateModel>    _devices         = new();
    [ObservableProperty] private ObservableCollection<PredictionResultModel> _predictions   = new();

    // ── Queue depth stats for the header ──────────────────────
    [ObservableProperty] private int    _standardQueueDepth;
    [ObservableProperty] private int    _criticalQueueDepth;
    [ObservableProperty] private int    _activeAlertCount;
    [ObservableProperty] private string _statusMessage = "Connecting…";

    // ── Command issue form ─────────────────────────────────────
    [ObservableProperty] private string _targetDeviceId = string.Empty;
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

    // ── Polling ────────────────────────────────────────────────

    private async Task RefreshAllAsync()
    {
        try
        {
            // 1. Queue snapshot (Queue<T> + PriorityQueue<T,int>)
            var stream = await _api.GetCommandStreamAsync();
            StandardQueueDepth = stream.StandardCount;
            CriticalQueueDepth = stream.CriticalCount;

            CriticalPackets.Clear();
            foreach (var p in stream.Critical) CriticalPackets.Add(p);

            StandardPackets.Clear();
            foreach (var p in stream.Standard) StandardPackets.Add(p);

            // 2. Command history (Stack<T> snapshot)
            var history = await _api.GetCommandHistoryAsync();
            CommandHistory.Clear();
            foreach (var c in history) CommandHistory.Add(c);

            // 3. Active alerts (HashSet<T> snapshot)
            var alerts = await _api.GetActiveAlertsAsync();
            ActiveAlertCount = alerts.Count;
            ActiveAlerts.Clear();
            foreach (var a in alerts) ActiveAlerts.Add(a);

            // 4. Predictions
            var preds = await _api.GetPredictionsAsync();
            Predictions.Clear();
            foreach (var p in preds) Predictions.Add(p);

            // 5. Devices (always refresh, filter applied on server)
            await LoadDevicesAsync();

            StatusMessage = $"Last updated: {DateTime.Now:HH:mm:ss}";
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

        var result = await _api.SendCommandAsync(TargetDeviceId, CommandType, CommandPayload);
        if (result is not null)
        {
            StatusMessage  = $"✅ Command {result.Id} issued to {result.DeviceId}";
            CommandPayload = string.Empty; // Clear input after send
        }
        await RefreshAllAsync();
    }

    /// <summary>Undoes the last command → API pops from Stack.</summary>
    [RelayCommand]
    private async Task UndoCommandAsync()
    {
        UndoResult = await _api.UndoLastCommandAsync();
        await RefreshAllAsync();
    }

    /// <summary>Applies category/anomaly filter → records search for Prediction Engine.</summary>
    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        // Record the search so PredictionEngine can analyse patterns
        if (!string.IsNullOrWhiteSpace(FilterCategory))
            await _api.RecordSearchAsync(FilterCategory);

        await LoadDevicesAsync();
    }

    /// <summary>Clears active filter and reloads all devices.</summary>
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterCategory  = string.Empty;
        ShowAnomalyOnly = false;
        await LoadDevicesAsync();
    }

    /// <summary>Resolves (removes) a specific alert from the HashSet.</summary>
    [RelayCommand]
    private async Task ResolveAlertAsync(string alertKey)
    {
        if (string.IsNullOrWhiteSpace(alertKey)) return;
        await _api.ResolveAlertAsync(alertKey);
        await RefreshAllAsync();
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task LoadDevicesAsync()
    {
        var category    = string.IsNullOrWhiteSpace(FilterCategory) ? null : FilterCategory;
        bool? anomOnly  = ShowAnomalyOnly ? true : null;
        var list        = await _api.GetDevicesAsync(category, anomOnly);

        Devices.Clear();
        foreach (var d in list) Devices.Add(d);
    }
}
