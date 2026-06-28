using System.Windows;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class CurrencyManagerWindow : Window
{
    public CurrencyManagerWindow()
    {
        InitializeComponent();
        ApplyLoc();
        RefreshCur();
        RefreshRates();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("currencies_title");
        TabCur.Header = AppLoc.T("tab_currencies");
        TabRates.Header = AppLoc.T("tab_rates");
        BtnCurAdd.Content = AppLoc.T("btn_add");
        BtnCurEdit.Content = AppLoc.T("btn_edit");
        BtnCurDel.Content = AppLoc.T("btn_delete");
        BtnCurDef.Content = AppLoc.T("cur_make_default");
        ColCode.Header = AppLoc.T("cur_code");
        ColCName.Header = AppLoc.T("cur_name");
        ColSym.Header = AppLoc.T("cur_symbol");
        ColDef.Header = AppLoc.T("cur_default");
        BtnRateAdd.Content = AppLoc.T("btn_add");
        BtnRateDel.Content = AppLoc.T("btn_delete");
        BtnRateDownload.Content = AppLoc.T("rate_download");
        ColRDate.Header = AppLoc.T("rate_date");
        ColRCur.Header = AppLoc.T("col_currency");
        ColRVal.Header = AppLoc.T("rate_value");
    }

    private void RefreshCur() => DgCur.ItemsSource = CurrencyService.GetAll();
    private void RefreshRates() => DgRates.ItemsSource = CurrencyService.GetRates();

    private void BtnCurAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CurrencyDialog { Owner = this };
        if (dlg.ShowDialog() == true) { CurrencyService.Add(dlg.Result); RefreshCur(); }
    }

    private void BtnCurEdit_Click(object sender, RoutedEventArgs e) => EditCur();
    private void DgCur_DoubleClick(object sender, MouseButtonEventArgs e) => EditCur();

    private void EditCur()
    {
        if (DgCur.SelectedItem is not Currency sel) return;
        var dlg = new CurrencyDialog(sel) { Owner = this };
        if (dlg.ShowDialog() == true) { CurrencyService.Update(dlg.Result); RefreshCur(); }
    }

    private void BtnCurDel_Click(object sender, RoutedEventArgs e)
    {
        if (DgCur.SelectedItem is not Currency sel) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        CurrencyService.Delete(sel.Id);
        RefreshCur();
    }

    private void BtnCurDef_Click(object sender, RoutedEventArgs e)
    {
        if (DgCur.SelectedItem is not Currency sel) return;
        CurrencyService.SetDefault(sel.Id);
        RefreshCur();
    }

    private void BtnRateAdd_Click(object sender, RoutedEventArgs e)
    {
        var preset = (DgCur.SelectedItem as Currency)?.Id;
        var dlg = new RateDialog(preset) { Owner = this };
        if (dlg.ShowDialog() == true) { CurrencyService.AddRate(dlg.CurrencyId, dlg.Date, dlg.Rate); RefreshRates(); }
    }

    private void BtnRateDel_Click(object sender, RoutedEventArgs e)
    {
        if (DgRates.SelectedItem is not ExchangeRate sel) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        CurrencyService.DeleteRate(sel.Id);
        RefreshRates();
    }

    private void BtnRateDownload_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        BtnRateDownload.IsEnabled = false;
        try
        {
            var (count, date) = CurrencyService.DownloadRatesNbu();
            RefreshRates();
            MessageBox.Show(AppLoc.T("rate_downloaded", "count", count.ToString(), "date", date),
                AppLoc.T("currencies_title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(AppLoc.T("rate_download_err") + "\n" + ex.Message,
                AppLoc.T("currencies_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            BtnRateDownload.IsEnabled = true;
        }
    }
}
