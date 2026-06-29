using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class CalculatorWindow : Window
{
    /// <summary>Итоговое значение (в инвариантном формате с точкой).</summary>
    public string ResultText { get; private set; } = "";

    private readonly bool _standalone;

    public CalculatorWindow(string? initial = null, bool standalone = false)
    {
        InitializeComponent();
        _standalone = standalone;
        Title = AppLoc.T("calc_title");
        BtnOk.Content     = standalone ? AppLoc.T("btn_close") : AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");
        if (standalone) BtnCancel.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(initial) && initial.Trim() != "0")
            TbDisplay.Text = initial.Trim().Replace(',', '.');
        Loaded += (_, _) => { TbDisplay.CaretIndex = TbDisplay.Text.Length; TbDisplay.Focus(); };
    }

    private void Char_Click(object sender, RoutedEventArgs e)
    {
        TbDisplay.Text += (string)((Button)sender).Content;
        TbDisplay.CaretIndex = TbDisplay.Text.Length;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => TbDisplay.Text = "";

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (TbDisplay.Text.Length > 0)
            TbDisplay.Text = TbDisplay.Text[..^1];
        TbDisplay.CaretIndex = TbDisplay.Text.Length;
    }

    private void Equals_Click(object sender, RoutedEventArgs e)
    {
        if (TryEval(TbDisplay.Text, out var v))
        {
            TbDisplay.Text = v.ToString(CultureInfo.InvariantCulture);
            TbDisplay.CaretIndex = TbDisplay.Text.Length;
        }
        else TbDisplay.Text = "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_standalone) { Close(); return; }
        if (string.IsNullOrWhiteSpace(TbDisplay.Text)) { ResultText = "0"; DialogResult = true; return; }
        if (TryEval(TbDisplay.Text, out var v))
        {
            ResultText = Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(AppLoc.T("calc_error"), AppLoc.T("calc_title"));
        }
    }

    private void TbDisplay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Equals_Click(sender, e); e.Handled = true; }
    }

    private static bool TryEval(string expr, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(expr)) return false;
        try
        {
            var s = expr.Replace(',', '.').Replace('×', '*').Replace('÷', '/').Replace(" ", "");
            var r = new DataTable().Compute(s, null);
            if (r == null || r is DBNull) return false;
            value = Convert.ToDouble(r, CultureInfo.InvariantCulture);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        catch { return false; }
    }
}
