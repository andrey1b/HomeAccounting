using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HomeAccounting.Views;

public partial class DebtDialog : Window
{
    public Debt Result { get; private set; } = new();
    private readonly int _id;

    public DebtDialog(Debt? existing = null)
    {
        InitializeComponent();
        ApplyLoc();

        CbKind.ItemsSource = new[] { AppLoc.T("debt_debtor"), AppLoc.T("debt_creditor") };

        var accs = AccountService.GetAll(true);
        accs.Insert(0, new Account { Id = 0, Name = AppLoc.T("none_option") });
        CbAccount.ItemsSource = accs;

        CbCurrency.ItemsSource = CurrencyService.GetAll();

        if (existing == null)
        {
            _id = 0;
            CbKind.SelectedIndex = 0;
            CbAccount.SelectedIndex = 0;
            DpDate.SelectedDate = DateTime.Today;
            int defCur = CurrencyService.DefaultId();
            CbCurrency.SelectedItem = (CbCurrency.ItemsSource as List<Currency>)?.FirstOrDefault(c => c.Id == defCur);
            TbPercent.Text = "0"; TbAmount.Text = "0"; TbBack.Text = "0";
        }
        else
        {
            _id = existing.Id;
            CbKind.SelectedIndex = existing.Kind == "creditor" ? 1 : 0;
            CbAccount.SelectedItem = accs.FirstOrDefault(a => a.Id == (existing.AccountId ?? 0)) ?? accs[0];
            CbCurrency.SelectedItem = (CbCurrency.ItemsSource as List<Currency>)?.FirstOrDefault(c => c.Id == existing.CurrencyId);
            DpDate.SelectedDate = existing.Date;
            TbParty.Text = existing.Counterparty;
            TbPercent.Text = existing.Percent.ToString(CultureInfo.InvariantCulture);
            TbAmount.Text = existing.Amount.ToString(CultureInfo.InvariantCulture);
            TbBack.Text = existing.AmountBack.ToString(CultureInfo.InvariantCulture);
            CbClosed.IsChecked = existing.IsClosed;
            if (DateTime.TryParse(existing.DateClose, out var dc)) DpClose.SelectedDate = dc;
        }
        UpdateClosePanel();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("debts_title");
        TbLblKind.Text = AppLoc.T("debt_kind");
        TbLblParty.Text = AppLoc.T("debt_party");
        TbLblAccount.Text = AppLoc.T("lbl_account");
        TbLblCurrency.Text = AppLoc.T("col_currency");
        TbLblDate.Text = AppLoc.T("col_date");
        TbLblPercent.Text = AppLoc.T("debt_percent");
        TbLblAmount.Text = AppLoc.T("debt_amount");
        TbLblBack.Text = AppLoc.T("debt_back");
        TbLblDateClose.Text = AppLoc.T("debt_date_close");
        TbLblNote.Text = AppLoc.T("col_note");
        CbClosed.Content = AppLoc.T("debt_closed");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");
    }

    private void CbClosed_Toggle(object sender, RoutedEventArgs e) => UpdateClosePanel();

    private void UpdateClosePanel()
    {
        if (PnlClose == null) return;
        PnlClose.Visibility = CbClosed.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (CbClosed.IsChecked == true && DpClose.SelectedDate == null)
            DpClose.SelectedDate = DateTime.Today;
    }

    private static double ParseNum(string s) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbParty.Text))
        {
            MessageBox.Show(AppLoc.T("debt_party_required"), AppLoc.T("app_title"));
            return;
        }
        var acc = CbAccount.SelectedItem as Account;
        var cur = CbCurrency.SelectedItem as Currency;
        Result = new Debt
        {
            Id = _id,
            Kind = CbKind.SelectedIndex == 1 ? "creditor" : "debtor",
            Counterparty = TbParty.Text.Trim(),
            AccountId = acc?.Id > 0 ? acc.Id : null,
            CurrencyId = cur?.Id,
            Date = DpDate.SelectedDate ?? DateTime.Today,
            Percent = ParseNum(TbPercent.Text),
            Amount = ParseNum(TbAmount.Text),
            AmountBack = ParseNum(TbBack.Text),
            IsClosed = CbClosed.IsChecked == true,
            DateClose = CbClosed.IsChecked == true && DpClose.SelectedDate.HasValue
                        ? DpClose.SelectedDate.Value.ToString("yyyy-MM-dd") : "",
            Note = TbNote.Text.Trim()
        };
        DialogResult = true;
    }
}
