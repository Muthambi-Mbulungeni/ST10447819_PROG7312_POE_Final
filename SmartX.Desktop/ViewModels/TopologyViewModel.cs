using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

public partial class TopologyViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private string _statusText = "Not Validated";

    [ObservableProperty]
    private ObservableCollection<string> _brokenPaths = new();

    public TopologyViewModel(ApiClient api)
    {
        _api = api;
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
        catch (System.Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
}
