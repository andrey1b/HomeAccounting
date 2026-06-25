using Dapper;

namespace HomeAccounting.Services;

public record ImportResult(List<int> Ids, string Store, int Count, DateTime? ReceiptDate);

public static class ReceiptImportService
{
    public static ImportResult Import(string xmlPath, int? defaultAccountId = null)
    {
        var receipt = ReceiptXmlParser.Parse(xmlPath);
        var ids     = new List<int>();

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

    private static int? GetOrCreateUnit(string name)
    {
        using var conn = Db.Open();
        conn.Execute("INSERT OR IGNORE INTO units(name) VALUES(@n)", new { n = name });
        return conn.ExecuteScalar<int?>("SELECT id FROM units WHERE name = @n", new { n = name });
    }
}
