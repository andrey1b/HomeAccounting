using System.Windows;

namespace HomeAccounting.Views;

public partial class TransferDialog : Window
{
    public Transfer Result { get; private set; } = new();

    public TransferDialog(Transfer? existing = null)
    {
        InitializeComponent();
        ApplyLoc();

        var accounts = AccountService.GetAll(true);
        CbFrom.ItemsSource = accounts;
        CbTo.ItemsSource   = accounts.ToList();

        DpDate.SelectedDate = existing?.Date ?? DateTime.Today;

        if (existing != null)
        {
            Result = existing;
            CbFrom.SelectedItem = accounts.FirstOrDefault(a => a.Id == existing.FromAccountId);
            CbTo.SelectedItem   = accounts.FirstOrDefault(a => a.Id == existing.ToAccountId);
            TbAmount.Text = existing.Amount.ToString("F2");
            TbNote.Text   = existing.Note;
        }
        else
        {
            if (accounts.Count > 0) CbFrom.SelectedIndex = 0;
            if (accounts.Count > 1) CbTo.SelectedIndex   = 1;
        }
    }

    private void ApplyLoc()
    {
        Title             = AppLoc.T("dlg_transfer_title");
        TbLblDate.Text    = AppLoc.T("lbl_date");
        TbLblFrom.Text    = AppLoc.T("lbl_from_account");
        TbLblTo.Text      = AppLoc.T("lbl_to_account");
        TbLblAmount.Text  = AppLoc.T("lbl_amount");
        TbLblNote.Text    = AppLoc.T("col_note") + ":";
        BtnCancel.Content = AppLoc.T("btn_cancel");
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CbFrom.SelectedItem is not Account fromAcct)
        { MessageBox.Show(AppLoc.T("msg_select_from_account")); return; }
        if (CbTo.SelectedItem is not Account toAcct)
        { MessageBox.Show(AppLoc.T("msg_select_to_account")); return; }
        if (fromAcct.Id == toAcct.Id)
        { MessageBox.Show(AppLoc.T("msg_same_accounts")); return; }
        if (!double.TryParse(TbAmount.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amt) || amt <= 0)
        { MessageBox.Show(AppLoc.T("msg_invalid_amount")); return; }

        Result.Date          = DpDate.SelectedDate ?? DateTime.Today;
        Result.FromAccountId = fromAcct.Id;
        Result.ToAccountId   = toAcct.Id;
        Result.Amount        = amt;
        Result.Note          = TbNote.Text.Trim();
        DialogResult = true;
    }
}
