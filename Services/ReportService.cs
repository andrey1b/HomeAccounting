using Dapper;

namespace HomeAccounting.Services;

public class ReportRow
{
    public string CategoryName { get; set; } = "";
    public string SubcategoryName { get; set; } = "";
    public double Total { get; set; }
    public int Count { get; set; }
    public string TotalStr => $"{Total:N2}";
}

public class DateGroupRow
{
    public string Period { get; set; } = "";
    public double Total { get; set; }
    public int Count { get; set; }
    public string TotalStr => $"{Total:N2}";
}

public static class ReportService
{
    public static List<ReportRow> GetExpensesByCategory(
        DateTime from, DateTime to,
        int? accountId = null, int? categoryId = null, int? subcategoryId = null)
    {
        using var conn = Db.Open();
        return conn.Query<ReportRow>(@"
            SELECT COALESCE(c.name,'(без категорії)') AS CategoryName,
                   COALESCE(s.name,'')                AS SubcategoryName,
                   SUM(e.amount*(1-e.discount/100))   AS Total,
                   COUNT(*)                           AS Count
            FROM expenses e
            LEFT JOIN categories    c ON c.id = e.category_id
            LEFT JOIN subcategories s ON s.id = e.subcategory_id
            WHERE e.user_id = @uid AND e.date >= @df AND e.date <= @dt
              AND (@aid IS NULL OR e.account_id    = @aid)
              AND (@cid IS NULL OR e.category_id   = @cid)
              AND (@sid IS NULL OR e.subcategory_id = @sid)
            GROUP BY e.category_id, e.subcategory_id
            ORDER BY c.name, s.name",
            new { uid=Session.UserId, df=from.ToString("yyyy-MM-dd"), dt=to.ToString("yyyy-MM-dd"),
                  aid=accountId, cid=categoryId, sid=subcategoryId }
        ).ToList();
    }

    public static List<ReportRow> GetIncomesByCategory(
        DateTime from, DateTime to,
        int? accountId = null, int? categoryId = null, int? subcategoryId = null)
    {
        using var conn = Db.Open();
        return conn.Query<ReportRow>(@"
            SELECT COALESCE(c.name,'(без категорії)') AS CategoryName,
                   COALESCE(s.name,'')                AS SubcategoryName,
                   SUM(i.amount)                      AS Total,
                   COUNT(*)                           AS Count
            FROM incomes i
            LEFT JOIN categories    c ON c.id = i.category_id
            LEFT JOIN subcategories s ON s.id = i.subcategory_id
            WHERE i.user_id = @uid AND i.date >= @df AND i.date <= @dt
              AND (@aid IS NULL OR i.account_id    = @aid)
              AND (@cid IS NULL OR i.category_id   = @cid)
              AND (@sid IS NULL OR i.subcategory_id = @sid)
            GROUP BY i.category_id, i.subcategory_id
            ORDER BY c.name, s.name",
            new { uid=Session.UserId, df=from.ToString("yyyy-MM-dd"), dt=to.ToString("yyyy-MM-dd"),
                  aid=accountId, cid=categoryId, sid=subcategoryId }
        ).ToList();
    }

    // groupBy: "day" | "week" | "month"
    public static List<DateGroupRow> GetExpensesByDate(DateTime from, DateTime to, string groupBy, int? accountId = null)
    {
        var groupExpr = groupBy switch {
            "week"  => "strftime('%Y-W%W', e.date)",
            "month" => "strftime('%Y-%m', e.date)",
            _       => "e.date"
        };
        using var conn = Db.Open();
        return conn.Query<DateGroupRow>($@"
            SELECT {groupExpr} AS Period,
                   SUM(e.amount*(1-e.discount/100)) AS Total,
                   COUNT(*) AS Count
            FROM expenses e
            WHERE e.user_id = @uid AND e.date >= @df AND e.date <= @dt
              AND (@aid IS NULL OR e.account_id = @aid)
            GROUP BY {groupExpr}
            ORDER BY Period DESC",
            new { uid=Session.UserId, df=from.ToString("yyyy-MM-dd"), dt=to.ToString("yyyy-MM-dd"), aid=accountId }
        ).ToList();
    }

    public static List<DateGroupRow> GetIncomesByDate(DateTime from, DateTime to, string groupBy, int? accountId = null)
    {
        var groupExpr = groupBy switch {
            "week"  => "strftime('%Y-W%W', i.date)",
            "month" => "strftime('%Y-%m', i.date)",
            _       => "i.date"
        };
        using var conn = Db.Open();
        return conn.Query<DateGroupRow>($@"
            SELECT {groupExpr} AS Period,
                   SUM(i.amount) AS Total,
                   COUNT(*) AS Count
            FROM incomes i
            WHERE i.user_id = @uid AND i.date >= @df AND i.date <= @dt
              AND (@aid IS NULL OR i.account_id = @aid)
            GROUP BY {groupExpr}
            ORDER BY Period DESC",
            new { uid=Session.UserId, df=from.ToString("yyyy-MM-dd"), dt=to.ToString("yyyy-MM-dd"), aid=accountId }
        ).ToList();
    }
}
