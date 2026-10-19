using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SmartX.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}