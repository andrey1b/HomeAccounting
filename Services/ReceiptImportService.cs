using Dapper;

namespace HomeAccounting.Services;

public record ImportResult(List<int> Ids, string Store, int Count, DateTime? ReceiptDate,
                           bool Duplicate = false, string ReceiptNo = "");

public static class ReceiptImportService
{
    /// <summary>
    /// Импорт чека. Если чек с таким номером и датой уже вносился текущим пользователем,
    /// вызывается onDuplicate(receipt): true — внести повторно, false — пропустить.
    /// Если onDuplicate == null, проверка не выполняется (обратная совместимость).
    /// </summary>
    public static ImportResult Import(string xmlPath, int? defaultAccountId = null,
                                      Func<ParsedReceipt, bool>? onDuplicate = null)
    {
        var receipt = ReceiptXmlParser.Parse(xmlPath);

        // Предохранитель от двойного внесения чека
        if (receipt.Items.Count > 0 && onDuplicate != null && IsDuplicate(receipt))
        {
            if (!onDuplicate(receipt))
                return new ImportResult(new List<int>(), receipt.Store, 0, null,
                                        Duplicate: true, ReceiptNo: receipt.ReceiptNo);
        }

        var ids = new List<int>();
        foreach (var item in receipt.Items)
        {
            // Category: exact DB mapping first, then fuzzy suggestion
            var mapping = ItemMappingService.Find(item.Name);
            if (mapping == null && !string.IsNullOrEmpty(item.Barcode))
                mapping = ItemMappingService.FindBarcode(item.Barcode);

            int? catId = mapping?.CategoryId;
            int? subId = mapping?.SubcategoryId;
            if (catId == null)
            {
                var (sugCat, sugSub) = ReceiptCategorySuggestor.Suggest(item.Name, item.Barcode);
                catId = sugCat;
                subId = sugSub;
            }

            // Unit: from XML tag, or from saved mapping
            int? unitId = null;
            if (!string.IsNullOrEmpty(item.UnitName))
                unitId = GetOrCreateUnit(item.UnitName);
            else if (mapping?.UnitId != null)
                unitId = mapping.UnitId;

            var expense = new Expense
            {
                Date            = receipt.Date,
                AccountId       = defaultAccountId,
                CategoryId      = catId,
                SubcategoryId   = subId,
                Quantity        = item.Quantity,
                UnitId          = unitId,
                Amount          = Math.Round(item.Cost, 2),
                Note            = ReceiptCategorySuggestor.BuildNote(item, receipt),
                ReceiptItemName = item.Name
            };

            ids.Add(ExpenseService.Add(expense));
        }

        return new ImportResult(ids, receipt.Store, ids.Count,
            ids.Count > 0 ? receipt.Date : null);
    }

    /// <summary>Чек уже вносился? Сверяем по номеру чека + дате (+ магазину) у текущего пользователя.</summary>
    public static bool IsDuplicate(ParsedReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNo)) return false;  // без номера не проверяем
        using var conn = Db.Open();
        var p = new DynamicParameters();
        p.Add("uid", Session.UserId);
        p.Add("d", receipt.Date.ToString("yyyy-MM-dd"));
        p.Add("np", $"%(чек №{receipt.ReceiptNo})%");
        var sql = "SELECT COUNT(*) FROM expenses WHERE user_id=@uid AND date=@d AND note LIKE @np";
        if (!string.IsNullOrWhiteSpace(receipt.Store))
        {
            sql += " AND note LIKE @sp";
            p.Add("sp", $"%{receipt.Store}%");
        }
        return conn.ExecuteScalar<long>(sql, p) > 0;
    }

    private static int? GetOrCreateUnit(string name) => CategoryService.AddUnit(name);
}
