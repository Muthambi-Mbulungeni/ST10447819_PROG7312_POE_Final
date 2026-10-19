using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace SmartX.Desktop.Converters;

public class AnomalyColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAnomaly && isAnomaly)
        {
            return new SolidColorBrush(Color.Parse("#e74c3c")); // Red
        }
        return new SolidColorBrush(Color.Parse("#27ae60")); // Green
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
