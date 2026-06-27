using System.Windows;

namespace HomeAccounting.Views;

public partial class CurrencyDialog : Window
{
    public Currency Result { get; private set; } = new();
    private readonly int _id;

    public CurrencyDialog(Currency? existing = null)
    {
        InitializeComponent();
        Title = AppLoc.T("dlg_currency_title");
        TbLblCode.Text = AppLoc.T("cur_code");
        TbLblName.Text = AppLoc.T("cur_name");
        TbLblSymbol.Text = AppLoc.T("cur_symbol");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");

        if (existing != null)
        {
            _id = existing.Id;
            TbCode.Text = existing.Code;
            TbName.Text = existing.Name;
            TbSymbol.Text = existing.Symbol;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbCode.Text)) { MessageBox.Show(AppLoc.T("cur_code")); return; }
        Result = new Currency
        {
            Id = _id,
            Code = TbCode.Text.Trim(),
            Name = TbName.Text.Trim(),
            Symbol = TbSymbol.Text.Trim()
        };
        DialogResult = true;
    }
}
