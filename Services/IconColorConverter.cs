using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HomeAccounting.Services;

/// <summary>Подбирает цвет плитки для иконки счёта (эмодзи → цвет фона бейджа).</summary>
public class IconColorConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["💵"] = "#2E7D32", ["💶"] = "#3F51B5", ["💷"] = "#00897B", ["💴"] = "#C62828",
        ["💳"] = "#1565C0", ["🏦"] = "#6D4C41", ["🐷"] = "#EC407A", ["💰"] = "#F9A825",
        ["🪙"] = "#FFB300", ["🏧"] = "#455A64", ["👛"] = "#8E24AA",
        ["🟢"] = "#2E7D32", ["🔴"] = "#C62828", ["🔵"] = "#1565C0", ["🟡"] = "#F9A825",
        ["🟠"] = "#EF6C00", ["🟣"] = "#6A1B9A", ["🟤"] = "#6D4C41",
        ["🏠"] = "#00695C", ["🚗"] = "#283593", ["🛒"] = "#00838F", ["📱"] = "#37474F",
        ["✈️"] = "#0277BD", ["🎁"] = "#AD1457", ["💊"] = "#D81B60", ["🎓"] = "#4527A0",
        ["🌐"] = "#0097A7", ["⭐"] = "#F9A825", ["📈"] = "#2E7D32", ["📊"] = "#1565C0",
    };

    private static readonly Dictionary<string, Brush> Cache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? "";
        var hex = Map.TryGetValue(key, out var v) ? v : "#2E7D32";
        if (!Cache.TryGetValue(hex, out var brush))
        {
            brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            Cache[hex] = brush;
        }
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
