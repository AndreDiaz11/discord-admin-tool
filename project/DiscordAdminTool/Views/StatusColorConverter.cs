using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DiscordAdminTool.Views;

public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "disconnected";
        return status switch
        {
            "connected" => Color.Parse("#3BA55D"),
            "connecting" => Color.Parse("#F9A825"),
            _ => Color.Parse("#ED4245"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
