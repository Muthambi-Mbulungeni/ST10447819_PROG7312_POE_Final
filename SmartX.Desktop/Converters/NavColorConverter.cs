using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SmartX.Desktop.Converters;

/// <summary>
/// Returns a highlighted background brush when the bound boolean (IsActive) is true,
/// and a transparent/dimmed brush when false. Used by nav buttons to show the active view.
/// </summary>
public sealed class NavColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive
            ? new SolidColorBrush(Color.Parse("#7c3aed"))  // active — purple accent
            : new SolidColorBrush(Color.Parse("#252542"));  // inactive — subtle dark
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
