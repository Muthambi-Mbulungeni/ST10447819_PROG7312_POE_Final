using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;
using Avalonia.Threading;
using System;

namespace SmartX.Desktop.ViewModels;

public partial class HeatmapViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private DispatcherTimer _timer;

    [ObservableProperty]
    private ObservableCollection<HeatmapEntry> _entries = new();

    public HeatmapViewModel(ApiClient api)
    {
        _api = api;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (s, e) => await LoadData();
        _timer.Start();
        
        _ = LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var data = await _api.GetHeatmapAsync();
            Entries.Clear();
            foreach (var item in data) Entries.Add(item);
        }
        catch { /* ignored for simplicity in polling */ }
    }
}
