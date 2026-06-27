using System.Globalization;
using System.Windows;

namespace HomeAccounting.Views;

public partial class RateDialog : Window
{
    public int      CurrencyId { get; private set; }
    public DateTime Date       { get; private set; }
    public double   Rate       { get; private set; }

    public RateDialog(int? presetCurrencyId = null)
    {
        InitializeComponent();
        Title = AppLoc.T("dlg_rate_title");
        TbLblCurrency.Text = AppLoc.T("col_currency");
        TbLblDate.Text = AppLoc.T("rate_date");
        TbLblRate.Text = AppLoc.T("rate_value");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");

        var curs = CurrencyService.GetAll();
        CbCurrency.ItemsSource = curs;
        CbCurrency.SelectedItem = curs.FirstOrDefault(c => c.Id == (presetCurrencyId ?? -1)) ?? curs.FirstOrDefault();
        DpDate.SelectedDate = DateTime.Today;
        TbRate.Text = "1";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CbCurrency.SelectedItem is not Currency c) return;
        CurrencyId = c.Id;
        Date = DpDate.SelectedDate ?? DateTime.Today;
        Rate = double.TryParse(TbRate.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 1;
        DialogResult = true;
    }
}
