using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SmartX.Desktop.Models;
using SmartX.Desktop.Services;

namespace SmartX.Desktop.ViewModels;

public partial class RegistryViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    [ObservableProperty]
    private ObservableCollection<SensorProfile> _sensors = new();

    public RegistryViewModel(ApiClient api)
    {
        _api = api;
        _ = LoadSensors();
    }

    private async Task LoadSensors()
    {
        try
        {
            var data = await _api.GetSensorsAsync();
            Sensors.Clear();
            foreach (var s in data) Sensors.Add(s);
        }
        catch { }
    }

    // Called from code-behind after file is selected
    public async Task UploadFirmware(string macAddress, string filePath)
    {
        try
        {
            await _api.UploadFirmwareAsync(macAddress, filePath);
            await LoadSensors(); // Refresh to show it has firmware now
        }
        catch { }
    }
}
