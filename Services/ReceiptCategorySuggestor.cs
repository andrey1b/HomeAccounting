using System.Text.RegularExpressions;
using Dapper;

namespace HomeAccounting.Services;

public static class ReceiptCategorySuggestor
{
    // ── Note builder ─────────────────────────────────────────────────────────
    // Format: "ProductName  (чек №N); Магазин · картка · HH:MM:SS"
    // Mirrors HomeB's build_note() logic.

    public static string BuildNote(ReceiptItem item, ParsedReceipt receipt)
    {
        var head = item.Name;
        if (!string.IsNullOrEmpty(receipt.ReceiptNo))
            head += $"  (чек №{receipt.ReceiptNo})";

        var parts = new List<string>();

        // 1) Merchant name or TN fallback
        var store = receipt.Store.Trim();
        if (!string.IsNullOrEmpty(store))
            parts.Add(store);
        else if (!string.IsNullOrEmpty(receipt.TN))
            parts.Add("ТН " + receipt.TN);

        // 2) Payment kind (classify like HomeB's _classify_payment)
        if (!string.IsNullOrEmpty(receipt.PaymentKind))
        {
            // Latin 'I' → Cyrillic 'І' before comparison (occurs in ДПС XML)
            var upper = receipt.PaymentKind.ToUpperInvariant().Replace("I", "І");
            if (upper.Contains("ГОТІВ") || upper.Contains("НАЛИЧ") || upper.Contains("CASH"))
                parts.Add("готівка");
            else if (upper.Contains("КАРТ") || upper.Contains("CARD") || upper.Contains("БЕЗГОТ"))
            {
                var seg = "картка";
                if (!string.IsNullOrEmpty(receipt.PaymentMask)) seg += " " + receipt.PaymentMask;
                parts.Add(seg);
            }
            else
                parts.Add(receipt.PaymentKind.ToLower().Trim());
        }

        // 3) Time
        if (!string.IsNullOrEmpty(receipt.TimeStr))
            parts.Add(receipt.TimeStr);

        if (parts.Count == 0) return head;
        return head + "; " + string.Join(" · ", parts);
    }

    // ── Category suggestion ───────────────────────────────────────────────────
    // 4-step algorithm mirroring HomeB's suggest():
    //   1. Barcode exact match in item_mappings
    //   2. Normalised name exact match in item_mappings
    //   3. Fuzzy match against saved item names (threshold 0.84)
    //   4. Fuzzy match against subcategory names (threshold 0.85)

    public static (int? CatId, int? SubId) Suggest(string name, string? barcode = null)
    {
        // 1. Barcode exact match
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var bcMap = ItemMappingService.FindBarcode(barcode);
            if (bcMap != null) return (bcMap.CategoryId, bcMap.SubcategoryId);
        }

        // 2. Exact name match
        var nameMap = ItemMappingService.Find(name);
        if (nameMap != null) return (nameMap.CategoryId, nameMap.SubcategoryId);

        var ptoks = Tokens(name);
        if (ptoks.Count == 0) return (null, null);

        // 3. Fuzzy match against all saved item names
        var allMappings = ItemMappingService.GetAll();
        double memScore = 0;
        ItemMapping? memRec = null;
        foreach (var m in allMappings)
        {
            if (string.IsNullOrEmpty(m.NameKey) || m.NameKey.StartsWith("bc:")) continue;
            var ctoks = Tokens(m.NameKey);
            if (ctoks.Count == 0) continue;
            double s = BestMatch(ptoks, ctoks);
            if (s > memScore) { memScore = s; memRec = m; }
        }
        if (memRec != null && memScore >= 0.84)
            return (memRec.CategoryId, memRec.SubcategoryId);

        // 4. Fuzzy match against subcategory dictionary
        var allSubs = GetAllSubcats();
        double gScore = 0;
        int? gCat = null, gSub = null;
        foreach (var sub in allSubs)
        {
            var stoks = Tokens(sub.SubName);
            if (stoks.Count == 0) continue;
            double s = BestMatch(ptoks, stoks);
            if (s > gScore) { gScore = s; gCat = sub.CatId; gSub = sub.SubId; }
        }
        if (gScore >= 0.85)
            return (gCat, gSub);

        return (null, null);
    }

    // Remember user correction — call when user manually changes category of imported item.
    public static void Remember(string name, string? barcode, int? catId, int? subId, int? unitId)
    {
        ItemMappingService.Save(name, catId, subId, unitId);
        if (!string.IsNullOrWhiteSpace(barcode))
            ItemMappingService.SaveBarcode(barcode, catId, subId);
    }

    // ── Text normalisation (ported from HomeB) ────────────────────────────────

    private static readonly Regex _nonAlpha = new(@"[^а-яёa-z]+", RegexOptions.Compiled);

    private static readonly HashSet<string> _unitTokens = new(StringComparer.Ordinal)
    {
        "кг", "гр", "мл", "шт", "уп", "мг", "ст", "пак", "пач", "бут", "тм"
    };

    // norm(): UA→RU transliteration + lowercase (stable memory key)
    static string Norm(string s)
    {
        s = s.Replace("і", "и").Replace("ї", "и").Replace("є", "е").Replace("ґ", "г")
             .Replace("І", "и").Replace("Ї", "и").Replace("Є", "е").Replace("Ґ", "г")
             .Replace("'", "").Replace("`", "").Replace("\"", "");
        return s.ToLowerInvariant().Trim();
    }

    // _fold(): norm + ы→и + Latin lookalikes → Cyrillic
    static string Fold(string s)
    {
        s = Norm(s).Replace("ы", "и");
        // Replace Latin chars that look like Cyrillic
        s = s.Replace('a', 'а').Replace('c', 'с').Replace('e', 'е').Replace('i', 'и')
             .Replace('o', 'о').Replace('p', 'р').Replace('x', 'х').Replace('y', 'у')
             .Replace('k', 'к').Replace('m', 'м').Replace('t', 'т');
        return s;
    }

    // _tokens(): meaningful words, length ≥ 3, no unit tokens
    static List<string> Tokens(string name)
    {
        var folded  = Fold(name);
        var cleaned = _nonAlpha.Replace(folded, " ");
        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                      .Where(t => t.Length >= 3 && !_unitTokens.Contains(t))
                      .ToList();
    }

    // _root(): first 6 chars stripped of trailing vowel endings
    static string Root(string w)
    {
        var s = w.Length > 6 ? w[..6] : w;
        return s.TrimEnd('а', 'я', 'у', 'ю', 'о', 'е', 'и', 'ы', 'й', 'ь', 'ъ', 'ё');
    }

    // _tok_sim(): token similarity — exact → root-match → LCS ratio
    static double TokSim(string a, string b)
    {
        if (a == b) return 1.0;
        var ra = Root(a); var rb = Root(b);
        if (ra.Length >= 4 && rb.Length >= 4 &&
            (a.StartsWith(rb) || b.StartsWith(ra) || ra == rb))
            return 0.9;
        // Approximate SequenceMatcher.ratio() = 2 * LCS_substring / (len(a) + len(b))
        int lcs = LongestCommonSubstring(a, b);
        return (double)(2 * lcs) / (a.Length + b.Length);
    }

    static int LongestCommonSubstring(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        int best = 0;
        var m = new int[a.Length + 1, b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    m[i, j] = m[i - 1, j - 1] + 1;
                    if (m[i, j] > best) best = m[i, j];
                }
            }
        return best;
    }

    // _best_match(): max similarity across all product×candidate token pairs
    static double BestMatch(List<string> ptoks, List<string> ctoks)
    {
        double best = 0;
        foreach (var p in ptoks)
            foreach (var c in ctoks)
            {
                double s = TokSim(p, c);
                if (s > best) best = s;
            }
        return best;
    }

    // ── DB helpers ─────────────────────────────────────────────────────────────

    private class SubcatEntry
    {
        public int    SubId   { get; set; }
        public string SubName { get; set; } = "";
        public int    CatId   { get; set; }
    }

    private static List<SubcatEntry> GetAllSubcats()
    {
        using var conn = Db.Open();
        return conn.Query<SubcatEntry>(
            "SELECT s.id AS SubId, s.name AS SubName, s.category_id AS CatId " +
            "FROM subcategories s " +
            "JOIN categories c ON c.id = s.category_id " +
            "WHERE c.type = 'expense' AND c.user_id = @uid",
            new { uid = Session.UserId }
        ).ToList();
    }
}
