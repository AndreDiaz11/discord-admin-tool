using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DiscordAdminTool.Views;

public class FractionToWidthConverter : IValueConverter
{
    public static readonly FractionToWidthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double d ? d : 0;
        var total = parameter != null ? System.Convert.ToDouble(parameter, culture) : 260;
        return Math.Max(0, Math.Min(total, fraction * total));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
