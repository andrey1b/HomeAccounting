using System.Windows;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class DepositsWindow : Window
{
    public DepositsWindow()
    {
        InitializeComponent();
        ApplyLoc();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("deposits_title");
        BtnAdd.Content = AppLoc.T("btn_add");
        BtnEdit.Content = AppLoc.T("btn_edit");
        BtnDel.Content = AppLoc.T("btn_delete");
        ColName.Header = AppLoc.T("dep_name");
        ColAcc.Header = AppLoc.T("lbl_account");
        ColAmount.Header = AppLoc.T("dep_amount");
        ColRate.Header = AppLoc.T("dep_rate");
        ColOpen.Header = AppLoc.T("dep_open");
        ColClose.Header = AppLoc.T("dep_close");
        ColNote.Header = AppLoc.T("col_note");
    }

    private void Refresh()
    {
        var list = DepositService.GetAll();
        Dg.ItemsSource = list;
        TbTotal.Text = $"{AppLoc.T("lbl_total")} {list.Sum(d => d.Amount):N2}";
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DepositDialog { Owner = this };
        if (dlg.ShowDialog() == true) { DepositService.Add(dlg.Result); Refresh(); }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void Dg_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (Dg.SelectedItem is not Deposit sel) return;
        var existing = DepositService.GetById(sel.Id);
        if (existing == null) return;
        var dlg = new DepositDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true) { DepositService.Update(dlg.Result); Refresh(); }
    }

    private void BtnDel_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not Deposit sel) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DepositService.Delete(sel.Id);
        Refresh();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Refresh();
    }
}
