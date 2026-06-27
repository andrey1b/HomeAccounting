using System.Globalization;
using System.Windows;

namespace HomeAccounting.Views;

public partial class DepositDialog : Window
{
    public Deposit Result { get; private set; } = new();
    private readonly int _id;

    public DepositDialog(Deposit? existing = null)
    {
        InitializeComponent();
        ApplyLoc();

        var accs = AccountService.GetAll(true);
        accs.Insert(0, new Account { Id = 0, Name = AppLoc.T("none_option") });
        CbAccount.ItemsSource = accs;
        CbCurrency.ItemsSource = CurrencyService.GetAll();

        if (existing == null)
        {
            _id = 0;
            CbAccount.SelectedIndex = 0;
            int defCur = CurrencyService.DefaultId();
            CbCurrency.SelectedItem = (CbCurrency.ItemsSource as List<Currency>)?.FirstOrDefault(c => c.Id == defCur);
            DpOpen.SelectedDate = DateTime.Today;
            TbAmount.Text = "0"; TbRate.Text = "0";
        }
        else
        {
            _id = existing.Id;
            TbName.Text = existing.Name;
            CbAccount.SelectedItem = accs.FirstOrDefault(a => a.Id == (existing.AccountId ?? 0)) ?? accs[0];
            CbCurrency.SelectedItem = (CbCurrency.ItemsSource as List<Currency>)?.FirstOrDefault(c => c.Id == existing.CurrencyId);
            TbAmount.Text = existing.Amount.ToString(CultureInfo.InvariantCulture);
            TbRate.Text = existing.Rate.ToString(CultureInfo.InvariantCulture);
            if (DateTime.TryParse(existing.OpenDate, out var o)) DpOpen.SelectedDate = o;
            if (DateTime.TryParse(existing.CloseDate, out var c)) DpClose.SelectedDate = c;
            TbNote.Text = existing.Note;
        }
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("dlg_deposit_title");
        TbLblName.Text = AppLoc.T("dep_name");
        TbLblAccount.Text = AppLoc.T("lbl_account");
        TbLblCurrency.Text = AppLoc.T("col_currency");
        TbLblAmount.Text = AppLoc.T("dep_amount");
        TbLblRate.Text = AppLoc.T("dep_rate");
        TbLblOpen.Text = AppLoc.T("dep_open");
        TbLblClose.Text = AppLoc.T("dep_close");
        TbLblNote.Text = AppLoc.T("col_note");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");
    }

    private static double ParseNum(string s) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbName.Text))
        {
            MessageBox.Show(AppLoc.T("msg_enter_name"), AppLoc.T("app_title"));
            return;
        }
        var acc = CbAccount.SelectedItem as Account;
        var cur = CbCurrency.SelectedItem as Currency;
        Result = new Deposit
        {
            Id = _id,
            Name = TbName.Text.Trim(),
            AccountId = acc?.Id > 0 ? acc.Id : null,
            CurrencyId = cur?.Id,
            Amount = ParseNum(TbAmount.Text),
            Rate = ParseNum(TbRate.Text),
            OpenDate = DpOpen.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
            CloseDate = DpClose.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
            Note = TbNote.Text.Trim()
        };
        DialogResult = true;
    }
}
