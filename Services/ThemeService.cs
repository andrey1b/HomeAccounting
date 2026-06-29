using System.Windows;
using System.Windows.Media;

namespace HomeAccounting.Services;

/// <summary>Темы оформления. «Windows» — классический вид; «Garden» — палитра «Офиса пенсионера»
/// (GardenPlanner/SeniorHub). Цвета задаются в ресурсах приложения (DynamicResource).</summary>
public static class ThemeService
{
    public static string Current { get; private set; } = "Garden";

    private record Palette(
        string Alt, string Sel, string SelText, string HeaderBg, string HeaderText,
        string BtnBg, string BtnFg, string BtnHover, string BtnPress, string BtnBorder, string MenuBg,
        string TabSelBg, string TabSelText, string TabBg, string TabText, string Accent,
        string PanelBg, string PanelText, string TodayBg);

    private static readonly Dictionary<string, Palette> Palettes = new()
    {
        ["Windows"] = new(
            Alt: "#F0F0F0", Sel: "#CCE5FF", SelText: "#000000", HeaderBg: "#F3F3F3", HeaderText: "#000000",
            BtnBg: "#E1E1E1", BtnFg: "#000000", BtnHover: "#EAEAEA", BtnPress: "#CFCFCF",
            BtnBorder: "#ADADAD", MenuBg: "#F0F0F0",
            TabSelBg: "#FFFFFF", TabSelText: "#000000", TabBg: "#ECECEC", TabText: "#000000", Accent: "#1565C0",
            PanelBg: "#ECECEC", PanelText: "#000000", TodayBg: "#DCEBFF"),
        ["Garden"] = new(
            Alt: "#EAF3E6", Sel: "#CFE3CF", SelText: "#1B5E20", HeaderBg: "#2E7D32", HeaderText: "#FFFFFF",
            BtnBg: "#2E7D32", BtnFg: "#FFFFFF", BtnHover: "#388E3C", BtnPress: "#1B5E20",
            BtnBorder: "#2E7D32", MenuBg: "#DCEFD6",
            TabSelBg: "#2E7D32", TabSelText: "#FFFFFF", TabBg: "#DCEFD6", TabText: "#1B5E20", Accent: "#2E7D32",
            PanelBg: "#EAF3E6", PanelText: "#1B5E20", TodayBg: "#FFF1C2"),
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
            theme = "Garden";
            p = Palettes["Garden"];
        }
        Current = theme;

        var r = Application.Current.Resources;
        r["HA.Row.Alt"]      = B(p.Alt);
        r["HA.Row.Sel"]      = B(p.Sel);
        r["HA.Row.SelText"]  = B(p.SelText);
        r["HA.Header.Bg"]    = B(p.HeaderBg);
        r["HA.Header.Text"]  = B(p.HeaderText);
        r["HA.Button.Bg"]    = B(p.BtnBg);
        r["HA.Button.Fg"]    = B(p.BtnFg);
        r["HA.Button.Hover"] = B(p.BtnHover);
        r["HA.Button.Press"] = B(p.BtnPress);
        r["HA.Button.Border"]= B(p.BtnBorder);
        r["HA.Menu.Bg"]      = B(p.MenuBg);
        r["HA.Tab.SelBg"]    = B(p.TabSelBg);
        r["HA.Tab.SelText"]  = B(p.TabSelText);
        r["HA.Tab.Bg"]       = B(p.TabBg);
        r["HA.Tab.Text"]     = B(p.TabText);
        r["HA.Accent"]       = B(p.Accent);
        r["HA.Panel.Bg"]     = B(p.PanelBg);
        r["HA.Panel.Text"]   = B(p.PanelText);
        r["HA.Row.Today"]    = B(p.TodayBg);

        // системное выделение (для ячеек DataGrid и пр.)
        r[SystemColors.HighlightBrushKey]                      = B(p.Sel);
        r[SystemColors.HighlightTextBrushKey]                  = B(p.SelText);
        r[SystemColors.InactiveSelectionHighlightBrushKey]     = B(p.Sel);
        r[SystemColors.InactiveSelectionHighlightTextBrushKey] = B(p.SelText);
    }

    // Для строк, фон которых выставляется из кода (вкладки «Счета»/«Переносы»)
    public static Brush RowSel   => (Brush)Application.Current.Resources["HA.Row.Sel"];
    public static Brush RowAlt   => (Brush)Application.Current.Resources["HA.Row.Alt"];
    public static Brush RowToday => (Brush)Application.Current.Resources["HA.Row.Today"];
}
