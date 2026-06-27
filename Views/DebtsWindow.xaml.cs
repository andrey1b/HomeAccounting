using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class DebtsWindow : Window
{
    public DebtsWindow()
    {
        InitializeComponent();
        ApplyLoc();
        CbKindFilter.ItemsSource = new[]
        {
            AppLoc.T("debt_all"), AppLoc.T("debt_debtor"), AppLoc.T("debt_creditor")
        };
        CbKindFilter.SelectedIndex = 0;
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("menu_debts");
        TbLblShow.Text = AppLoc.T("debt_show");
        BtnAdd.Content = AppLoc.T("btn_add");
        BtnEdit.Content = AppLoc.T("btn_edit");
        BtnDel.Content = AppLoc.T("btn_delete");
        ColDate.Header = AppLoc.T("col_date");
        ColParty.Header = AppLoc.T("debt_party");
        ColAcc.Header = AppLoc.T("lbl_account");
        ColAmount.Header = AppLoc.T("debt_amount");
        ColBack.Header = AppLoc.T("debt_back");
        ColRem.Header = AppLoc.T("debt_remaining");
        ColStatus.Header = AppLoc.T("debt_status");
        ColNote.Header = AppLoc.T("col_note");
    }

    private string? KindFilter() => CbKindFilter.SelectedIndex switch
    {
        1 => "debtor",
        2 => "creditor",
        _ => null
    };

    private void Refresh()
    {
        var list = DebtService.GetAll(KindFilter());
        Dg.ItemsSource = list;
        double owedToMe = list.Where(d => d.Kind == "debtor"  && !d.IsClosed).Sum(d => d.Remaining);
        double iOwe     = list.Where(d => d.Kind == "creditor" && !d.IsClosed).Sum(d => d.Remaining);
        TbTotal.Text = AppLoc.T("debt_total", "owed", $"{owedToMe:N2}", "iowe", $"{iOwe:N2}");
    }

    private void CbKindFilter_Changed(object sender, SelectionChangedEventArgs e) => Refresh();

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DebtDialog { Owner = this };
        if (dlg.ShowDialog() == true) { DebtService.Add(dlg.Result); Refresh(); }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void Dg_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (Dg.SelectedItem is not Debt sel) return;
        var existing = DebtService.GetById(sel.Id);
        if (existing == null) return;
        var dlg = new DebtDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true) { DebtService.Update(dlg.Result); Refresh(); }
    }

    private void BtnDel_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not Debt sel) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DebtService.Delete(sel.Id);
        Refresh();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Refresh();
    }
}
