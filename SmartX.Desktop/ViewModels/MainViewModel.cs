using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ApiClient _api = new();

    // Cached ViewModels so that application state (filters, inputs, history) is
    // seamlessly preserved across asynchronous view navigation.
    private readonly HeatmapViewModel       _heatmapViewModel;
    private readonly CommandStreamViewModel _commandStreamViewModel;
    private readonly TopologyViewModel      _topologyViewModel;
    private readonly RegistryViewModel      _registryViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeatmapActive))]
    [NotifyPropertyChangedFor(nameof(IsCommandStreamActive))]
    [NotifyPropertyChangedFor(nameof(IsTopologyActive))]
    [NotifyPropertyChangedFor(nameof(IsRegistryActive))]
    private ViewModelBase _currentView;

    // Active tab indicators for highlighted nav buttons
    public bool IsHeatmapActive       => CurrentView is HeatmapViewModel;
    public bool IsCommandStreamActive => CurrentView is CommandStreamViewModel;
    public bool IsTopologyActive      => CurrentView is TopologyViewModel;
    public bool IsRegistryActive      => CurrentView is RegistryViewModel;

    public MainViewModel()
    {
        _heatmapViewModel       = new HeatmapViewModel(_api);
        _commandStreamViewModel = new CommandStreamViewModel(_api);
        _topologyViewModel      = new TopologyViewModel(_api);
        _registryViewModel      = new RegistryViewModel(_api);

        CurrentView = _heatmapViewModel;
    }

    [RelayCommand]
    private void NavigateToHeatmap() => CurrentView = _heatmapViewModel;

    [RelayCommand]
    private void NavigateToCommandStream() => CurrentView = _commandStreamViewModel;

    [RelayCommand]
    private void NavigateToTopology() => CurrentView = _topologyViewModel;

    [RelayCommand]
    private void NavigateToRegistry() => CurrentView = _registryViewModel;
}
