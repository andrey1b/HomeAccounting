using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;

namespace HomeAccounting.Views;

public partial class BudgetsWindow : Window
{
    private int _year  = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;
    private string _type = "expense";

    private static readonly Brush ActiveTab = new SolidColorBrush(Color.FromRgb(0xCC, 0xE5, 0xFF));
    private static readonly Brush SelMonth  = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9));

    public BudgetsWindow()
    {
        InitializeComponent();
        Title = AppLoc.T("budgets_title");
        BtnTypeExp.Content = AppLoc.T("budget_tab_exp");
        BtnTypeInc.Content = AppLoc.T("budget_tab_inc");
        BtnToday.Content   = AppLoc.T("btn_today");
        BtnCopy.Content    = AppLoc.T("btn_copy");
        BtnPrint.Content   = AppLoc.T("btn_print");
        BtnExport.Content  = AppLoc.T("btn_export");
        BtnAdd.Content     = AppLoc.T("btn_add");
        BtnEdit.Content    = AppLoc.T("btn_edit");
        BtnDel.Content     = AppLoc.T("btn_delete");
        ColCat.Header  = AppLoc.T("col_category");
        ColSub.Header  = AppLoc.T("col_subcategory");
        ColPlan.Header = AppLoc.T("budget_plan");
        ColFact.Header = AppLoc.T("budget_fact");
        ColDone.Header = AppLoc.T("budget_done");
        ColDiff.Header = AppLoc.T("budget_diff");
        ColNote.Header = AppLoc.T("col_note");

        // подписи кнопок месяцев
        foreach (var b in MonthGrid.Children.OfType<Button>())
        {
            int m = int.Parse((string)b.Tag);
            var name = CultureInfo.CurrentUICulture.DateTimeFormat.GetAbbreviatedMonthName(m).TrimEnd('.');
            b.Content = char.ToUpper(name[0]) + name.Substring(1);
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        UpdateChrome();
        Refresh();
    }

    private void UpdateChrome()
    {
        TbYear.Text = _year.ToString();
        BtnTypeExp.Background = _type == "expense" ? ActiveTab : SystemColors.ControlBrush;
        BtnTypeInc.Background = _type == "income"  ? ActiveTab : SystemColors.ControlBrush;
        foreach (var b in MonthGrid.Children.OfType<Button>())
        {
            int m = int.Parse((string)b.Tag);
            bool sel = m == _month;
            b.Background = sel ? SelMonth : SystemColors.ControlBrush;
            b.Foreground = sel ? Brushes.White : SystemColors.ControlTextBrush;
            b.FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    private void Refresh()
    {
        var list = BudgetService.GetAll(_year, _month, _type);
        Dg.ItemsSource = list;

        // итоги по валютам
        var curs = CurrencyService.GetAll();
        var lines = new List<string>();
        foreach (var c in curs)
        {
            var rows = list.Where(b => (b.CurrencyId ?? 0) == c.Id).ToList();
            double plan = rows.Sum(b => b.Plan), fact = rows.Sum(b => b.Fact);
            // строку базовой валюты показываем всегда, прочие — только при наличии данных
            if (!c.IsDefault && plan == 0 && fact == 0) continue;
            lines.Add($"{c.Symbol}   {AppLoc.T("budget_plan")}: {plan:N2}    {AppLoc.T("budget_fact")}: {fact:N2}    {AppLoc.T("budget_diff")}: {plan - fact:N2}");
        }
        TotalsList.ItemsSource = lines;
    }

    // ── навигация ────────────────────────────────────────────────────────────
    private void PrevYear_Click(object sender, RoutedEventArgs e) { _year--; UpdateChrome(); Refresh(); }
    private void NextYear_Click(object sender, RoutedEventArgs e) { _year++; UpdateChrome(); Refresh(); }
    private void Month_Click(object sender, RoutedEventArgs e)
    {
        _month = int.Parse((string)((Button)sender).Tag);
        UpdateChrome(); Refresh();
    }
    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _year = DateTime.Today.Year; _month = DateTime.Today.Month;
        UpdateChrome(); Refresh();
    }
    private void TypeExp_Click(object sender, RoutedEventArgs e) { _type = "expense"; UpdateChrome(); Refresh(); }
    private void TypeInc_Click(object sender, RoutedEventArgs e) { _type = "income";  UpdateChrome(); Refresh(); }

    // ── записи ───────────────────────────────────────────────────────────────
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new BudgetDialog(null, _year, _month, _type) { Owner = this };
        if (dlg.ShowDialog() == true) { BudgetService.Add(dlg.Result); Refresh(); }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void Dg_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (Dg.SelectedItem is not Budget sel) return;
        var existing = BudgetService.GetById(sel.Id);
        if (existing == null) return;
        var dlg = new BudgetDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true) { BudgetService.Update(dlg.Result); Refresh(); }
    }

    private void BtnDel_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not Budget sel) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        BudgetService.Delete(sel.Id);
        Refresh();
    }

    // ── копирование ──────────────────────────────────────────────────────────
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new BudgetCopyDialog(_year, _month) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        int n = BudgetService.CopyMonth(dlg.FromYear, dlg.FromMonth, dlg.ToYear, dlg.ToMonth,
                                        dlg.CopyExpense, dlg.CopyIncome);
        // перейти на целевой месяц и показать результат
        _year = dlg.ToYear; _month = dlg.ToMonth; UpdateChrome(); Refresh();
        MessageBox.Show(AppLoc.T("budget_copied", "count", n.ToString()), AppLoc.T("budget_copy_title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── печать ───────────────────────────────────────────────────────────────
    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var pd = new System.Windows.Controls.PrintDialog();
        if (pd.ShowDialog() != true) return;
        pd.PrintVisual(Dg, AppLoc.T("budgets_title") + $" {_month:00}.{_year}");
    }

    // ── экспорт ──────────────────────────────────────────────────────────────
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"Бюджет_{_year}-{_month:00}.xlsx"
        };
        if (sfd.ShowDialog() != true) return;
        var list = BudgetService.GetAll(_year, _month, _type);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Budget");
        string[] hdr = { AppLoc.T("col_category"), AppLoc.T("col_subcategory"), AppLoc.T("budget_plan"),
                         AppLoc.T("budget_fact"), AppLoc.T("budget_done"), AppLoc.T("budget_diff"), AppLoc.T("col_note") };
        for (int i = 0; i < hdr.Length; i++) ws.Cell(1, i + 1).Value = hdr[i];
        ws.Row(1).Style.Font.Bold = true;
        int r = 2;
        foreach (var b in list)
        {
            ws.Cell(r, 1).Value = b.CategoryName;
            ws.Cell(r, 2).Value = b.SubcategoryName;
            ws.Cell(r, 3).Value = b.Plan;
            ws.Cell(r, 4).Value = b.Fact;
            ws.Cell(r, 5).Value = b.PercentStr;
            ws.Cell(r, 6).Value = b.Diff;
            ws.Cell(r, 7).Value = b.Note;
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(sfd.FileName);
        MessageBox.Show(AppLoc.T("btn_export") + " ✓", AppLoc.T("budgets_title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
