using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Markup.Xaml;
using SmartX.Desktop.ViewModels;
using System.Linq;

namespace SmartX.Desktop.Views;

public partial class RegistryView : UserControl
{
    public RegistryView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnUploadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string macAddress && DataContext is RegistryViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Firmware Manifest",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                var filePath = files[0].Path.LocalPath;
                await vm.UploadFirmware(macAddress, filePath);
            }
        }
    }
}
