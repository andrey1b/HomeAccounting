using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dapper;
using HomeAccounting.Database;
using HomeAccounting.Services;
using HomeAccounting.Views;
using QRCoder;

namespace HomeAccounting;

public partial class MainWindow : Window
{
    private bool _filtersReady;
    private bool _datesInitialized;
    private bool _trDatesInitialized;
    private bool _detDatesInitialized;
    private System.Windows.Threading.DispatcherTimer? _statusTimer;

    private List<ExpenseRow> _expenseRows = new();
    private List<IncomeRow>  _incomeRows  = new();
    private List<ShopItem>   _shopItems   = new();
    private bool _shopLoaded;
    private float _tableFontSize = 9f;
    private bool _receiptAccLoading;

    public MainWindow()
    {
        // Восстанавливаем позицию/размер до InitializeComponent, чтобы сработал WindowStartupLocation.Manual
        var s0 = AppSettings.Load();
        if (!double.IsNaN(s0.WindowTop) && !double.IsNaN(s0.WindowLeft))
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            Top    = s0.WindowTop;
            Left   = s0.WindowLeft;
            Width  = Math.Max(s0.WindowWidth,  820);
            Height = Math.Max(s0.WindowHeight, 520);
        }

        InitializeComponent();

        _tableFontSize = s0.TableFontSize > 0 ? s0.TableFontSize : 13f;
        if (!s0.ShowQrPanel) QrPanel.Visibility = System.Windows.Visibility.Collapsed;

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
        Title = $"HomeAccounting v{ver.Major}.{ver.Minor}.{ver.Build}";
        ColAccName.Binding = new Binding("Name");
        ApplyLoc();
        InitFilters();
        GeneratePhoneQr();
        RefreshAccounts(null, null);
        AppLoc.Changed += () => Dispatcher.Invoke(OnLangChanged);
        SetStatus(AppLoc.T("status_watching"));
    }

    // ─── Receipt import notification ─────────────────────────────────────────

    public void OnReceiptImported(ImportResult result)
    {
        if (result.Duplicate)
        {
            SetStatus(AppLoc.T("receipt_dup_skipped"), temporary: true);
            return;
        }
        if (result.Count == 0)
        {
            SetStatus(AppLoc.T("msg_receipt_no_items"), temporary: true);
            return;
        }

        // Uncheck filter so ALL expenses are visible regardless of date
        CbExpFilter.IsChecked = false;

        // Switch to Expenses tab and refresh
        MainTabs.SelectedIndex = 1;
        RefreshExpenses();
        RefreshAccounts(null, null);

        var msg = AppLoc.T("msg_receipt_imported", "count", result.Count.ToString());
        if (!string.IsNullOrEmpty(result.Store))
            msg += $" ({result.Store})";
        SetStatus(msg, temporary: true);
    }

    private void SetStatus(string text, bool temporary = false)
    {
        TbStatus.Text = text;
        _statusTimer?.Stop();
        if (temporary)
        {
            _statusTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(8) };
            _statusTimer.Tick += (_, _) =>
            {
                _statusTimer.Stop();
                TbStatus.Text = AppLoc.T("status_watching");
            };
            _statusTimer.Start();
        }
    }

    // ─── Window state ────────────────────────────────────────────────────────

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var s = AppSettings.Load();
        if (s.WindowMaximized)
            WindowState = System.Windows.WindowState.Maximized;
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = AppSettings.Load();
        s.WindowMaximized = WindowState == System.Windows.WindowState.Maximized;
        if (WindowState == System.Windows.WindowState.Normal)
        {
            s.WindowTop    = Top;
            s.WindowLeft   = Left;
            s.WindowWidth  = Width;
            s.WindowHeight = Height;
        }
        s.Save();

        // Сохраняем отметки списка покупок
        if (_shopLoaded)
            try { ShoppingService.SaveMarks(_shopItems); } catch { }
    }

    // ─── Phone QR panel ──────────────────────────────────────────────────────

    private void GeneratePhoneQr()
    {
        // Android QR → /setup (ComfortBuh intent)
        var url = $"http://{ReceiptHttpReceiver.LocalIp}:{ReceiptHttpReceiver.Port}/setup";
        TbPhoneQrUrl.Text = url.Replace("http://", "");
        try
        {
            using var qrGen = new QRCodeGenerator();
            var data  = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png   = new PngByteQRCode(data);
            var bytes = png.GetGraphic(4);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(bytes);
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            ImgPhoneQr.Source = bmp;
        }
        catch { }

        // iPhone QR → /web (веб-форма для отправки URL чека)
        var webUrl = $"http://{ReceiptHttpReceiver.LocalIp}:{ReceiptHttpReceiver.Port}/web";
        TbIphoneQrUrl.Text = webUrl.Replace("http://", "");
        try
        {
            using var qrGen2 = new QRCodeGenerator();
            var data2  = qrGen2.CreateQrCode(webUrl, QRCodeGenerator.ECCLevel.M);
            var png2   = new PngByteQRCode(data2);
            var bytes2 = png2.GetGraphic(4);
            var bmp2 = new BitmapImage();
            bmp2.BeginInit();
            bmp2.StreamSource = new System.IO.MemoryStream(bytes2);
            bmp2.CacheOption  = BitmapCacheOption.OnLoad;
            bmp2.EndInit();
            bmp2.Freeze();
            ImgIphoneQr.Source = bmp2;
        }
        catch { }
    }

    private void ImgPhoneQr_MouseDown(object sender, MouseButtonEventArgs e)
    {
        new InstallPhoneWindow { Owner = this }.ShowDialog();
    }

    private void OnLangChanged()
    {
        ApplyLoc();
        TbStatus.Text = AppLoc.T("status_watching");
        var tabIdx = MainTabs.SelectedIndex;
        if (tabIdx == 1) RefreshExpenses();
        else if (tabIdx == 2) RefreshIncomes();
    }

    // ─── Localization ────────────────────────────────────────────────────────

    private void ApplyLoc()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
        Title = $"{AppLoc.T("app_title")} v{ver.Major}.{ver.Minor}.{ver.Build}";

        TabAccounts.Header = AppLoc.T("tab_accounts");
        TabExpenses.Header = AppLoc.T("tab_expenses");
        TabIncomes.Header  = AppLoc.T("tab_incomes");

        TabShopping.Header   = AppLoc.T("tab_shopping");
        BtnShopAll.Content   = AppLoc.T("shop_all");
        BtnShopNone.Content  = AppLoc.T("shop_none");
        BtnShopRefresh.Content = AppLoc.T("shop_refresh");
        TbShopSearchLbl.Text = AppLoc.T("shop_search");
        BtnShopCompose.Content = AppLoc.T("shop_compose");
        ColShopName.Header   = AppLoc.T("shop_col_product");
        ColShopCat.Header    = AppLoc.T("col_category");
        ColShopQty.Header    = AppLoc.T("col_qty");
        ColShopUnit.Header   = AppLoc.T("col_unit");
        TbShopSendTitle.Text = AppLoc.T("shop_send_title");
        BtnShopPhone.Content = AppLoc.T("shop_open_phone");
        TbShopScanHint.Text  = AppLoc.T("shop_scan_hint");

        TabAccBrief.Header     = AppLoc.T("tab_acc_brief");
        TabAccDetailed.Header  = AppLoc.T("tab_acc_detailed");
        TabAccTransfers.Header = AppLoc.T("tab_acc_transfers");

        TbDetFromLbl.Text    = AppLoc.T("lbl_from");
        TbDetToLbl.Text      = AppLoc.T("lbl_to");
        TbDetAccountLbl.Text = AppLoc.T("lbl_account");
        CbDetFilter.Content  = AppLoc.T("chk_filter");
        ColDetDate.Header    = AppLoc.T("col_date");
        ColDetAcc.Header     = AppLoc.T("col_account");
        ColDetExp.Header     = AppLoc.T("col_expense");
        ColDetInc.Header     = AppLoc.T("col_income");

        MiFile.Header            = AppLoc.T("menu_file");
        MiImportPreview.Header   = AppLoc.T("menu_import_preview");
        MiImportAccounts.Header  = AppLoc.T("menu_import_accounts");
        MiImportExpenses.Header  = AppLoc.T("menu_import_expenses");
        MiImportIncomes.Header   = AppLoc.T("menu_import_incomes");
        MiExportAccounts.Header  = AppLoc.T("menu_export_accounts");
        MiExportExpenses.Header  = AppLoc.T("menu_export_expenses");
        MiExportIncomes.Header   = AppLoc.T("menu_export_incomes");
        MiLoadQr.Header          = AppLoc.T("menu_load_qr");
        MiInstallPhone.Header    = AppLoc.T("menu_install_phone");
        MiExit.Header            = AppLoc.T("menu_exit");
        MiRefs.Header    = AppLoc.T("menu_refs");
        MiCatsExp.Header = AppLoc.T("menu_cats_exp");
        MiCatsInc.Header = AppLoc.T("menu_cats_inc");
        MiCurrencies.Header = AppLoc.T("menu_currencies");
        MiAccounting.Header = AppLoc.T("menu_accounting");
        MiBudgets.Header  = AppLoc.T("menu_budgets");
        MiDebts.Header    = AppLoc.T("menu_debts");
        MiDeposits.Header = AppLoc.T("menu_deposits");
        MiUser.Header        = AppLoc.T("user_current", "name", Session.UserName);
        MiSwitchUser.Header  = AppLoc.T("user_switch");
        MiManageUsers.Header = AppLoc.T("user_manage");
        MiReports.Header  = AppLoc.T("menu_reports");
        MiCalc.Header        = AppLoc.T("menu_calc");
        MiCalcHomeB.Header   = AppLoc.T("calc_homeb");
        MiCalcWindows.Header = AppLoc.T("calc_windows");
        MiMaint.Header        = AppLoc.T("menu_maint");
        MiOpenFolder.Header   = AppLoc.T("mi_open_folder");
        MiOpenSite.Header     = AppLoc.T("mi_open_site");
        MiCheckUpdate.Header  = AppLoc.T("mi_check_update");
        MiDbExport.Header     = AppLoc.T("mi_db_export");
        MiDbImport.Header     = AppLoc.T("mi_db_import");
        MiBackupSettings.Header = AppLoc.T("mi_backup_settings");
        MiClearDb.Header      = AppLoc.T("mi_clear_db");
        MiVacuumDb.Header     = AppLoc.T("mi_vacuum_db");
        MiLang.Header    = AppLoc.T("menu_lang");
        MiLangRu.Header  = AppLoc.T("lang_ru");
        MiLangEn.Header  = AppLoc.T("lang_en");
        MiLangRu.IsChecked = AppLoc.Lang == "ru";
        MiLangEn.IsChecked = AppLoc.Lang == "en";

        MiTableTheme.Header   = AppLoc.T("menu_table_theme");
        MiThemeWindows.Header = AppLoc.T("theme_windows");
        MiThemeGarden.Header  = AppLoc.T("theme_garden");
        MiThemeWindows.IsChecked = ThemeService.Current == "Windows";
        MiThemeGarden.IsChecked  = ThemeService.Current == "Garden";

        MiFontSize.Header = AppLoc.T("menu_font_size");
        MiFont11.Header   = AppLoc.T("font_small");
        MiFont13.Header   = AppLoc.T("font_normal");
        MiFont15.Header   = AppLoc.T("font_large");
        MiFont18.Header   = AppLoc.T("font_xlarge");
        UpdateFontMenuChecks();
        MiQrPanel.Header    = AppLoc.T("menu_qr_panel");
        MiQrPanel.IsChecked = QrPanel.Visibility == Visibility.Visible;

        // Accounts sub-tab: Brief
        BtnAddAccount.Content    = AppLoc.T("btn_add");
        BtnEditAccount.Content   = AppLoc.T("btn_edit");
        BtnDeleteAccount.Content = AppLoc.T("btn_delete");
        BtnHideAccount.Content   = DgAccounts.SelectedItem is Account { IsHidden: true }
                                   ? AppLoc.T("btn_show") : AppLoc.T("btn_hide");
        CbShowHiddenAccounts.Content = AppLoc.T("chk_show_hidden");
        TbTotalBalanceLbl.Text   = AppLoc.T("lbl_total_balance");
        TbReceiptAccountLbl.Text = AppLoc.T("lbl_receipt_account");

        ColAccNum.Header     = AppLoc.T("col_num");
        ColAccIcon.Header    = AppLoc.T("col_icon");
        ColAccName.Header    = AppLoc.T("col_name");
        ColAccExpense.Header = AppLoc.T("col_expense");
        ColAccIncome.Header  = AppLoc.T("col_income");
        ColAccBalance.Header = AppLoc.T("col_balance");
        ColAccNote.Header    = AppLoc.T("col_note");

        // Accounts sub-tab: Transfers
        BtnAddTransfer.Content    = AppLoc.T("btn_add");
        BtnEditTransfer.Content   = AppLoc.T("btn_edit");
        BtnDeleteTransfer.Content = AppLoc.T("btn_delete");
        CbTrFilter.Content        = AppLoc.T("chk_filter");
        TbTrFromLbl.Text          = AppLoc.T("lbl_from");
        TbTrToLbl.Text            = AppLoc.T("lbl_to");
        TbTrTotalLbl.Text         = AppLoc.T("lbl_tr_total");

        ColTrDate.Header   = AppLoc.T("col_date");
        ColTrFrom.Header   = AppLoc.T("col_from_account");
        ColTrTo.Header     = AppLoc.T("col_to_account");
        ColTrAmount.Header = AppLoc.T("col_amount");
        ColTrNote.Header   = AppLoc.T("col_note");

        // Expenses tab
        BtnAddExpense.Content    = AppLoc.T("btn_add");
        BtnEditExpense.Content   = AppLoc.T("btn_edit");
        BtnDeleteExpense.Content = AppLoc.T("btn_delete");
        CbExpFilter.Content      = AppLoc.T("chk_filter");
        TbExpFromLbl.Text        = AppLoc.T("lbl_from");
        TbExpToLbl.Text          = AppLoc.T("lbl_to");
        TbExpAccountLbl.Text     = AppLoc.T("lbl_account");
        TbExpCategoryLbl.Text    = AppLoc.T("lbl_category");
        TbExpSubcategoryLbl.Text = AppLoc.T("lbl_subcategory");
        TbExpTodayLbl.Text       = AppLoc.T("lbl_today");
        TbExpWeekLbl.Text        = AppLoc.T("lbl_week");
        TbExpMonthLbl.Text       = AppLoc.T("lbl_month");
        TbExpFilterLbl.Text      = AppLoc.T("lbl_filter_total");

        // Заголовки столбцов таблицы расходов (0=Дата,1=Счёт,2=Кат,3=Подкат,4=Ед,5=Кол,6=Сумма,7=Прим)
        if (DgvExpenses.Columns.Count == 8)
        {
            DgvExpenses.Columns[0].Header = AppLoc.T("col_date");
            DgvExpenses.Columns[1].Header = AppLoc.T("col_account");
            DgvExpenses.Columns[2].Header = AppLoc.T("col_category");
            DgvExpenses.Columns[3].Header = AppLoc.T("col_subcategory");
            DgvExpenses.Columns[4].Header = AppLoc.T("col_unit");
            DgvExpenses.Columns[5].Header = AppLoc.T("col_qty");
            DgvExpenses.Columns[6].Header = AppLoc.T("col_amount");
            DgvExpenses.Columns[7].Header = AppLoc.T("col_note");
        }

        // Incomes tab
        BtnAddIncome.Content    = AppLoc.T("btn_add");
        BtnEditIncome.Content   = AppLoc.T("btn_edit");
        BtnDeleteIncome.Content = AppLoc.T("btn_delete");
        CbIncFilter.Content     = AppLoc.T("chk_filter");
        TbIncFromLbl.Text       = AppLoc.T("lbl_from");
        TbIncToLbl.Text         = AppLoc.T("lbl_to");
        TbIncAccountLbl.Text    = AppLoc.T("lbl_account");
        TbIncCategoryLbl.Text    = AppLoc.T("lbl_category");
        TbIncSubcategoryLbl.Text = AppLoc.T("lbl_subcategory");
        TbIncTodayLbl.Text       = AppLoc.T("lbl_today");
        TbIncWeekLbl.Text       = AppLoc.T("lbl_week");
        TbIncMonthLbl.Text      = AppLoc.T("lbl_month");
        TbIncFilterLbl.Text     = AppLoc.T("lbl_filter_total");

        // Заголовки столбцов таблицы доходов
        if (DgvIncomes.Columns.Count == 8)
        {
            DgvIncomes.Columns[0].Header = AppLoc.T("col_date");
            DgvIncomes.Columns[1].Header = AppLoc.T("col_account");
            DgvIncomes.Columns[2].Header = AppLoc.T("col_income_cat");
            DgvIncomes.Columns[3].Header = AppLoc.T("col_income_sub");
            DgvIncomes.Columns[4].Header = AppLoc.T("col_qty");
            DgvIncomes.Columns[5].Header = AppLoc.T("col_unit");
            DgvIncomes.Columns[6].Header = AppLoc.T("col_amount");
            DgvIncomes.Columns[7].Header = AppLoc.T("col_note");
        }

        if (_filtersReady) InitFilters();
    }

    // ─── Filters ─────────────────────────────────────────────────────────────

    private void InitFilters()
    {
        _filtersReady = false;

        int? expAccId = (CbExpAccount.SelectedItem    as Account)?.Id;
        int? expCatId = (CbExpCategory.SelectedItem   as Category)?.Id;
        int? expSubId = (CbExpSubcategory.SelectedItem as Subcategory)?.Id;
        int? incAccId = (CbIncAccount.SelectedItem    as Account)?.Id;
        int? incCatId = (CbIncCategory.SelectedItem   as Category)?.Id;
        int? incSubId = (CbIncSubcategory.SelectedItem as Subcategory)?.Id;

        var all = AppLoc.T("all_filter");

        var expAcc = AccountService.GetAll(true);
        expAcc.Insert(0, new Account { Id = 0, Name = all });
        CbExpAccount.ItemsSource   = expAcc;
        CbExpAccount.SelectedIndex = expAccId.HasValue ? Math.Max(0, expAcc.FindIndex(a => a.Id == expAccId.Value)) : 0;

        var expCat = CategoryService.GetAll("expense");
        expCat.Insert(0, new Category { Id = 0, Name = all });
        CbExpCategory.ItemsSource   = expCat;
        CbExpCategory.SelectedIndex = expCatId.HasValue ? Math.Max(0, expCat.FindIndex(c => c.Id == expCatId.Value)) : 0;

        ReloadExpSubcategories(expSubId);

        var incAcc = AccountService.GetAll(true);
        incAcc.Insert(0, new Account { Id = 0, Name = all });
        CbIncAccount.ItemsSource   = incAcc;
        CbIncAccount.SelectedIndex = incAccId.HasValue ? Math.Max(0, incAcc.FindIndex(a => a.Id == incAccId.Value)) : 0;

        var incCat = CategoryService.GetAll("income");
        incCat.Insert(0, new Category { Id = 0, Name = all });
        CbIncCategory.ItemsSource   = incCat;
        CbIncCategory.SelectedIndex = incCatId.HasValue ? Math.Max(0, incCat.FindIndex(c => c.Id == incCatId.Value)) : 0;

        ReloadIncSubcategories(incSubId);

        var detAcc = AccountService.GetAll(true);
        detAcc.Insert(0, new Account { Id = 0, Name = all });
        CbDetAccount.ItemsSource   = detAcc;
        CbDetAccount.SelectedIndex = 0;

        if (!_datesInitialized)
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DpExpFrom.SelectedDate = monthStart;
            DpExpTo.SelectedDate   = DateTime.Today;
            DpIncFrom.SelectedDate = monthStart;
            DpIncTo.SelectedDate   = DateTime.Today;
            _datesInitialized = true;
        }

        _filtersReady = true;
    }

    private void ReloadExpSubcategories(int? restoreId = null)
    {
        var catId = FilterId(CbExpCategory);
        var subs  = catId.HasValue ? CategoryService.GetSubcategories(catId.Value) : new List<Subcategory>();
        subs.Insert(0, new Subcategory { Id = 0, Name = AppLoc.T("all_filter") });
        CbExpSubcategory.ItemsSource       = subs;
        CbExpSubcategory.DisplayMemberPath = "Name";
        int idx = restoreId.HasValue ? Math.Max(0, subs.FindIndex(s => s.Id == restoreId.Value)) : 0;
        CbExpSubcategory.SelectedIndex = idx;
    }

    private void ReloadIncSubcategories(int? restoreId = null)
    {
        var catId = FilterId(CbIncCategory);
        var subs  = catId.HasValue ? CategoryService.GetSubcategories(catId.Value) : new List<Subcategory>();
        subs.Insert(0, new Subcategory { Id = 0, Name = AppLoc.T("all_filter") });
        CbIncSubcategory.ItemsSource       = subs;
        CbIncSubcategory.DisplayMemberPath = "Name";
        int idx = restoreId.HasValue ? Math.Max(0, subs.FindIndex(s => s.Id == restoreId.Value)) : 0;
        CbIncSubcategory.SelectedIndex = idx;
    }

    private static int? FilterId(ComboBox cb) =>
        cb.SelectedItem is Account     { Id: > 0 } a ? a.Id :
        cb.SelectedItem is Category    { Id: > 0 } c ? c.Id :
        cb.SelectedItem is Subcategory { Id: > 0 } s ? s.Id : null;

    // ─── Language menu ───────────────────────────────────────────────────────

    private void MiLang_Click(object sender, RoutedEventArgs e)
    {
        var lang = (string)((MenuItem)sender).Tag;
        AppLoc.SetLang(lang);
        var s = AppSettings.Load();
        s.Lang = lang;
        s.Save();
    }

    private void MiTheme_Click(object sender, RoutedEventArgs e)
    {
        var theme = (string)((MenuItem)sender).Tag;
        ThemeService.Apply(theme);
        var s = AppSettings.Load();
        s.Theme = theme;
        s.Save();

        MiThemeWindows.IsChecked = theme == "Windows";
        MiThemeGarden.IsChecked  = theme == "Garden";

        // перерисовать таблицы, где фон строк задаётся из кода
        RefreshAccounts(null, null);
        if (MainTabs.SelectedIndex == 1) RefreshExpenses();
        else if (MainTabs.SelectedIndex == 2) RefreshIncomes();
        if (_trDatesInitialized)  RefreshTransfers();
        if (_detDatesInitialized) RefreshDetailed();
    }

    // ─── Tabs ────────────────────────────────────────────────────────────────

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged всплывает от дочерних DataGrid — обрабатываем только смену вкладки
        if (e.OriginalSource is not TabControl) return;
        if (!_filtersReady) return;
        if (MainTabs.SelectedIndex == 1) RefreshExpenses();
        else if (MainTabs.SelectedIndex == 2) RefreshIncomes();
        else if (MainTabs.SelectedIndex == 3 && !_shopLoaded) PopulateShop();
    }

    // ─── Список покупок ──────────────────────────────────────────────────────
    private System.ComponentModel.ICollectionView? _shopView;
    private bool _composed;

    private void PopulateShop()
    {
        _shopItems = ShoppingService.GetItems();
        DgShop.ItemsSource = _shopItems;
        _shopView = System.Windows.Data.CollectionViewSource.GetDefaultView(DgShop.ItemsSource);
        _composed = false;
        _shopLoaded = true;
        TbShopHint.Text = AppLoc.T("shop_count", "count", _shopItems.Count.ToString());
    }

    // «Сформировать список»: показываем только отмеченные товары
    private void BtnShopCompose_Click(object sender, RoutedEventArgs e)
    {
        DgShop.CommitEdit(DataGridEditingUnit.Row, true);
        if (!_shopItems.Any(i => i.Include))
        {
            MessageBox.Show(AppLoc.T("shop_empty"), AppLoc.T("tab_shopping"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_shopView != null)
        {
            _shopView.Filter = o => o is ShopItem s && s.Include;
            _shopView.Refresh();
        }
        _composed = true;
    }

    // Клик в поле поиска расформировывает список (показываем все товары снова)
    private void ShopSearch_GotFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (!_composed) return;
        if (_shopView != null) { _shopView.Filter = null; _shopView.Refresh(); }
        _composed = false;
    }

    // Поиск прокручивает список к первому совпавшему товару (остальные не скрываются)
    private void ShopSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = TbShopSearch.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return;
        var item = _shopItems.FirstOrDefault(s => s.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                ?? _shopItems.FirstOrDefault(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                ?? _shopItems.FirstOrDefault(s => s.Category.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            DgShop.SelectedItem = item;
            DgShop.ScrollIntoView(item);
        }
    }

    private void BtnShopAll_Click(object sender, RoutedEventArgs e)
    { foreach (var it in _shopItems) it.Include = true;  DgShop.Items.Refresh(); }

    private void BtnShopNone_Click(object sender, RoutedEventArgs e)
    { foreach (var it in _shopItems) it.Include = false; DgShop.Items.Refresh(); }

    private void BtnShopRefresh_Click(object sender, RoutedEventArgs e)
    { if (TbShopSearch != null) TbShopSearch.Text = ""; PopulateShop(); }

    // Клик по ячейке количества открывает калькулятор
    private void ShopQty_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ShopItem s)
        {
            var dlg = new Views.CalculatorWindow(s.Qty) { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResultText))
                s.Qty = dlg.ResultText;
            e.Handled = true;
        }
    }

    private void BtnShopPhone_Click(object sender, RoutedEventArgs e)
    {
        DgShop.CommitEdit(DataGridEditingUnit.Row, true);   // зафиксировать правки количества
        var chosen = _shopItems.Where(i => i.Include).ToList();
        if (chosen.Count == 0)
        {
            MessageBox.Show(AppLoc.T("shop_empty"), AppLoc.T("tab_shopping"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ReceiptHttpReceiver.ShoppingHtml = ShoppingService.BuildHtml(chosen);
        var url = $"http://{ReceiptHttpReceiver.LocalIp}:{ReceiptHttpReceiver.Port}/shop";
        ImgShopQr.Source = MakeQr(url);
        TbShopUrl.Text = url.Replace("http://", "");
        TbShopScanHint.Visibility = ImgShopQr.Visibility = TbShopUrl.Visibility = Visibility.Visible;
    }

    private static BitmapImage MakeQr(string url)
    {
        using var gen = new QRCodeGenerator();
        var data  = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var bytes = new PngByteQRCode(data).GetGraphic(6);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new System.IO.MemoryStream(bytes);
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private void AccSubTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not TabControl) return;
        if (AccSubTabs.SelectedIndex == 0 && _filtersReady)  // Кратко
        {
            RefreshAccounts(null, null);
        }
        else if (AccSubTabs.SelectedIndex == 1)  // Подробно
        {
            if (!_detDatesInitialized)
            {
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DpDetFrom.SelectedDate = monthStart;
                DpDetTo.SelectedDate   = DateTime.Today;
                _detDatesInitialized = true;
            }
            RefreshDetailed();
        }
        if (AccSubTabs.SelectedIndex == 2)  // Переносы
        {
            if (!_trDatesInitialized)
            {
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DpTrFrom.SelectedDate = monthStart;
                DpTrTo.SelectedDate   = DateTime.Today;
                _trDatesInitialized = true;
            }
            RefreshTransfers();
        }
    }

    // ─── ACCOUNTS ────────────────────────────────────────────────────────────

    private void RefreshAccounts(object? sender, RoutedEventArgs? e)
    {
        var accounts = AccountService.GetAll(CbShowHiddenAccounts.IsChecked == true);
        for (int i = 0; i < accounts.Count; i++)
            accounts[i].RowNumber = i + 1;
        DgAccounts.ItemsSource = accounts;

        // Общий баланс сводим в базовую валюту по курсам (для гривневых счетов курс=1)
        var rates = CurrencyService.RatesToBase();
        double totalBase = accounts.Sum(a =>
            a.Balance * (a.CurrencyId.HasValue && rates.TryGetValue(a.CurrencyId.Value, out var r) ? r : 1.0));
        TbTotalBalance.Text = $"{totalBase:N2} {CurrencyService.DefaultSymbol()}";
        PopulateReceiptAccountCombo();
    }

    private void PopulateReceiptAccountCombo()
    {
        _receiptAccLoading = true;
        var saved = AppSettings.Load().DefaultAccountId;
        var accs  = AccountService.GetAll(true);
        accs.Insert(0, new Account { Id = 0, Name = AppLoc.T("none_option") });
        CbReceiptAccount.ItemsSource   = accs;
        CbReceiptAccount.SelectedIndex = saved.HasValue
            ? Math.Max(0, accs.FindIndex(a => a.Id == saved.Value))
            : 0;
        _receiptAccLoading = false;
    }

    private void CbReceiptAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_receiptAccLoading) return;
        var s   = AppSettings.Load();
        var acc = CbReceiptAccount.SelectedItem as Account;
        s.DefaultAccountId = acc?.Id > 0 ? acc.Id : null;
        s.Save();
    }

    private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
    {
        int next = AccountService.GetNextSortOrder();
        var dlg = new AccountDialog(nextSortOrder: next) { Owner = this };
        if (dlg.ShowDialog() == true) { AccountService.Add(dlg.Result); RefreshAccounts(null, null); }
    }

    private void BtnEditAccount_Click(object sender, RoutedEventArgs e) => EditSelectedAccount();
    private void DgAccounts_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedAccount();

    private void EditSelectedAccount()
    {
        if (DgAccounts.SelectedItem is not Account sel) return;
        var dlg = new AccountDialog(sel) { Owner = this };
        if (dlg.ShowDialog() == true) { AccountService.Update(dlg.Result); RefreshAccounts(null, null); }
    }

    private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        if (DgAccounts.SelectedItem is not Account sel) return;
        var msg = AppLoc.T("msg_confirm_del_account", "name", sel.Name);
        if (MessageBox.Show(msg, AppLoc.T("msg_confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes) return;
        AccountService.Delete(sel.Id);
        RefreshAccounts(null, null);
    }

    private void BtnHideAccount_Click(object sender, RoutedEventArgs e)
    {
        if (DgAccounts.SelectedItem is not Account sel) return;
        AccountService.SetHidden(sel.Id, !sel.IsHidden);
        RefreshAccounts(null, null);
    }

    // ─── Размер шрифта таблиц ────────────────────────────────────────────────

    // Размер шрифта таблиц задаётся ресурсом HA.Table.FontSize (используется стилем DataGrid во всех окнах)
    private void SetTableFontSize(float size)
    {
        _tableFontSize = Math.Clamp(size, 8f, 30f);
        Application.Current.Resources["HA.Table.FontSize"] = (double)_tableFontSize;
        var s = AppSettings.Load();
        s.TableFontSize = _tableFontSize;
        s.Save();
        UpdateFontMenuChecks();
    }

    private void UpdateFontMenuChecks()
    {
        int sz = (int)_tableFontSize;
        MiFont11.IsChecked = sz == 11;
        MiFont13.IsChecked = sz == 13;
        MiFont15.IsChecked = sz == 15;
        MiFont18.IsChecked = sz == 18;
    }

    private void MiFontSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && float.TryParse((string)mi.Tag, out var sz))
            SetTableFontSize(sz);
    }

    private void MiQrPanel_Click(object sender, RoutedEventArgs e)
    {
        bool show = MiQrPanel.IsChecked;
        QrPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        var s = AppSettings.Load(); s.ShowQrPanel = show; s.Save();
    }

    private void BtnFontMinus_Click(object sender, RoutedEventArgs e) => SetTableFontSize(_tableFontSize - 1f);
    private void BtnFontPlus_Click(object sender, RoutedEventArgs e)  => SetTableFontSize(_tableFontSize + 1f);

    // ─── Подсветка выбранной строки (WPF DataGrid: Счета и Переносы) ────────
    private static System.Windows.Media.Brush RowSelBrush => ThemeService.RowSel;
    private static System.Windows.Media.Brush RowAltBrush => ThemeService.RowAlt;

    // Базовый фон строки: записи за сегодня подсвечиваются отдельным цветом
    private static System.Windows.Media.Brush BaseBrush(object? item, int index)
    {
        if (item is IDatedRow d && d.Date.Date == DateTime.Today) return ThemeService.RowToday;
        return index % 2 == 1 ? RowAltBrush : System.Windows.Media.Brushes.White;
    }

    private static void HighlightRows(DataGrid dg, SelectionChangedEventArgs e)
    {
        foreach (var item in e.RemovedItems)
        {
            if (dg.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                row.Background = BaseBrush(item, dg.Items.IndexOf(item));
        }
        foreach (var item in e.AddedItems)
        {
            if (dg.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                row.Background = RowSelBrush;
        }
    }

    private static void HighlightRow(DataGrid dg, DataGridRow row)
    {
        row.Background = row.IsSelected ? RowSelBrush : BaseBrush(row.Item, row.GetIndex());
    }

    private void DgAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnHideAccount.Content = DgAccounts.SelectedItem is Account { IsHidden: true }
                                 ? AppLoc.T("btn_show") : AppLoc.T("btn_hide");
        HighlightRows(DgAccounts, e);
    }

    private void DgAccounts_LoadingRow(object sender, DataGridRowEventArgs e)            => HighlightRow(DgAccounts, e.Row);
    private void DgTransfers_SelectionChanged(object sender, SelectionChangedEventArgs e) => HighlightRows(DgTransfers, e);
    private void DgTransfers_LoadingRow(object sender, DataGridRowEventArgs e)            => HighlightRow(DgTransfers, e.Row);
    private void DgExpenses_SelectionChanged(object sender, SelectionChangedEventArgs e)  => HighlightRows(DgvExpenses, e);
    private void DgExpenses_LoadingRow(object sender, DataGridRowEventArgs e)             => HighlightRow(DgvExpenses, e.Row);
    private void DgIncomes_SelectionChanged(object sender, SelectionChangedEventArgs e)   => HighlightRows(DgvIncomes, e);
    private void DgIncomes_LoadingRow(object sender, DataGridRowEventArgs e)              => HighlightRow(DgvIncomes, e.Row);

    // ─── TRANSFERS ───────────────────────────────────────────────────────────

    private void RefreshTransfers()
    {
        DateTime from, to;
        if (CbTrFilter.IsChecked == true)
        {
            if (DpTrFrom.SelectedDate == null || DpTrTo.SelectedDate == null) return;
            from = DpTrFrom.SelectedDate.Value;
            to   = DpTrTo.SelectedDate.Value;
        }
        else
        {
            from = new DateTime(1900, 1, 1);
            to   = new DateTime(2099, 12, 31);
        }
        var rows = TransferService.GetFiltered(from, to);
        DgTransfers.ItemsSource = rows;
        TbTrTotal.Text = $"{rows.Sum(r => r.Amount):N2} ₴";
    }

    private void TrFilter_Changed(object sender, object e)
    {
        if (!_trDatesInitialized) return;
        if (ReferenceEquals(sender, CbTrFilter) || CbTrFilter.IsChecked == true)
            RefreshTransfers();
    }

    private void BtnAddTransfer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TransferDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            TransferService.Add(dlg.Result);
            RefreshTransfers();
            RefreshAccounts(null, null);
        }
    }

    private void BtnEditTransfer_Click(object sender, RoutedEventArgs e) => EditSelectedTransfer();
    private void DgTransfers_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedTransfer();

    private void EditSelectedTransfer()
    {
        if (DgTransfers.SelectedItem is not TransferRow row) return;
        var existing = TransferService.GetById(row.Id);
        if (existing == null) return;
        var dlg = new TransferDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            TransferService.Update(dlg.Result);
            RefreshTransfers();
            RefreshAccounts(null, null);
        }
    }

    private void BtnDeleteTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (DgTransfers.SelectedItem is not TransferRow row) return;
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        TransferService.Delete(row.Id);
        RefreshTransfers();
        RefreshAccounts(null, null);
    }

    // ─── DETAILED ────────────────────────────────────────────────────────────

    private void RefreshDetailed()
    {
        DateTime from, to;
        int? acctId;
        if (CbDetFilter.IsChecked == true)
        {
            from   = DpDetFrom.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            to     = DpDetTo.SelectedDate   ?? DateTime.Today;
            acctId = FilterId(CbDetAccount);
        }
        else
        {
            from = new DateTime(1900, 1, 1); to = new DateTime(2099, 12, 31);
            acctId = null;
        }
        DgDetailed.ItemsSource = AccountDetailService.GetGroups(from, to, acctId);
    }

    private void DetFilter_Changed(object sender, object e)
    {
        if (!_filtersReady) return;
        if (ReferenceEquals(sender, CbDetFilter) || CbDetFilter.IsChecked == true)
            RefreshDetailed();
    }

    private void BtnDetToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DetailGroup g })
        {
            g.IsExpanded = !g.IsExpanded;
            if (DgDetailed.ItemContainerGenerator.ContainerFromItem(g) is DataGridRow row)
                row.DetailsVisibility = g.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void DgDetailed_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is DetailGroup g)
            e.Row.DetailsVisibility = g.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── EXPENSES ────────────────────────────────────────────────────────────

    private void RefreshExpenses()
    {
        try
        {
            DateTime from, to;
            int? acctId, catId, subId;

            if (CbExpFilter.IsChecked == true)
            {
                from   = DpExpFrom.SelectedDate ?? new DateTime(DateTime.Today.Year, 1, 1);
                to     = DpExpTo.SelectedDate   ?? DateTime.Today;
                acctId = FilterId(CbExpAccount);
                catId  = FilterId(CbExpCategory);
                subId  = FilterId(CbExpSubcategory);
            }
            else
            {
                from = new DateTime(1900, 1, 1); to = new DateTime(2099, 12, 31);
                acctId = null; catId = null; subId = null;
            }

            var filter  = new ExpenseFilter(from, to, acctId, catId, subId);
            _expenseRows = ExpenseService.GetFiltered(filter).ToList();
            var summary  = ExpenseService.GetSummary(FilterId(CbExpAccount));

            DgvExpenses.ItemsSource = _expenseRows;

            TbExpToday.Text  = $"{summary.Today:N2} ₴";
            TbExpWeek.Text   = $"{summary.Week:N2} ₴";
            TbExpMonth.Text  = $"{summary.Month:N2} ₴";
            var expTotal = _expenseRows.Sum(r => r.Amount * (1 - r.Discount / 100));
            TbExpTotal.Text    = $"{expTotal:N2} ₴";
            TbExpRowCount.Text = $"Строк: {_expenseRows.Count}";
            TbExpRowTotal.Text = $"{expTotal:N2} ₴";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка загрузки расходов:\n" + ex.Message,
                "HomeAccounting", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExpFilter_Changed(object sender, object e)
    {
        if (!_filtersReady) return;
        if (ReferenceEquals(sender, CbExpFilter) || CbExpFilter.IsChecked == true)
            RefreshExpenses();
    }

    private void CbExpCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_filtersReady) return;
        ReloadExpSubcategories();
        if (CbExpFilter.IsChecked == true) RefreshExpenses();
    }

    private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
    {
        var more = true;
        while (more)
        {
            var dlg = new ExpenseDialog { Owner = this };
            if (dlg.ShowDialog() != true) break;
            ExpenseService.Add(dlg.Result);
            more = dlg.OpenAnother;
        }
        RefreshExpenses();
        RefreshAccounts(null, null);
    }

    private void BtnEditExpense_Click(object sender, RoutedEventArgs e) => EditSelectedExpense();

    private void DgExpenses_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgvExpenses.SelectedItem is ExpenseRow) EditSelectedExpense();
    }

    private void DgExpenses_KeyDown(object sender, KeyEventArgs e)
    {
        if      (e.Key == Key.Enter)  { EditSelectedExpense(); e.Handled = true; }
        else if (e.Key == Key.Delete)   DeleteExpense();
    }

    private ExpenseRow? SelectedExpense() => DgvExpenses.SelectedItem as ExpenseRow;

    private void EditSelectedExpense()
    {
        var row = SelectedExpense();
        if (row == null) { MessageBox.Show(AppLoc.T("msg_select_row")); return; }
        var existing = ExpenseService.GetById(row.Id);
        if (existing == null) return;
        var dlg = new ExpenseDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ExpenseService.Update(dlg.Result);
            if (!string.IsNullOrEmpty(existing.ReceiptItemName))
                ItemMappingService.Save(existing.ReceiptItemName,
                    dlg.Result.CategoryId, dlg.Result.SubcategoryId, dlg.Result.UnitId);
            RefreshExpenses();
            RefreshAccounts(null, null);
        }
    }

    private void BtnDeleteExpense_Click(object sender, RoutedEventArgs e) => DeleteExpense();

    private void DeleteExpense()
    {
        var row = SelectedExpense();
        if (row == null) { MessageBox.Show(AppLoc.T("msg_select_row")); return; }
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ExpenseService.Delete(row.Id);
        RefreshExpenses();
        RefreshAccounts(null, null);
    }

    // ─── INCOMES ─────────────────────────────────────────────────────────────

    private void RefreshIncomes()
    {
        try
        {
            DateTime from, to;
            int? acctId, catId, subId;

            if (CbIncFilter.IsChecked == true)
            {
                from   = DpIncFrom.SelectedDate ?? new DateTime(DateTime.Today.Year, 1, 1);
                to     = DpIncTo.SelectedDate   ?? DateTime.Today;
                acctId = FilterId(CbIncAccount);
                catId  = FilterId(CbIncCategory);
                subId  = FilterId(CbIncSubcategory);
            }
            else
            {
                from = new DateTime(1900, 1, 1); to = new DateTime(2099, 12, 31);
                acctId = null; catId = null; subId = null;
            }

            var filter  = new IncomeFilter(from, to, acctId, catId, subId);
            _incomeRows = IncomeService.GetFiltered(filter).ToList();
            var summary  = IncomeService.GetSummary(FilterId(CbIncAccount));

            DgvIncomes.ItemsSource = _incomeRows;

            TbIncToday.Text  = $"{summary.Today:N2} ₴";
            TbIncWeek.Text   = $"{summary.Week:N2} ₴";
            TbIncMonth.Text  = $"{summary.Month:N2} ₴";
            var incTotal = _incomeRows.Sum(r => r.Amount);
            TbIncTotal.Text    = $"{incTotal:N2} ₴";
            TbIncRowCount.Text = $"Строк: {_incomeRows.Count}";
            TbIncRowTotal.Text = $"{incTotal:N2} ₴";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка загрузки доходов:\n" + ex.Message,
                "HomeAccounting", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void IncFilter_Changed(object sender, object e)
    {
        if (!_filtersReady) return;
        if (ReferenceEquals(sender, CbIncFilter) || CbIncFilter.IsChecked == true)
            RefreshIncomes();
    }

    private void CbIncCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_filtersReady) return;
        ReloadIncSubcategories();
        if (CbIncFilter.IsChecked == true) RefreshIncomes();
    }

    private void BtnAddIncome_Click(object sender, RoutedEventArgs e)
    {
        var more = true;
        while (more)
        {
            var dlg = new IncomeDialog { Owner = this };
            if (dlg.ShowDialog() != true) break;
            IncomeService.Add(dlg.Result);
            more = dlg.OpenAnother;
        }
        RefreshIncomes();
        RefreshAccounts(null, null);
    }

    private void BtnEditIncome_Click(object sender, RoutedEventArgs e) => EditSelectedIncome();

    private void DgIncomes_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgvIncomes.SelectedItem is IncomeRow) EditSelectedIncome();
    }

    private void DgIncomes_KeyDown(object sender, KeyEventArgs e)
    {
        if      (e.Key == Key.Enter)  { EditSelectedIncome(); e.Handled = true; }
        else if (e.Key == Key.Delete)   DeleteIncome();
    }

    private IncomeRow? SelectedIncome() => DgvIncomes.SelectedItem as IncomeRow;

    private void EditSelectedIncome()
    {
        var row = SelectedIncome();
        if (row == null) { MessageBox.Show(AppLoc.T("msg_select_row")); return; }
        var existing = IncomeService.GetById(row.Id);
        if (existing == null) return;
        var dlg = new IncomeDialog(existing) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            IncomeService.Update(dlg.Result);
            RefreshIncomes();
            RefreshAccounts(null, null);
        }
    }

    private void BtnDeleteIncome_Click(object sender, RoutedEventArgs e) => DeleteIncome();

    private void DeleteIncome()
    {
        var row = SelectedIncome();
        if (row == null) { MessageBox.Show(AppLoc.T("msg_select_row")); return; }
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        IncomeService.Delete(row.Id);
        RefreshIncomes();
        RefreshAccounts(null, null);
    }

    // ─── Menu ─────────────────────────────────────────────────────────────────

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    public void ShowUpdateBanner(string tag)
    {
        TbUpdateBanner.Text = $"🔔 Доступна новая версия {tag} — нажмите для перехода на страницу загрузки";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void UpdateBanner_MouseDown(object sender, MouseButtonEventArgs e) =>
        Services.UpdateChecker.OpenReleasesPage();

    private void MiLoadQr_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.LoadQrWindow { Owner = this };
        if (dlg.ShowDialog() != true) return;

        SetStatus(AppLoc.T("msg_loading_receipt"));
        var url = dlg.QrUrl;
        Task.Run(() =>
        {
            var (result, error) = Services.ReceiptHttpReceiver.FetchAndImport(url);
            Dispatcher.Invoke(() =>
            {
                if (result != null && result.Duplicate)
                {
                    OnReceiptImported(result);
                }
                else if (result != null && result.Count > 0)
                {
                    OnReceiptImported(result);
                }
                else if (result != null && result.Count == 0)
                {
                    var logPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "HomeAccounting", "last_receipt.xml");
                    MessageBox.Show(
                        "Чек получен из ДПС, но позиции товаров не распознаны.\n\n" +
                        "XML-файл для диагностики сохранён:\n" + logPath,
                        "Загрузка QR", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus(AppLoc.T("msg_receipt_no_items"), temporary: true);
                }
                else
                {
                    var msg = error == "receipt_not_in_registry"
                        ? "Чек не найден в реестре ДПС.\n\nМожливі причини:\n• чек ещё не зарегистрирован (попробуйте через несколько минут)\n• неверная дата или номер чека в QR-коде"
                        : error ?? "Неизвестная ошибка";
                    MessageBox.Show(msg, "Загрузка QR", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus(AppLoc.T("status_ready"));
                }
            });
        });
    }

    private void MiInstallPhone_Click(object sender, RoutedEventArgs e)
    {
        new Views.InstallPhoneWindow { Owner = this }.ShowDialog();
    }

    private void MiImportAccounts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "Excel Files|*.xlsx", Title = AppLoc.T("menu_import_accounts") };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var r = XlsxService.ImportAccounts(dlg.FileName);
            var msg = AppLoc.T("msg_import_ok",   "count", r.Imported.ToString()) + "\n" +
                      AppLoc.T("msg_import_skip", "skip",  r.Skipped.ToString());
            if (r.Errors.Count > 0) msg += "\n\n" + string.Join("\n", r.Errors.Take(10));
            MessageBox.Show(msg, AppLoc.T("menu_import_accounts"), MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshAccounts(null, null);
            InitFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_import_accounts"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiExportAccounts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Excel Files|*.xlsx",
            FileName = $"Рахунки_{DateTime.Today:yyyy-MM-dd}.xlsx",
            Title    = AppLoc.T("menu_export_accounts")
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            XlsxService.ExportAccounts(dlg.FileName);
            SetStatus(AppLoc.T("menu_export_accounts") + " ✓", temporary: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_export_accounts"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiImportExpenses_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "Excel Files|*.xlsx", Title = AppLoc.T("menu_import_expenses") };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var r = XlsxService.ImportExpenses(dlg.FileName);
            var msg = AppLoc.T("msg_import_ok",   "count", r.Imported.ToString()) + "\n" +
                      AppLoc.T("msg_import_skip", "skip",  r.Skipped.ToString());
            if (r.Errors.Count > 0) msg += "\n\n" + string.Join("\n", r.Errors.Take(10));
            MessageBox.Show(msg, AppLoc.T("menu_import_expenses"), MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshExpenses();
            RefreshAccounts(null, null);
            InitFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_import_expenses"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiImportPreview_Click(object sender, RoutedEventArgs e)
    {
        var wnd = new Views.ImportPreviewWindow { Owner = this };
        wnd.ShowDialog();
        // Обновляем все таблицы — импорт мог добавить записи
        RefreshExpenses();
        RefreshIncomes();
        RefreshAccounts(null, null);
        InitFilters();
    }

    private void MiImportIncomes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "Excel Files|*.xlsx", Title = AppLoc.T("menu_import_incomes") };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var r = XlsxService.ImportIncomes(dlg.FileName);
            var msg = AppLoc.T("msg_import_ok",   "count", r.Imported.ToString()) + "\n" +
                      AppLoc.T("msg_import_skip", "skip",  r.Skipped.ToString());
            if (r.Errors.Count > 0) msg += "\n\n" + string.Join("\n", r.Errors.Take(10));
            MessageBox.Show(msg, AppLoc.T("menu_import_incomes"), MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshIncomes();
            RefreshAccounts(null, null);
            InitFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_import_incomes"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiExportExpenses_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Excel Files|*.xlsx",
            FileName = $"Расходы_{DateTime.Today:yyyy-MM-dd}.xlsx",
            Title    = AppLoc.T("menu_export_expenses")
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            XlsxService.ExportExpenses(dlg.FileName);
            SetStatus(AppLoc.T("menu_export_expenses") + " ✓", temporary: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_export_expenses"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiExportIncomes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Excel Files|*.xlsx",
            FileName = $"Доходы_{DateTime.Today:yyyy-MM-dd}.xlsx",
            Title    = AppLoc.T("menu_export_incomes")
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            XlsxService.ExportIncomes(dlg.FileName);
            SetStatus(AppLoc.T("menu_export_incomes") + " ✓", temporary: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("menu_export_incomes"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuReports_Click(object sender, RoutedEventArgs e) =>
        new ReportsWindow { Owner = this }.ShowDialog();

    private void MiCalcHomeB_Click(object sender, RoutedEventArgs e) =>
        new Views.CalculatorWindow(standalone: true) { Owner = this }.Show();

    private void MiCalcWindows_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("calc.exe") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, AppLoc.T("menu_calc"), MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    // ─── Новые разделы (Учёт / Справочники) ──────────────────────────────────
    private void MiBudgets_Click(object sender, RoutedEventArgs e) =>
        new Views.BudgetsWindow { Owner = this }.ShowDialog();

    private void MiDebts_Click(object sender, RoutedEventArgs e) =>
        new Views.DebtsWindow { Owner = this }.ShowDialog();

    private void MiDeposits_Click(object sender, RoutedEventArgs e) =>
        new Views.DepositsWindow { Owner = this }.ShowDialog();

    private void MiCurrencies_Click(object sender, RoutedEventArgs e) =>
        new Views.CurrencyManagerWindow { Owner = this }.ShowDialog();

    // ─── Пользователи ────────────────────────────────────────────────────────
    private void MiSwitchUser_Click(object sender, RoutedEventArgs e)
    {
        var login = new Views.LoginWindow { Owner = this };
        if (login.ShowDialog() != true) return;
        Session.Set(login.SelectedUserId, login.SelectedUserName);
        ReloadForCurrentUser();
    }

    private void MiManageUsers_Click(object sender, RoutedEventArgs e)
    {
        new Views.UsersWindow { Owner = this }.ShowDialog();
        ApplyLoc();
    }

    private void ReloadForCurrentUser()
    {
        ApplyLoc();                 // обновляет имя пользователя в меню
        InitFilters();
        RefreshAccounts(null, null);
        _datesInitialized = _trDatesInitialized = _detDatesInitialized = false;
        if (MainTabs.SelectedIndex == 1) RefreshExpenses();
        else if (MainTabs.SelectedIndex == 2) RefreshIncomes();
        SetStatus(AppLoc.T("user_current", "name", Session.UserName), temporary: true);
    }

    private void MiOpenFolder_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start("explorer.exe", AppContext.BaseDirectory);

    private void MiOpenSite_Click(object sender, RoutedEventArgs e) =>
        Services.UpdateChecker.OpenReleasesPage();

    private void MiCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        MiCheckUpdate.IsEnabled = false;
        Services.UpdateChecker.CheckAsync((tag, err) =>
            Dispatcher.Invoke(() =>
            {
                MiCheckUpdate.IsEnabled = true;
                if (tag != null)
                    ShowUpdateBanner(tag);
                else if (err)
                    MessageBox.Show(AppLoc.T("msg_update_error"), "HomeAccounting",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(AppLoc.T("msg_no_updates"), "HomeAccounting",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }));
    }

    private void MiClearDb_Click(object sender, RoutedEventArgs e)
    {
        // Первое подтверждение
        var r1 = MessageBox.Show(
            "Будут удалены ВСЕ данные:\nрасходы, доходы, переносы, счета, категории, подкатегории, единицы измерения.\n\nПродолжить?",
            "Очистить базу данных",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r1 != MessageBoxResult.Yes) return;

        // Второе подтверждение
        var r2 = MessageBox.Show(
            "Вы уверены? Это действие НЕОБРАТИМО.\nВосстановление данных будет невозможно.",
            "Очистить базу данных — подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Stop);
        if (r2 != MessageBoxResult.Yes) return;

        using var conn = Db.Open();
        using var tr = conn.BeginTransaction();
        var p = new { uid = Session.UserId };
        conn.Execute("DELETE FROM item_mappings WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM expenses      WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM incomes       WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM transfers     WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM budgets       WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM debts         WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM deposits      WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM subcategories WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM categories    WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM accounts      WHERE user_id=@uid", p, tr);
        conn.Execute("DELETE FROM units         WHERE user_id=@uid", p, tr);
        tr.Commit();

        RefreshAccounts(null, null);
        RefreshExpenses();
        RefreshIncomes();
        RefreshTransfers();
        RefreshDetailed();
        MessageBox.Show("База данных очищена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MiVacuumDb_Click(object sender, RoutedEventArgs e)
    {
        using var conn = Db.Open();
        conn.Execute("VACUUM");
        MessageBox.Show("База данных сжата.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ─── Перенос базы данных ─────────────────────────────────────────────────
    private void MiDbExport_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "База данных|*.db",
            FileName = $"homeaccounting_backup_{DateTime.Today:yyyy-MM-dd}.db",
            Title    = AppLoc.T("mi_db_export")
        };
        if (sfd.ShowDialog() != true) return;
        try
        {
            Db.Backup(sfd.FileName);
            MessageBox.Show(AppLoc.T("msg_db_exported", "path", sfd.FileName),
                AppLoc.T("mi_db_export"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("mi_db_export"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MiBackupSettings_Click(object sender, RoutedEventArgs e)
    {
        new Views.BackupSettingsWindow { Owner = this }.ShowDialog();
    }

    private void MiDbImport_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "База данных|*.db",
            Title  = AppLoc.T("mi_db_import")
        };
        if (ofd.ShowDialog() != true) return;

        if (!Db.IsValidDb(ofd.FileName))
        {
            MessageBox.Show(AppLoc.T("msg_db_invalid"), AppLoc.T("mi_db_import"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(AppLoc.T("msg_db_import_confirm"), AppLoc.T("mi_db_import"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            Db.RestoreFrom(ofd.FileName);
            MessageBox.Show(AppLoc.T("msg_db_restored"), AppLoc.T("mi_db_import"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            // перезапуск, чтобы применить миграции и перечитать данные
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) System.Diagnostics.Process.Start(exe);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("mi_db_import"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuCatsExpense_Click(object sender, RoutedEventArgs e)
    {
        new CategoryManagerWindow("expense") { Owner = this }.ShowDialog();
        InitFilters();
    }

    private void MenuCatsIncome_Click(object sender, RoutedEventArgs e)
    {
        new CategoryManagerWindow("income") { Owner = this }.ShowDialog();
        InitFilters();
    }
}
