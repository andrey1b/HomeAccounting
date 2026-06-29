using System.Windows;
using System.Windows.Controls;

namespace HomeAccounting.Views;

public partial class ExpenseDialog : Window
{
    public Expense Result { get; private set; } = new();
    public bool OpenAnother { get; private set; }

    private void BtnCalc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CalculatorWindow(TbAmount.Text) { Owner = this };
        if (dlg.ShowDialog() == true) TbAmount.Text = dlg.ResultText;
    }

    public ExpenseDialog(Expense? existing = null)
    {
        InitializeComponent();
        ApplyLoc();

        var accounts   = AccountService.GetAll(true);
        var categories = CategoryService.GetAll("expense");
        var units      = CategoryService.GetUnits();

        CbAccount.ItemsSource  = accounts;
        CbCategory.ItemsSource = categories;
        CbUnit.ItemsSource     = units;

        DpDate.SelectedDate = existing?.Date ?? DateTime.Today;

        if (existing != null)
        {
            Result = existing;
            CbAccount.SelectedItem    = accounts.FirstOrDefault(a => a.Id == existing.AccountId);
            CbCategory.SelectedItem   = categories.FirstOrDefault(c => c.Id == existing.CategoryId);
            CbSubcategory.SelectedItem = CbSubcategory.Items.Cast<Subcategory>()
                                            .FirstOrDefault(s => s.Id == existing.SubcategoryId);
            CbUnit.SelectedItem       = units.FirstOrDefault(u => u.Id == existing.UnitId);
            TbQty.Text      = (existing.Quantity ?? 1).ToString("F2");
            TbAmount.Text   = existing.Amount.ToString("F2");
            TbDiscount.Text = existing.Discount.ToString("F2");
            TbNote.Text     = existing.Note;
        }
        else
        {
            if (accounts.Count > 0)   CbAccount.SelectedIndex  = 0;
            if (categories.Count > 0) CbCategory.SelectedIndex = 0;
        }
    }

    private void ApplyLoc()
    {
        Title                = AppLoc.T("dlg_expense_title");
        TbLblDate.Text       = AppLoc.T("lbl_date");
        TbLblAccount.Text    = AppLoc.T("lbl_account_from");
        TbLblCategory.Text   = AppLoc.T("lbl_category");
        TbLblSubcategory.Text= AppLoc.T("lbl_subcategory");
        TbLblQty.Text        = AppLoc.T("lbl_qty");
        TbLblAmount.Text     = AppLoc.T("lbl_amount");
        TbLblDiscount.Text   = AppLoc.T("lbl_discount");
        TbLblNote.Text       = AppLoc.T("col_note") + ":";
        BtnMore.Content      = AppLoc.T("btn_more");
        BtnCancel.Content    = AppLoc.T("btn_cancel");
    }

    private void CbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CbCategory.SelectedItem is not Category cat) { CbSubcategory.ItemsSource = null; return; }
        CbSubcategory.ItemsSource = CategoryService.GetSubcategories(cat.Id);
        if (CbSubcategory.Items.Count > 0) CbSubcategory.SelectedIndex = 0;
    }

    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
        if (!TryFillResult()) return;
        OpenAnother = true;
        DialogResult = true;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (TryFillResult()) DialogResult = true;
    }

    private bool TryFillResult()
    {
        if (CbAccount.SelectedItem is not Account acct)
        { MessageBox.Show(AppLoc.T("msg_select_account")); return false; }
        if (CbCategory.SelectedItem is not Category cat)
        { MessageBox.Show(AppLoc.T("msg_select_category")); return false; }
        if (!double.TryParse(TbAmount.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amt) || amt <= 0)
        { MessageBox.Show(AppLoc.T("msg_invalid_amount")); return false; }

        double.TryParse(TbDiscount.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var disc);
        double.TryParse(TbQty.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var qty);

        Result.Date          = DpDate.SelectedDate ?? DateTime.Today;
        Result.AccountId     = acct.Id;
        Result.CategoryId    = cat.Id;
        Result.SubcategoryId = (CbSubcategory.SelectedItem as Subcategory)?.Id;
        Result.UnitId        = (CbUnit.SelectedItem as Unit)?.Id;
        Result.Quantity      = qty > 0 ? qty : null;
        Result.Amount        = amt;
        Result.Discount      = disc;
        Result.Note          = TbNote.Text.Trim();
        return true;
    }
}
