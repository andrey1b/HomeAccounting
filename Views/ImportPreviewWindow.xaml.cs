using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using HomeAccounting.Models;
using HomeAccounting.Services;
using Microsoft.Win32;

namespace HomeAccounting.Views;

public partial class ImportPreviewWindow : Window
{
    private ObservableCollection<ExpImportRow> _expRows = [];
    private ObservableCollection<IncImportRow> _incRows = [];
    private ObservableCollection<AccImportRow> _accRows = [];
    private ObservableCollection<DetImportRow> _detRows = [];

    public ImportPreviewWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DgExp.ItemsSource = _expRows;
        DgInc.ItemsSource = _incRows;
        DgAcc.ItemsSource = _accRows;
        DgDet.ItemsSource = _detRows;
        ApplyLoc();
        UpdateStatus();
    }

    private void ApplyLoc()
    {
        Title                = AppLoc.T("dlg_import_preview_title");
        TbFileLabel.Text     = AppLoc.T("lbl_imp_file");
        BtnBrowse.Content    = AppLoc.T("btn_open_file");
        BtnEdit.Content      = AppLoc.T("btn_edit");
        BtnDelete.Content    = AppLoc.T("btn_delete");
        BtnDelAll.Content    = AppLoc.T("btn_del_all");
        BtnImport.Content    = AppLoc.T("btn_import");
        BtnClose.Content     = AppLoc.T("btn_cancel");
        TabAcc.Header        = AppLoc.T("tab_imp_acc_brief");
        TabExp.Header        = AppLoc.T("tab_expenses");
        TabInc.Header        = AppLoc.T("tab_incomes");
        TabDet.Header        = AppLoc.T("tab_imp_detailed");

        // Расходы
        ColExpDate.Header = AppLoc.T("col_date");
        ColExpAcc.Header  = AppLoc.T("col_account");
        ColExpCat.Header  = AppLoc.T("col_category");
        ColExpSub.Header  = AppLoc.T("col_subcategory");
        ColExpUnit.Header = AppLoc.T("col_unit");
        ColExpQty.Header  = AppLoc.T("col_qty");
        ColExpAmt.Header  = AppLoc.T("col_amount");
        ColExpNote.Header = AppLoc.T("col_note");

        // Доходы
        ColIncDate.Header = AppLoc.T("col_date");
        ColIncAcc.Header  = AppLoc.T("col_account");
        ColIncCat.Header  = AppLoc.T("col_income_cat");
        ColIncSub.Header  = AppLoc.T("col_income_sub");
        ColIncQty.Header  = AppLoc.T("col_qty");
        ColIncUnit.Header = AppLoc.T("col_unit");
        ColIncAmt.Header  = AppLoc.T("col_amount");
        ColIncNote.Header = AppLoc.T("col_note");

        // Счета
        ColAccName.Header = AppLoc.T("col_name");
        ColAccBal.Header  = AppLoc.T("col_init_balance");
        ColAccNote.Header = AppLoc.T("col_note");

        // Счета подробно
        ColDetDate.Header = AppLoc.T("col_date");
        ColDetAcc.Header  = AppLoc.T("col_account");
        ColDetExp.Header  = AppLoc.T("col_imp_expense");
        ColDetInc.Header  = AppLoc.T("col_imp_income");
        ColDetOth.Header  = AppLoc.T("col_other_ops");
        ColDetTurn.Header = AppLoc.T("col_turnover");
        ColDetNote.Header = AppLoc.T("col_note");
    }

    // ── Открытие файла ────────────────────────────────────────────────────────

    // Ключевые слова для проверки имени файла: индекс вкладки → (ключевое слово, название вкладки)
    private static readonly (string Keyword, string TabName)[] _tabFileKeywords =
    [
        ("счет",    "Счета кратко"),
        ("расход",  "Расходы"),
        ("доход",   "Доходы"),
        ("подробн", "Счета подробно"),
    ];

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            Title  = AppLoc.T("btn_open_file")
        };
        if (dlg.ShowDialog() != true) return;

        // Проверка: имя файла должно содержать ключевое слово активной вкладки
        int idx = TabImport.SelectedIndex;
        if (idx >= 0 && idx < _tabFileKeywords.Length)
        {
            string fileNameLower = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName).ToLower();
            var (keyword, tabName) = _tabFileKeywords[idx];
            if (!fileNameLower.Contains(keyword))
            {
                string fileName = System.IO.Path.GetFileName(dlg.FileName);
                var ans = MessageBox.Show(
                    $"Файл «{fileName}» не похож на файл «{tabName}».\nВозможно выбран не тот файл. Продолжить?",
                    AppLoc.T("dlg_import_preview_title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ans == MessageBoxResult.No) return;
            }
        }

        TbFilePath.Text = dlg.FileName;
        LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        _expRows.Clear();
        _incRows.Clear();
        _accRows.Clear();
        _detRows.Clear();

        try
        {
            var (expRows, _) = XlsxService.ParseExpenses(path);
            foreach (var r in expRows) _expRows.Add(r);

            var (incRows, _) = XlsxService.ParseIncomes(path);
            foreach (var r in incRows) _incRows.Add(r);

            var (accRows, _) = XlsxService.ParseAccounts(path);
            foreach (var r in accRows) _accRows.Add(r);

            var (detRows, _) = XlsxService.ParseDetailed(path);
            foreach (var r in detRows) _detRows.Add(r);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("dlg_import_preview_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Активируем вкладку с наибольшим числом строк
        int maxRows = Math.Max(Math.Max(_expRows.Count, _incRows.Count),
                               Math.Max(_accRows.Count, _detRows.Count));
        if      (maxRows == _detRows.Count && _detRows.Count > 0) TabImport.SelectedItem = TabDet;
        else if (maxRows == _expRows.Count && _expRows.Count > 0) TabImport.SelectedItem = TabExp;
        else if (maxRows == _incRows.Count && _incRows.Count > 0) TabImport.SelectedItem = TabInc;
        else if (_accRows.Count > 0)                               TabImport.SelectedItem = TabAcc;

        UpdateTabHeaders();
        UpdateStatus();
    }

    private void UpdateTabHeaders()
    {
        TabAcc.Header = $"{AppLoc.T("tab_imp_acc_brief")} ({_accRows.Count})";
        TabExp.Header = $"{AppLoc.T("tab_expenses")} ({_expRows.Count})";
        TabInc.Header = $"{AppLoc.T("tab_incomes")} ({_incRows.Count})";
        TabDet.Header = $"{AppLoc.T("tab_imp_detailed")} ({_detRows.Count})";
    }

    private void UpdateStatus()
    {
        int count = ActiveRowCount();
        TbStatus.Text = $"{AppLoc.T("lbl_imp_rows")} {count}";
    }

    private int ActiveRowCount() => TabImport.SelectedIndex switch
    {
        0 => _accRows.Count,
        1 => _expRows.Count,
        2 => _incRows.Count,
        3 => _detRows.Count,
        _ => 0
    };

    private void TabImport_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateStatus();
    }

    // ── Редактирование строки ─────────────────────────────────────────────────

    private DataGrid? ActiveDataGrid() => TabImport.SelectedIndex switch
    {
        0 => DgAcc,
        1 => DgExp,
        2 => DgInc,
        3 => DgDet,
        _ => null
    };

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        var dg = ActiveDataGrid();
        if (dg?.SelectedItem == null)
        {
            MessageBox.Show(AppLoc.T("msg_select_row"), AppLoc.T("dlg_import_preview_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        dg.Focus();
        dg.ScrollIntoView(dg.SelectedItem);
        if (dg.Columns.Count > 0)
            dg.CurrentCell = new DataGridCellInfo(dg.SelectedItem, dg.Columns[0]);
        dg.BeginEdit();
    }

    // ── Удаление строк ────────────────────────────────────────────────────────

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        switch (TabImport.SelectedIndex)
        {
            case 0: DeleteSelected(DgAcc, _accRows); break;
            case 1: DeleteSelected(DgExp, _expRows); break;
            case 2: DeleteSelected(DgInc, _incRows); break;
            case 3: DeleteSelected(DgDet, _detRows); break;
        }
        UpdateTabHeaders();
        UpdateStatus();
    }

    private void BtnDelAll_Click(object sender, RoutedEventArgs e)
    {
        switch (TabImport.SelectedIndex)
        {
            case 0: _accRows.Clear(); break;
            case 1: _expRows.Clear(); break;
            case 2: _incRows.Clear(); break;
            case 3: _detRows.Clear(); break;
        }
        UpdateTabHeaders();
        UpdateStatus();
    }

    private static void DeleteSelected<T>(DataGrid dg, ObservableCollection<T> list)
    {
        var selected = dg.SelectedItems.Cast<T>().ToList();
        foreach (var item in selected) list.Remove(item);
    }

    // ── Импорт ───────────────────────────────────────────────────────────────

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveRowCount() == 0)
        {
            MessageBox.Show(AppLoc.T("msg_imp_no_rows"),
                AppLoc.T("dlg_import_preview_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        XlsxImportResult result;
        try
        {
            result = TabImport.SelectedIndex switch
            {
                0 => XlsxService.CommitAccounts(_accRows),
                1 => XlsxService.CommitExpenses(_expRows),
                2 => XlsxService.CommitIncomes(_incRows),
                3 => XlsxService.CommitDetailed(_detRows),
                _ => new XlsxImportResult(0, 0, [])
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("dlg_import_preview_title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string msg = AppLoc.T("msg_import_ok", "count", result.Imported.ToString());
        if (result.Skipped > 0)
            msg += "\n" + AppLoc.T("msg_import_skip", "skip", result.Skipped.ToString());
        if (result.Errors.Count > 0)
            msg += "\n" + string.Join("\n", result.Errors.Take(5));

        MessageBox.Show(msg, AppLoc.T("dlg_import_preview_title"),
            MessageBoxButton.OK, MessageBoxImage.Information);

        // Очищаем вкладку после успешного импорта
        switch (TabImport.SelectedIndex)
        {
            case 0: _accRows.Clear(); break;
            case 1: _expRows.Clear(); break;
            case 2: _incRows.Clear(); break;
            case 3: _detRows.Clear(); break;
        }
        UpdateTabHeaders();
        UpdateStatus();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
