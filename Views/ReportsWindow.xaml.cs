using System.Windows;
using System.Windows.Controls;

namespace HomeAccounting.Views;

public partial class ReportsWindow : Window
{
    private bool _rptReady;

    public ReportsWindow()
    {
        InitializeComponent();
        ApplyLoc();

        var accounts = AccountService.GetAll(true);
        accounts.Insert(0, new Account { Id = 0, Name = AppLoc.T("all_filter") });

        CbAccount.ItemsSource   = accounts;
        CbAccount.SelectedIndex = 0;

        CbDateAccount.ItemsSource   = accounts.ToList();
        CbDateAccount.SelectedIndex = 0;

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DpFrom.SelectedDate     = monthStart;
        DpTo.SelectedDate       = DateTime.Today;
        DpDateFrom.SelectedDate = monthStart;
        DpDateTo.SelectedDate   = DateTime.Today;

        ReloadRptCategories();
        _rptReady = true;
        LoadCategoryReport();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("dlg_reports_title");

        TabByCategory.Header = AppLoc.T("tab_rpt_by_cat");
        TabByDate.Header     = AppLoc.T("tab_rpt_by_date");

        // Tab1
        TbLblFrom.Text              = AppLoc.T("lbl_from");
        TbLblTo.Text                = AppLoc.T("lbl_to");
        TbLblAccount.Text           = AppLoc.T("lbl_account");
        TbLblRptCategory.Text       = AppLoc.T("lbl_category");
        TbLblRptSubcategory.Text    = AppLoc.T("lbl_subcategory");
        RbExpenses.Content          = AppLoc.T("rb_expenses");
        RbIncomes.Content           = AppLoc.T("rb_incomes");
        BtnShow.Content             = AppLoc.T("btn_show");
        TbLblTotal.Text             = AppLoc.T("lbl_total");
        TbLblRecords.Text           = AppLoc.T("lbl_records_lbl");

        ColRptCat.Header     = AppLoc.T("col_category");
        ColRptSub.Header     = AppLoc.T("col_subcategory");
        ColRptRecords.Header = AppLoc.T("col_records");
        ColRptAmount.Header  = AppLoc.T("col_amount");

        // Tab2
        TbLblGroupBy.Text      = AppLoc.T("lbl_group_by");
        RbGroupDays.Content    = AppLoc.T("grp_days");
        RbGroupWeeks.Content   = AppLoc.T("grp_weeks");
        RbGroupMonths.Content  = AppLoc.T("grp_months");
        TbLblDateFrom.Text     = AppLoc.T("lbl_from");
        TbLblDateTo.Text       = AppLoc.T("lbl_to");
        RbDateExpenses.Content = AppLoc.T("rb_expenses");
        RbDateIncomes.Content  = AppLoc.T("rb_incomes");
        TbLblDateAccount.Text  = AppLoc.T("lbl_account");
        BtnShowByDate.Content  = AppLoc.T("btn_show");
        TbLblDateTotal.Text    = AppLoc.T("lbl_total");
        TbLblDateRecords.Text  = AppLoc.T("lbl_records_lbl");

        ColDatePeriod.Header  = AppLoc.T("col_period");
        ColDateRecords.Header = AppLoc.T("col_records");
        ColDateAmount.Header  = AppLoc.T("col_amount");
    }

    private void ReloadRptCategories()
    {
        var isExp = RbExpenses.IsChecked == true;
        var cats  = CategoryService.GetAll(isExp ? "expense" : "income");
        cats.Insert(0, new Category { Id = 0, Name = AppLoc.T("all_filter") });
        CbRptCategory.ItemsSource       = cats;
        CbRptCategory.DisplayMemberPath = "Name";
        CbRptCategory.SelectedIndex     = 0;
        ReloadRptSubcategories();
    }

    private void ReloadRptSubcategories()
    {
        var catId = (CbRptCategory.SelectedItem as Category)?.Id is int id && id != 0 ? id : (int?)null;
        var subs  = catId.HasValue ? CategoryService.GetSubcategories(catId.Value) : new List<Subcategory>();
        subs.Insert(0, new Subcategory { Id = 0, Name = AppLoc.T("all_filter") });
        CbRptSubcategory.ItemsSource       = subs;
        CbRptSubcategory.DisplayMemberPath = "Name";
        CbRptSubcategory.SelectedIndex     = 0;
    }

    private void CbRptCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_rptReady) return;
        ReloadRptSubcategories();
    }

    private void RbRptType_Checked(object sender, RoutedEventArgs e)
    {
        if (!_rptReady) return;
        ReloadRptCategories();
    }

    private void ReportTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // intentionally empty — reports load on button click only
    }

    // ─── Tab 1: By category ──────────────────────────────────────────────────

    private void BtnShow_Click(object sender, RoutedEventArgs e) => LoadCategoryReport();

    private void LoadCategoryReport()
    {
        var from   = DpFrom.SelectedDate ?? new DateTime(DateTime.Today.Year, 1, 1);
        var to     = DpTo.SelectedDate   ?? DateTime.Today;
        var acctId = (CbAccount.SelectedItem    as Account)?.Id    is int aid && aid != 0 ? aid : (int?)null;
        var catId  = (CbRptCategory.SelectedItem as Category)?.Id  is int cid && cid != 0 ? cid : (int?)null;
        var subId  = (CbRptSubcategory.SelectedItem as Subcategory)?.Id is int sid && sid != 0 ? sid : (int?)null;
        var isExp  = RbExpenses.IsChecked == true;

        var rows = isExp
            ? ReportService.GetExpensesByCategory(from, to, acctId, catId, subId)
            : ReportService.GetIncomesByCategory(from, to, acctId, catId, subId);

        DgByCategory.ItemsSource = rows;
        TbTotal.Text = $"{rows.Sum(r => r.Total):N2} ₴";
        TbCount.Text = rows.Sum(r => r.Count).ToString();
    }

    // ─── Tab 2: By date ──────────────────────────────────────────────────────

    private void BtnShowByDate_Click(object sender, RoutedEventArgs e) => LoadDateReport();

    private void LoadDateReport()
    {
        var from   = DpDateFrom.SelectedDate ?? new DateTime(DateTime.Today.Year, 1, 1);
        var to     = DpDateTo.SelectedDate   ?? DateTime.Today;
        var acctId = (CbDateAccount.SelectedItem as Account)?.Id is int id && id != 0 ? id : (int?)null;
        var isExp  = RbDateExpenses.IsChecked == true;
        var groupBy = RbGroupWeeks.IsChecked == true ? "week"
                    : RbGroupMonths.IsChecked == true ? "month"
                    : "day";

        var rows = isExp
            ? ReportService.GetExpensesByDate(from, to, groupBy, acctId)
            : ReportService.GetIncomesByDate(from, to, groupBy, acctId);

        DgByDate.ItemsSource = rows;
        TbDateTotal.Text = $"{rows.Sum(r => r.Total):N2} ₴";
        TbDateCount.Text = rows.Sum(r => r.Count).ToString();
    }
}
