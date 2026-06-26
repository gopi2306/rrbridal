using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Converters;

public sealed class LayoutBreakpointConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LayoutBreakpoint current || parameter is null)
            return targetType == typeof(Visibility) ? Visibility.Collapsed : false;

        if (!Enum.TryParse<LayoutBreakpoint>(parameter.ToString(), true, out var expected))
            return targetType == typeof(Visibility) ? Visibility.Collapsed : false;

        var matches = current == expected;
        if (targetType == typeof(Visibility))
            return matches ? Visibility.Visible : Visibility.Collapsed;

        return matches;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
