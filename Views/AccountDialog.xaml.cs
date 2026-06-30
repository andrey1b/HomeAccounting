using System.Windows;

namespace HomeAccounting.Views;

public partial class AccountDialog : Window
{
    public Account Result { get; private set; } = new();

    private static readonly string[] Icons =
        ["💵", "💳", "🏦", "🐷", "🪙", "💶", "💷", "💴", "🏧", "💰",
         "🟢", "🔴", "🔵", "🟡", "🟠", "🟣", "🟤", "⭐", "📈", "📊",
         "🏠", "🚗", "🛒", "📱", "✈️", "🎁", "💊", "🎓", "🌐", "👛"];

    private static readonly string[] Currencies = ["₴", "$", "€"];

    public AccountDialog(Account? existing = null, int nextSortOrder = 1)
    {
        InitializeComponent();

        foreach (var ic  in Icons)      CbIcon.Items.Add(ic);
        foreach (var cur in Currencies) CbCurrency.Items.Add(cur);

        if (existing != null)
        {
            Result = existing;
            TbName.Text             = existing.Name;
            TbNote.Text             = existing.Note;
            TbBalance.Text          = existing.InitialBalance.ToString("F2");
            TbSortOrder.Text        = existing.SortOrder.ToString();
            CbIcon.SelectedItem     = existing.Icon;
            CbCurrency.SelectedItem = existing.Currency;
            CbHidden.IsChecked      = existing.IsHidden;
        }
        else
        {
            TbSortOrder.Text        = nextSortOrder.ToString();
            CbIcon.SelectedIndex     = 0; // 💰
            CbCurrency.SelectedIndex = 0; // ₴
        }

        ApplyLoc();
        TbName.Focus();
    }

    private void ApplyLoc()
    {
        Title               = AppLoc.T(Result.Id == 0 ? "dlg_account_add" : "dlg_account_edit");
        TbLblName.Text      = AppLoc.T("lbl_name");
        TbLblIcon.Text      = AppLoc.T("lbl_icon");
        TbLblSortOrder.Text = AppLoc.T("lbl_sort_order");
        TbLblBalance.Text   = AppLoc.T("lbl_init_balance");
        TbLblNote.Text      = AppLoc.T("col_note") + ":";
        CbHidden.Content    = AppLoc.T("chk_hidden_acc");
        BtnOk.Content       = "OK";
        BtnCancel.Content   = AppLoc.T("btn_cancel");
    }

    private void BtnOrderUp_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TbSortOrder.Text, out int v) && v > 1)
            TbSortOrder.Text = (v - 1).ToString();
    }

    private void BtnOrderDown_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TbSortOrder.Text, out int v))
            TbSortOrder.Text = (v + 1).ToString();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbName.Text))
        { MessageBox.Show(AppLoc.T("msg_enter_name")); return; }

        if (!double.TryParse(TbBalance.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var bal))
        { MessageBox.Show(AppLoc.T("msg_invalid_balance")); return; }

        Result.Name           = TbName.Text.Trim();
        Result.Note           = TbNote.Text.Trim();
        Result.InitialBalance = bal;
        Result.IsHidden       = CbHidden.IsChecked == true;
        Result.Icon           = CbIcon.SelectedItem?.ToString()     ?? "💰";
        Result.Currency       = CbCurrency.SelectedItem?.ToString() ?? "₴";

        if (int.TryParse(TbSortOrder.Text, out int order) && order > 0)
            Result.SortOrder = order;

        DialogResult = true;
    }
}
