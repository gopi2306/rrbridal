using System;
using System.Globalization;
using System.Windows.Data;

namespace RRBridal.StoreBilling.App.Converters;

public sealed class EnumBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        return value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s)
            return Enum.Parse(targetType, s, ignoreCase: true);
        return Binding.DoNothing;
    }
}
