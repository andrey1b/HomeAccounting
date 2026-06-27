using ClosedXML.Excel;
using Dapper;
using HomeAccounting.Database;
using HomeAccounting.Models;

namespace HomeAccounting.Services;

public record XlsxImportResult(int Imported, int Skipped, List<string> Errors);

public static class XlsxService
{
    // ── Export ────────────────────────────────────────────────────────────────

    public static void ExportExpenses(string path)
    {
        var rows = ExpenseService.GetFiltered(
            new ExpenseFilter(new DateTime(1900, 1, 1), new DateTime(2099, 12, 31)));

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Расходы");
        WriteHeader(ws, "Дата","Счёт","Категория","Подкатегория","Ед. изм.","Кол-во","Сумма","Примечание");

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.DateStr;
            ws.Cell(r, 2).Value = row.AccountName;
            ws.Cell(r, 3).Value = row.CategoryName;
            ws.Cell(r, 4).Value = row.SubcategoryName;
            ws.Cell(r, 5).Value = row.UnitName;
            ws.Cell(r, 6).Value = row.QtyStr;
            ws.Cell(r, 7).Value = row.EffectiveAmount;
            ws.Cell(r, 8).Value = row.Note;
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    public static void ExportIncomes(string path)
    {
        var rows = IncomeService.GetFiltered(
            new IncomeFilter(new DateTime(1900, 1, 1), new DateTime(2099, 12, 31)));

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Доходы");
        WriteHeader(ws, "Дата","Счёт","Категория дохода","Подкатегория дохода","Кол-во","Ед. изм.","Сумма","Примечание");

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.DateStr;
            ws.Cell(r, 2).Value = row.AccountName;
            ws.Cell(r, 3).Value = row.CategoryName;
            ws.Cell(r, 4).Value = row.SubcategoryName;
            ws.Cell(r, 5).Value = row.QtyStr;
            ws.Cell(r, 6).Value = row.UnitName;
            ws.Cell(r, 7).Value = row.Amount;
            ws.Cell(r, 8).Value = row.Note;
            r++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    public static void ExportAccounts(string path)
    {
        var accounts = AccountService.GetAll(true);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Рахунки");
        WriteHeader(ws, "N", "Рахунок", "Поч. баланс", "Витрати", "Дохід", "Баланс", "Примітка");
        int r = 2;
        foreach (var a in accounts)
        {
            ws.Cell(r, 1).Value = a.SortOrder;
            ws.Cell(r, 2).Value = a.Name;
            ws.Cell(r, 3).Value = a.InitialBalance;
            ws.Cell(r, 4).Value = a.TotalExpense;
            ws.Cell(r, 5).Value = a.TotalIncome;
            ws.Cell(r, 6).Value = a.Balance;
            ws.Cell(r, 7).Value = a.Note;
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    public static XlsxImportResult ImportAccounts(string path)
    {
        int imported = 0, skipped = 0;
        var errors = new List<string>();
        try
        {
            using var wb = new XLWorkbook(path);
            var ws   = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);

            int cName = FindCol(hdrs, "рахунок", "счёт", "счет", "account", "назва", "name");
            int cNote = FindCol(hdrs, "примітка", "примечание", "note");
            int cInit = FindCol(hdrs, "поч. баланс", "нач. баланс", "поч.баланс", "нач.баланс", "initial balance");

            if (cName < 0)
            {
                errors.Add("Не знайдено колонку «Рахунок».");
                return new XlsxImportResult(0, 0, errors);
            }

            using var conn = Db.Open();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var name = Cell(row, cName);
                if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                bool exists = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM accounts WHERE user_id=@uid AND LOWER(name)=LOWER(@n)",
                    new { uid = Session.UserId, n = name }) > 0;
                if (exists) { skipped++; continue; }

                double initBal = 0;
                if (cInit >= 0) TryParseAmount(Cell(row, cInit), out initBal);
                var note = cNote >= 0 ? Cell(row, cNote) : "";
                int nextSort = conn.ExecuteScalar<int>(
                    "SELECT COALESCE(MAX(sort_order),0)+1 FROM accounts WHERE user_id=@uid", new { uid = Session.UserId });
                conn.Execute(
                    "INSERT INTO accounts(user_id,name,sort_order,initial_balance,note) VALUES(@uid,@n,@s,@b,@nt)",
                    new { uid = Session.UserId, n = name, s = nextSort, b = initBal, nt = note });
                imported++;
            }
        }
        catch (Exception ex) { errors.Add(ex.Message); }
        return new XlsxImportResult(imported, skipped, errors);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public static XlsxImportResult ImportExpenses(string path)
    {
        int imported = 0, skipped = 0;
        var errors = new List<string>();

        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);

            int cDate = FindCol(hdrs, "дата", "date");
            int cAcc  = FindCol(hdrs, "счёт", "счет", "рахунок", "account");
            int cCat  = FindCol(hdrs, "категория", "категорія", "категория расхода", "category");
            int cSub  = FindCol(hdrs, "подкатегория", "підкатегорія", "подкатегория расхода", "subcategory");
            int cUnit = FindCol(hdrs, "ед. изм.", "ед.изм.", "од. вим.", "unit");
            int cQty  = FindCol(hdrs, "кол-во", "кол.", "к-сть", "quantity");
            int cAmt  = FindCol(hdrs, "сумма", "сума", "гривны|сумма", "amount");
            int cNote = FindCol(hdrs, "примечание", "примітка", "note");

            if (cDate < 0 || cAmt < 0)
            {
                var missing = new List<string>();
                if (cDate < 0) missing.Add("Дата");
                if (cAmt < 0) missing.Add("Сумма");
                var found = string.Join(", ", hdrs.Keys.Take(10));
                errors.Add($"Не найдены столбцы: {string.Join(", ", missing)}.\nВ файле найдены: {found}");
                return new XlsxImportResult(0, 0, errors);
            }

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                try
                {
                    var dateStr = Cell(row, cDate);
                    if (string.IsNullOrWhiteSpace(dateStr)) { skipped++; continue; }
                    if (!TryParseDate(dateStr, out var date))
                    {
                        errors.Add($"Строка {row.RowNumber()}: неверная дата «{dateStr}»");
                        skipped++;
                        continue;
                    }

                    if (!TryParseAmount(Cell(row, cAmt), out var amount)) { skipped++; continue; }

                    var accName  = cAcc  >= 0 ? Cell(row, cAcc)  : "";
                    var catName  = cCat  >= 0 ? Cell(row, cCat)  : "";
                    var subName  = cSub  >= 0 ? Cell(row, cSub)  : "";
                    var unitName = cUnit >= 0 ? Cell(row, cUnit) : "";
                    double? qty  = cQty >= 0 && TryParseAmount(Cell(row, cQty), out var qv) ? qv : null;
                    var note     = cNote >= 0 ? Cell(row, cNote) : "";

                    int? accId  = string.IsNullOrEmpty(accName) ? null : GetOrCreateAccount(accName);
                    int? catId  = string.IsNullOrEmpty(catName) ? null : GetOrCreateCategory(catName, "expense");
                    int? subId  = !string.IsNullOrEmpty(subName) && catId.HasValue
                                  ? GetOrCreateSubcategory(subName, catId.Value) : null;
                    int? unitId = string.IsNullOrEmpty(unitName) ? null : GetOrCreateUnit(unitName);

                    ExpenseService.Add(new Expense
                    {
                        Date = date, AccountId = accId, CategoryId = catId,
                        SubcategoryId = subId, Quantity = qty, UnitId = unitId,
                        Amount = amount, Discount = 0, Note = note
                    });
                    imported++;
                }
                catch (Exception ex) { errors.Add($"Строка {row.RowNumber()}: {ex.Message}"); skipped++; }
            }
        }
        catch (Exception ex) { errors.Add(ex.Message); }

        return new XlsxImportResult(imported, skipped, errors);
    }

    public static XlsxImportResult ImportIncomes(string path)
    {
        int imported = 0, skipped = 0;
        var errors = new List<string>();

        try
        {
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);

            int cDate = FindCol(hdrs, "дата", "date");
            int cAcc  = FindCol(hdrs, "счёт", "счет", "рахунок", "account");
            int cCat  = FindCol(hdrs, "категория дохода", "категорія доходу", "категория", "категорія", "category");
            int cSub  = FindCol(hdrs, "подкатегория дохода", "підкатег. доходу", "подкатегория", "підкатегорія", "subcategory");
            int cUnit = FindCol(hdrs, "ед. изм.", "ед.изм.", "од. вим.", "unit");
            int cQty  = FindCol(hdrs, "кол-во", "кол.", "к-сть", "quantity");
            int cAmt  = FindCol(hdrs, "сумма", "сума", "гривны", "гривні", "гривень", "amount");
            int cNote = FindCol(hdrs, "примечание", "примітка", "note");

            if (cDate < 0 || cAmt < 0)
            {
                var missing = new List<string>();
                if (cDate < 0) missing.Add("Дата");
                if (cAmt < 0) missing.Add("Сумма/Гривны");
                var found = string.Join(", ", hdrs.Keys.Take(10));
                errors.Add($"Не найдены столбцы: {string.Join(", ", missing)}.\nВ файле найдены: {found}");
                return new XlsxImportResult(0, 0, errors);
            }

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                try
                {
                    var dateStr = Cell(row, cDate);
                    if (string.IsNullOrWhiteSpace(dateStr)) { skipped++; continue; }
                    if (!TryParseDate(dateStr, out var date)) { skipped++; continue; }
                    if (!TryParseAmount(Cell(row, cAmt), out var amount)) { skipped++; continue; }

                    var accName  = cAcc  >= 0 ? Cell(row, cAcc)  : "";
                    var catName  = cCat  >= 0 ? Cell(row, cCat)  : "";
                    var subName  = cSub  >= 0 ? Cell(row, cSub)  : "";
                    var unitName = cUnit >= 0 ? Cell(row, cUnit) : "";
                    double? qty  = cQty >= 0 && TryParseAmount(Cell(row, cQty), out var qv) ? qv : null;
                    var note     = cNote >= 0 ? Cell(row, cNote) : "";

                    int? accId  = string.IsNullOrEmpty(accName) ? null : GetOrCreateAccount(accName);
                    int? catId  = string.IsNullOrEmpty(catName) ? null : GetOrCreateCategory(catName, "income");
                    int? subId  = !string.IsNullOrEmpty(subName) && catId.HasValue
                                  ? GetOrCreateSubcategory(subName, catId.Value) : null;
                    int? unitId = string.IsNullOrEmpty(unitName) ? null : GetOrCreateUnit(unitName);

                    IncomeService.Add(new Income
                    {
                        Date = date, AccountId = accId, CategoryId = catId,
                        SubcategoryId = subId, Quantity = qty, UnitId = unitId,
                        Amount = amount, Note = note
                    });
                    imported++;
                }
                catch (Exception ex) { errors.Add($"Строка {row.RowNumber()}: {ex.Message}"); skipped++; }
            }
        }
        catch (Exception ex) { errors.Add(ex.Message); }

        return new XlsxImportResult(imported, skipped, errors);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void WriteHeader(IXLWorksheet ws, params string[] cols)
    {
        for (int c = 0; c < cols.Length; c++)
            ws.Cell(1, c + 1).Value = cols[c];
        var r = ws.Row(1);
        r.Style.Font.Bold = true;
        r.Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet ws)
    {
        var dict = new Dictionary<string, int>();
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 20;
        for (int c = 1; c <= lastCol; c++)
        {
            var v = ws.Cell(1, c).GetString().Trim().ToLower();
            if (!string.IsNullOrEmpty(v) && !dict.ContainsKey(v))
                dict[v] = c;
        }
        return dict;
    }

    private static int FindCol(Dictionary<string, int> hdrs, params string[] candidates)
    {
        // Точное совпадение
        foreach (var c in candidates)
            if (hdrs.TryGetValue(c, out var col)) return col;
        // Частичное: заголовок файла содержит искомое слово
        foreach (var c in candidates)
            foreach (var kv in hdrs)
                if (kv.Key.Contains(c))
                    return kv.Value;
        return -1;
    }

    private static string Cell(IXLRow row, int oneBasedCol)
    {
        if (oneBasedCol <= 0) return "";
        try { return row.Cell(oneBasedCol).GetString().Trim(); }
        catch { return ""; }
    }

    private static bool TryParseDate(string s, out DateTime d)
    {
        if (DateTime.TryParseExact(s, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out d)) return true;
        if (DateTime.TryParseExact(s, "d.M.yyyy",   null, System.Globalization.DateTimeStyles.None, out d)) return true;
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out d)) return true;
        return DateTime.TryParse(s, out d);
    }

    private static bool TryParseAmount(string s, out double v)
        => double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                           System.Globalization.CultureInfo.InvariantCulture, out v);

    private static int GetOrCreateAccount(string name)
    {
        using var conn = Db.Open();
        var id = conn.ExecuteScalar<int?>(
            "SELECT id FROM accounts WHERE user_id=@uid AND LOWER(name)=LOWER(@n) LIMIT 1",
            new { uid = Session.UserId, n = name });
        if (id.HasValue) return id.Value;
        return conn.ExecuteScalar<int>(
            "INSERT INTO accounts(user_id,name,sort_order,note,initial_balance,is_hidden,currency,icon) VALUES(@uid,@n,0,'',0,0,'₴','💰'); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = name });
    }

    private static int GetOrCreateCategory(string name, string type)
    {
        using var conn = Db.Open();
        var id = conn.ExecuteScalar<int?>(
            "SELECT id FROM categories WHERE user_id=@uid AND LOWER(name)=LOWER(@n) AND type=@t LIMIT 1",
            new { uid = Session.UserId, n = name, t = type });
        if (id.HasValue) return id.Value;
        return conn.ExecuteScalar<int>(
            "INSERT INTO categories(user_id,name,type) VALUES(@uid,@n,@t); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = name, t = type });
    }

    private static int GetOrCreateSubcategory(string name, int categoryId)
    {
        using var conn = Db.Open();
        var id = conn.ExecuteScalar<int?>(
            "SELECT id FROM subcategories WHERE user_id=@uid AND LOWER(name)=LOWER(@n) AND category_id=@c LIMIT 1",
            new { uid = Session.UserId, n = name, c = categoryId });
        if (id.HasValue) return id.Value;
        return conn.ExecuteScalar<int>(
            "INSERT INTO subcategories(user_id,category_id,name) VALUES(@uid,@c,@n); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = name, c = categoryId });
    }

    private static int GetOrCreateUnit(string name)
    {
        using var conn = Db.Open();
        var id = conn.ExecuteScalar<int?>(
            "SELECT id FROM units WHERE user_id=@uid AND LOWER(name)=LOWER(@n) LIMIT 1",
            new { uid = Session.UserId, n = name });
        if (id.HasValue) return id.Value;
        return conn.ExecuteScalar<int>(
            "INSERT INTO units(user_id,name) VALUES(@uid,@n); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = name });
    }

    // ── Import Preview: Parse ─────────────────────────────────────────────────

    public static (List<ExpImportRow> Rows, List<string> Errors) ParseExpenses(string path)
    {
        var rows = new List<ExpImportRow>();
        var errs = new List<string>();
        try
        {
            using var wb  = new XLWorkbook(path);
            var ws   = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);
            int cDate = FindCol(hdrs, "дата", "date");
            int cAcc  = FindCol(hdrs, "счёт", "счет", "рахунок", "account");
            int cCat  = FindCol(hdrs, "категория", "категорія", "category");
            int cSub  = FindCol(hdrs, "подкатегория", "підкатегорія", "subcategory");
            int cUnit = FindCol(hdrs, "ед. изм.", "ед.изм.", "unit");
            int cQty  = FindCol(hdrs, "кол-во", "кол.", "quantity");
            int cAmt  = FindCol(hdrs, "сумма", "сума", "amount");
            int cNote = FindCol(hdrs, "примечание", "примітка", "note");
            if (cDate < 0 || cAmt < 0) return (rows, errs);
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var ds = Cell(row, cDate);
                if (!TryParseDate(ds, out var date)) continue;
                if (!TryParseAmount(Cell(row, cAmt), out var amt)) continue;
                rows.Add(new ExpImportRow
                {
                    DateStr     = date.ToString("dd.MM.yyyy"),
                    Account     = cAcc  >= 0 ? Cell(row, cAcc)  : "",
                    Category    = cCat  >= 0 ? Cell(row, cCat)  : "",
                    Subcategory = cSub  >= 0 ? Cell(row, cSub)  : "",
                    Unit        = cUnit >= 0 ? Cell(row, cUnit) : "",
                    Qty         = cQty  >= 0 ? Cell(row, cQty)  : "",
                    Amount      = amt,
                    Note        = cNote >= 0 ? Cell(row, cNote) : ""
                });
            }
        }
        catch (Exception ex) { errs.Add(ex.Message); }
        return (rows, errs);
    }

    public static (List<IncImportRow> Rows, List<string> Errors) ParseIncomes(string path)
    {
        var rows = new List<IncImportRow>();
        var errs = new List<string>();
        try
        {
            using var wb  = new XLWorkbook(path);
            var ws   = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);
            int cDate = FindCol(hdrs, "дата", "date");
            int cAcc  = FindCol(hdrs, "счёт", "счет", "рахунок", "account");
            int cCat  = FindCol(hdrs, "категория дохода", "категорія доходу", "категория", "category");
            int cSub  = FindCol(hdrs, "подкатегория дохода", "підкатег. доходу", "подкатегория", "subcategory");
            int cQty  = FindCol(hdrs, "кол-во", "кол.", "quantity");
            int cUnit = FindCol(hdrs, "ед. изм.", "ед.изм.", "unit");
            int cAmt  = FindCol(hdrs, "сумма", "сума", "гривны", "гривні", "amount");
            int cNote = FindCol(hdrs, "примечание", "примітка", "note");
            if (cDate < 0 || cAmt < 0) return (rows, errs);
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var ds = Cell(row, cDate);
                if (!TryParseDate(ds, out var date)) continue;
                if (!TryParseAmount(Cell(row, cAmt), out var amt)) continue;
                rows.Add(new IncImportRow
                {
                    DateStr     = date.ToString("dd.MM.yyyy"),
                    Account     = cAcc  >= 0 ? Cell(row, cAcc)  : "",
                    Category    = cCat  >= 0 ? Cell(row, cCat)  : "",
                    Subcategory = cSub  >= 0 ? Cell(row, cSub)  : "",
                    Qty         = cQty  >= 0 ? Cell(row, cQty)  : "",
                    Unit        = cUnit >= 0 ? Cell(row, cUnit) : "",
                    Amount      = amt,
                    Note        = cNote >= 0 ? Cell(row, cNote) : ""
                });
            }
        }
        catch (Exception ex) { errs.Add(ex.Message); }
        return (rows, errs);
    }

    public static (List<AccImportRow> Rows, List<string> Errors) ParseAccounts(string path)
    {
        var rows = new List<AccImportRow>();
        var errs = new List<string>();
        try
        {
            using var wb  = new XLWorkbook(path);
            var ws   = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);
            int cName = FindCol(hdrs, "рахунок", "счёт", "счет", "account", "назва", "название", "name");
            int cBal  = FindCol(hdrs, "поч. баланс", "нач. баланс", "initial balance", "баланс", "balance");
            int cNote = FindCol(hdrs, "примітка", "примечание", "note");
            if (cName < 0) return (rows, errs);
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var name = Cell(row, cName);
                if (string.IsNullOrWhiteSpace(name)) continue;
                TryParseAmount(cBal >= 0 ? Cell(row, cBal) : "", out var bal);
                rows.Add(new AccImportRow
                {
                    Name        = name,
                    InitBalance = bal,
                    Note        = cNote >= 0 ? Cell(row, cNote) : ""
                });
            }
        }
        catch (Exception ex) { errs.Add(ex.Message); }
        return (rows, errs);
    }

    // Парсинг формата ДомБух7 «Счета подробно»: Дата, Счет, Расход|Гривны, Доход|Гривны, Прочие операции|Гривны, Оборот|Гривны
    public static (List<DetImportRow> Rows, List<string> Errors) ParseDetailed(string path)
    {
        var rows = new List<DetImportRow>();
        var errs = new List<string>();
        try
        {
            using var wb  = new XLWorkbook(path);
            var ws   = wb.Worksheets.First();
            var hdrs = ReadHeaders(ws);
            int cDate = FindCol(hdrs, "дата", "date");
            int cAcc  = FindCol(hdrs, "счет", "счёт", "рахунок", "account");
            int cExp  = FindCol(hdrs, "расход|гривны", "расход|гривні", "расход", "expense");
            int cInc  = FindCol(hdrs, "доход|гривны",  "доход|гривні",  "доход",  "income");
            int cOth  = FindCol(hdrs, "прочие операции|гривны", "прочие операции|гривні", "прочие операции", "other");
            int cTurn = FindCol(hdrs, "оборот|гривны", "оборот|гривні", "оборот", "turnover");
            if (cDate < 0 || cAcc < 0) return (rows, errs);
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var ds  = Cell(row, cDate);
                if (!TryParseDate(ds, out var date)) continue;
                var acc = Cell(row, cAcc);
                if (string.IsNullOrWhiteSpace(acc)) continue;
                TryParseAmount(cExp  >= 0 ? Cell(row, cExp)  : "", out var exp);
                TryParseAmount(cInc  >= 0 ? Cell(row, cInc)  : "", out var inc);
                TryParseAmount(cOth  >= 0 ? Cell(row, cOth)  : "", out var oth);
                TryParseAmount(cTurn >= 0 ? Cell(row, cTurn) : "", out var turn);
                rows.Add(new DetImportRow
                {
                    DateStr     = date.ToString("dd.MM.yyyy"),
                    AccountName = acc,
                    Expense     = exp,
                    Income      = inc,
                    Other       = oth,
                    Turnover    = turn
                });
            }
        }
        catch (Exception ex) { errs.Add(ex.Message); }
        return (rows, errs);
    }

    // ── Import Preview: Commit ────────────────────────────────────────────────

    public static XlsxImportResult CommitExpenses(IEnumerable<ExpImportRow> rows)
    {
        int imp = 0, skip = 0;
        var errs = new List<string>();
        foreach (var row in rows.Where(r => r.Include))
        {
            try
            {
                if (!TryParseDate(row.DateStr, out var date)) { skip++; continue; }
                TryParseAmount(row.Qty, out var qty);
                int? accId  = string.IsNullOrEmpty(row.Account)     ? null : GetOrCreateAccount(row.Account);
                int? catId  = string.IsNullOrEmpty(row.Category)    ? null : GetOrCreateCategory(row.Category, "expense");
                int? subId  = !string.IsNullOrEmpty(row.Subcategory) && catId.HasValue
                              ? GetOrCreateSubcategory(row.Subcategory, catId.Value) : null;
                int? unitId = string.IsNullOrEmpty(row.Unit)        ? null : GetOrCreateUnit(row.Unit);
                ExpenseService.Add(new Expense
                {
                    Date = date, AccountId = accId, CategoryId = catId,
                    SubcategoryId = subId, Quantity = qty > 0 ? qty : 1,
                    UnitId = unitId, Amount = row.Amount, Note = row.Note
                });
                imp++;
            }
            catch (Exception ex) { errs.Add(ex.Message); skip++; }
        }
        return new XlsxImportResult(imp, skip, errs);
    }

    public static XlsxImportResult CommitIncomes(IEnumerable<IncImportRow> rows)
    {
        int imp = 0, skip = 0;
        var errs = new List<string>();
        foreach (var row in rows.Where(r => r.Include))
        {
            try
            {
                if (!TryParseDate(row.DateStr, out var date)) { skip++; continue; }
                TryParseAmount(row.Qty, out var qty);
                int? accId  = string.IsNullOrEmpty(row.Account)     ? null : GetOrCreateAccount(row.Account);
                int? catId  = string.IsNullOrEmpty(row.Category)    ? null : GetOrCreateCategory(row.Category, "income");
                int? subId  = !string.IsNullOrEmpty(row.Subcategory) && catId.HasValue
                              ? GetOrCreateSubcategory(row.Subcategory, catId.Value) : null;
                int? unitId = string.IsNullOrEmpty(row.Unit)        ? null : GetOrCreateUnit(row.Unit);
                IncomeService.Add(new Income
                {
                    Date = date, AccountId = accId, CategoryId = catId,
                    SubcategoryId = subId, Quantity = qty > 0 ? qty : 1,
                    UnitId = unitId, Amount = row.Amount, Note = row.Note
                });
                imp++;
            }
            catch (Exception ex) { errs.Add(ex.Message); skip++; }
        }
        return new XlsxImportResult(imp, skip, errs);
    }

    public static XlsxImportResult CommitAccounts(IEnumerable<AccImportRow> rows)
    {
        int imp = 0, skip = 0;
        var errs = new List<string>();
        using var conn = Db.Open();
        foreach (var row in rows.Where(r => r.Include))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.Name)) { skip++; continue; }
                bool exists = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM accounts WHERE user_id=@uid AND LOWER(name)=LOWER(@n)",
                    new { uid = Session.UserId, n = row.Name }) > 0;
                if (exists)
                {
                    conn.Execute(
                        "UPDATE accounts SET initial_balance=@b, note=@nt WHERE user_id=@uid AND LOWER(name)=LOWER(@n)",
                        new { uid = Session.UserId, n = row.Name, b = row.InitBalance, nt = row.Note });
                }
                else
                {
                    int sortOrder = conn.ExecuteScalar<int>(
                        "SELECT COALESCE(MAX(sort_order),0)+1 FROM accounts WHERE user_id=@uid", new { uid = Session.UserId });
                    conn.Execute(
                        "INSERT INTO accounts(user_id,name,sort_order,initial_balance,note,currency,icon) VALUES(@uid,@n,@s,@b,@nt,'₴','💰')",
                        new { uid = Session.UserId, n = row.Name, s = sortOrder, b = row.InitBalance, nt = row.Note });
                }
                imp++;
            }
            catch (Exception ex) { errs.Add(ex.Message); skip++; }
        }
        return new XlsxImportResult(imp, skip, errs);
    }

    // Импорт из формата ДомБух7 «Счета подробно»
    public static XlsxImportResult CommitDetailed(IEnumerable<DetImportRow> rows)
    {
        int imp = 0, skip = 0;
        var errs = new List<string>();
        foreach (var row in rows.Where(r => r.Include))
        {
            try
            {
                if (!TryParseDate(row.DateStr, out var date)) { skip++; continue; }
                int? accId = string.IsNullOrEmpty(row.AccountName) ? null : GetOrCreateAccount(row.AccountName);
                string note = row.Note;
                if (row.Expense > 0)
                {
                    ExpenseService.Add(new Expense { Date = date, AccountId = accId, Amount = row.Expense, Quantity = 1, Note = note });
                    imp++;
                }
                if (row.Income > 0)
                {
                    IncomeService.Add(new Income { Date = date, AccountId = accId, Amount = row.Income, Quantity = 1, Note = note });
                    imp++;
                }
                if (row.Other > 0)
                {
                    IncomeService.Add(new Income { Date = date, AccountId = accId, Amount = row.Other, Quantity = 1,
                        Note = string.IsNullOrEmpty(note) ? "Прочие операции" : note });
                    imp++;
                }
                else if (row.Other < 0)
                {
                    ExpenseService.Add(new Expense { Date = date, AccountId = accId, Amount = Math.Abs(row.Other), Quantity = 1,
                        Note = string.IsNullOrEmpty(note) ? "Прочие операции" : note });
                    imp++;
                }
            }
            catch (Exception ex) { errs.Add(ex.Message); skip++; }
        }
        return new XlsxImportResult(imp, skip, errs);
    }
}
