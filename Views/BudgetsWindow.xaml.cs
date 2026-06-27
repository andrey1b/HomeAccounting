using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class BudgetsWindow : Window
{
    private bool _ready;

    public BudgetsWindow()
    {
        InitializeComponent();
        ApplyLoc();

        CbYear.ItemsSource = BudgetService.GetYears();
        CbYear.SelectedItem = DateTime.Today.Year;
        CbMonth.ItemsSource = MonthNames();
        CbMonth.SelectedIndex = DateTime.Today.Month - 1;
        CbType.ItemsSource = new[] { AppLoc.T("tab_expenses"), AppLoc.T("tab_incomes") };
        CbType.SelectedIndex = 0;
        _ready = true;
    }

    private static List<string> MonthNames() =>
        Enumerable.Range(1, 12).Select(m =>
            System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(m)).ToList();

    private void ApplyLoc()
    {
        Title = AppLoc.T("budgets_title");
        TbLblYear.Text = AppLoc.T("lbl_year");
        TbLblMonth.Text = AppLoc.T("lbl_month");
        TbLblType.Text = AppLoc.T("lbl_type");
        BtnAdd.Content = AppLoc.T("btn_add");
        BtnEdit.Content = AppLoc.T("btn_edit");
        BtnDel.Content = AppLoc.T("btn_delete");
        ColCat.Header = AppLoc.T("col_category");
        ColSub.Header = AppLoc.T("col_subcategory");
        ColPlan.Header = AppLoc.T("budget_plan");
        ColFact.Header = AppLoc.T("budget_fact");
        ColDiff.Header = AppLoc.T("budget_diff");
    }

    private int Year => CbYear.SelectedItem is int y ? y : DateTime.Today.Year;
    private int Month => CbMonth.SelectedIndex + 1;
    private string Type => CbType.SelectedIndex == 1 ? "income" : "expense";

    private void Refresh()
    {
        if (!_ready) return;
        var list = BudgetService.GetAll(Year, Month, Type);
        Dg.ItemsSource = list;
        TbTotal.Text = AppLoc.T("budget_total", "plan", $"{list.Sum(b => b.Plan):N2}", "fact", $"{list.Sum(b => b.Fact):N2}");
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Refresh();

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new BudgetDialog(null, Year, Month, Type) { Owner = this };
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

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Refresh();
    }
}
