using System.Globalization;
using System.Windows;

namespace HomeAccounting.Views;

public partial class BudgetCopyDialog : Window
{
    public int  FromYear, FromMonth, ToYear, ToMonth;
    public bool CopyExpense, CopyIncome;

    private class MonthItem { public int Num; public string Name = ""; public override string ToString() => Name; }

    public BudgetCopyDialog(int curYear, int curMonth)
    {
        InitializeComponent();
        Title = AppLoc.T("budget_copy_title");
        TbLblFrom.Text = AppLoc.T("budget_copy_from");
        TbLblTo.Text   = AppLoc.T("budget_copy_to");
        ChkExp.Content = AppLoc.T("budget_copy_exp");
        ChkInc.Content = AppLoc.T("budget_copy_inc");
        BtnOk.Content     = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");

        var months = Enumerable.Range(1, 12)
            .Select(m => new MonthItem { Num = m, Name = CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(m) })
            .ToList();
        var years = Enumerable.Range(2013, DateTime.Today.Year - 2013 + 2).ToList();

        CbFromMonth.ItemsSource = months;
        CbToMonth.ItemsSource   = months.Select(m => new MonthItem { Num = m.Num, Name = m.Name }).ToList();
        CbFromYear.ItemsSource  = years;
        CbToYear.ItemsSource    = years.ToList();

        // источник = текущий месяц; назначение = СЛЕДУЮЩИЙ месяц (исправляет баг ДБ7 «через месяц»)
        CbFromMonth.SelectedItem = months.First(m => m.Num == curMonth);
        CbFromYear.SelectedItem  = curYear;
        int nm = curMonth == 12 ? 1 : curMonth + 1;
        int ny = curMonth == 12 ? curYear + 1 : curYear;
        CbToMonth.SelectedItem = (CbToMonth.ItemsSource as List<MonthItem>)!.First(m => m.Num == nm);
        CbToYear.SelectedItem  = years.Contains(ny) ? ny : curYear;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        FromMonth = ((MonthItem)CbFromMonth.SelectedItem).Num;
        FromYear  = (int)CbFromYear.SelectedItem;
        ToMonth   = ((MonthItem)CbToMonth.SelectedItem).Num;
        ToYear    = (int)CbToYear.SelectedItem;
        CopyExpense = ChkExp.IsChecked == true;
        CopyIncome  = ChkInc.IsChecked == true;
        DialogResult = true;
    }
}
