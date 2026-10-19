using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ApiClient _api = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeatmapActive))]
    [NotifyPropertyChangedFor(nameof(IsCommandStreamActive))]
    [NotifyPropertyChangedFor(nameof(IsTopologyActive))]
    [NotifyPropertyChangedFor(nameof(IsRegistryActive))]
    private ViewModelBase _currentView;

    // Active tab indicators for highlighted nav buttons
    public bool IsHeatmapActive       => _currentView is HeatmapViewModel;
    public bool IsCommandStreamActive => _currentView is CommandStreamViewModel;
    public bool IsTopologyActive      => _currentView is TopologyViewModel;
    public bool IsRegistryActive      => _currentView is RegistryViewModel;

    public MainViewModel()
    {
        _currentView = new HeatmapViewModel(_api);
    }

    [RelayCommand]
    private void NavigateToHeatmap() => CurrentView = new HeatmapViewModel(_api);

    [RelayCommand]
    private void NavigateToCommandStream() => CurrentView = new CommandStreamViewModel(_api);

    [RelayCommand]
    private void NavigateToTopology() => CurrentView = new TopologyViewModel(_api);

    [RelayCommand]
    private void NavigateToRegistry() => CurrentView = new RegistryViewModel(_api);
}
