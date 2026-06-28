using System.Windows;
using System.Windows.Media;

namespace HomeAccounting.Services;

/// <summary>Темы фона таблиц. «Windows» — текущий вид; «Garden» — палитра «Офиса пенсионера»
/// (GardenPlanner/SeniorHub). Цвета задаются в ресурсах приложения (DynamicResource).</summary>
public static class ThemeService
{
    public static string Current { get; private set; } = "Windows";

    private record Palette(string Alt, string Sel, string SelText, string HeaderBg, string HeaderText);

    private static readonly Dictionary<string, Palette> Palettes = new()
    {
        // нечётная строка / выделение / текст выделения / фон заголовка / текст заголовка
        ["Windows"] = new("#F0F0F0", "#CCE5FF", "#000000", "#F3F3F3", "#000000"),
        ["Garden"]  = new("#EAF3E6", "#CFE3CF", "#1B5E20", "#2E7D32", "#FFFFFF"),
    };

    public static IEnumerable<string> Names => Palettes.Keys;

    private static Brush B(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }

    public static void Apply(string theme)
    {
        if (string.IsNullOrEmpty(theme) || !Palettes.TryGetValue(theme, out var p))
        {
            theme = "Windows";
            p = Palettes["Windows"];
        }
        Current = theme;

        var r = Application.Current.Resources;
        r["HA.Row.Alt"]     = B(p.Alt);
        r["HA.Row.Sel"]     = B(p.Sel);
        r["HA.Row.SelText"] = B(p.SelText);
        r["HA.Header.Bg"]   = B(p.HeaderBg);
        r["HA.Header.Text"] = B(p.HeaderText);

        // системное выделение (для ячеек DataGrid и пр.)
        r[SystemColors.HighlightBrushKey]                      = B(p.Sel);
        r[SystemColors.HighlightTextBrushKey]                  = B(p.SelText);
        r[SystemColors.InactiveSelectionHighlightBrushKey]     = B(p.Sel);
        r[SystemColors.InactiveSelectionHighlightTextBrushKey] = B(p.SelText);
    }

    // Для строк, фон которых выставляется из кода (вкладки «Счета»/«Переносы»)
    public static Brush RowSel => (Brush)Application.Current.Resources["HA.Row.Sel"];
    public static Brush RowAlt => (Brush)Application.Current.Resources["HA.Row.Alt"];
}
