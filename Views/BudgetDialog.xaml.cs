using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HomeAccounting.Views;

public partial class BudgetDialog : Window
{
    public Budget Result { get; private set; } = new();
    private readonly int _id;
    private bool _ready;

    public BudgetDialog(Budget? existing = null, int? year = null, int? month = null, string? type = null)
    {
        InitializeComponent();
        ApplyLoc();

        CbType.ItemsSource = new[] { AppLoc.T("tab_expenses"), AppLoc.T("tab_incomes") };
        CbMonth.ItemsSource = Enumerable.Range(1, 12).ToList();

        if (existing == null)
        {
            _id = 0;
            CbType.SelectedIndex = type == "income" ? 1 : 0;
            TbYear.Text = (year ?? DateTime.Today.Year).ToString();
            CbMonth.SelectedIndex = (month ?? DateTime.Today.Month) - 1;
            TbPlan.Text = "0";
        }
        else
        {
            _id = existing.Id;
            CbType.SelectedIndex = existing.Type == "income" ? 1 : 0;
            TbYear.Text = existing.Year.ToString();
            CbMonth.SelectedIndex = existing.Month - 1;
            TbPlan.Text = existing.Plan.ToString(CultureInfo.InvariantCulture);
            TbNote.Text = existing.Note;
        }

        _ready = true;
        ReloadCategories(existing?.CategoryId, existing?.SubcategoryId);
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("dlg_budget_title");
        TbLblType.Text = AppLoc.T("lbl_type");
        TbLblYear.Text = AppLoc.T("lbl_year");
        TbLblMonth.Text = AppLoc.T("lbl_month");
        TbLblCategory.Text = AppLoc.T("lbl_category");
        TbLblSub.Text = AppLoc.T("lbl_subcategory");
        TbLblPlan.Text = AppLoc.T("budget_plan");
        TbLblNote.Text = AppLoc.T("col_note");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");
    }

    private string Type => CbType.SelectedIndex == 1 ? "income" : "expense";

    private void CbType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) ReloadCategories(null, null);
    }

    private void ReloadCategories(int? catId, int? subId)
    {
        var cats = CategoryService.GetAll(Type);
        CbCategory.ItemsSource = cats;
        CbCategory.SelectedItem = cats.FirstOrDefault(c => c.Id == (catId ?? -1));
        if (CbCategory.SelectedItem == null && cats.Count > 0) CbCategory.SelectedIndex = 0;
        ReloadSubs(subId);
    }

    private void CbCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) ReloadSubs(null);
    }

    private void ReloadSubs(int? subId)
    {
        var subs = new List<Subcategory> { new() { Id = 0, Name = AppLoc.T("all_filter") } };
        if (CbCategory.SelectedItem is Category c)
            subs.AddRange(CategoryService.GetSubcategories(c.Id));
        CbSub.ItemsSource = subs;
        CbSub.SelectedItem = subs.FirstOrDefault(s => s.Id == (subId ?? 0)) ?? subs[0];
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CbCategory.SelectedItem is not Category cat)
        {
            MessageBox.Show(AppLoc.T("msg_select_category"), AppLoc.T("app_title"));
            return;
        }
        if (!int.TryParse(TbYear.Text.Trim(), out var year)) year = DateTime.Today.Year;
        double plan = double.TryParse(TbPlan.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
        var sub = CbSub.SelectedItem as Subcategory;
        Result = new Budget
        {
            Id = _id, Type = Type, Year = year, Month = CbMonth.SelectedIndex + 1,
            CategoryId = cat.Id, SubcategoryId = sub?.Id > 0 ? sub.Id : null,
            CurrencyId = CurrencyService.DefaultId(), Plan = plan, Note = TbNote.Text.Trim()
        };
        DialogResult = true;
    }
}
