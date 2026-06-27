using Dapper;

namespace HomeAccounting.Services;

public static class BudgetService
{
    public static List<Budget> GetAll(int year, int month, string type)
    {
        using var conn = Db.Open();
        var budgets = conn.Query<Budget>(@"
            SELECT b.id, b.type, b.year, b.month,
                   b.category_id AS CategoryId, b.subcategory_id AS SubcategoryId,
                   b.currency_id AS CurrencyId, b.plan, b.note,
                   COALESCE(c.name,'') AS CategoryName, COALESCE(s.name,'') AS SubcategoryName
            FROM budgets b
            LEFT JOIN categories    c ON c.id = b.category_id
            LEFT JOIN subcategories s ON s.id = b.subcategory_id
            WHERE b.user_id=@uid AND b.year=@y AND b.month=@m AND b.type=@t
            ORDER BY c.name, s.name",
            new { uid = Session.UserId, y = year, m = month, t = type }).ToList();

        // Фактические суммы за месяц
        var mStart = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
        var mEnd   = new DateTime(year, month, 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
        var table  = type == "income" ? "incomes" : "expenses";
        var amtExpr = type == "income" ? "amount" : "amount*(1-discount/100)";

        var facts = conn.Query($@"
            SELECT category_id AS CategoryId, subcategory_id AS SubcategoryId,
                   SUM({amtExpr}) AS Total
            FROM {table}
            WHERE user_id=@uid AND date>=@s AND date<=@e
            GROUP BY category_id, subcategory_id",
            new { uid = Session.UserId, s = mStart, e = mEnd }).ToList();

        foreach (var b in budgets)
        {
            double fact = 0;
            foreach (var f in facts)
            {
                int? fc = f.CategoryId is null ? null : (int?)(long)f.CategoryId;
                int? fs = f.SubcategoryId is null ? null : (int?)(long)f.SubcategoryId;
                if (fc != b.CategoryId) continue;
                if (b.SubcategoryId.HasValue && fs != b.SubcategoryId) continue;
                fact += Convert.ToDouble(f.Total ?? 0);
            }
            b.Fact = fact;
        }
        return budgets;
    }

    public static List<int> GetYears()
    {
        using var conn = Db.Open();
        var years = conn.Query<int>(
            "SELECT DISTINCT year FROM budgets WHERE user_id=@uid ORDER BY year DESC",
            new { uid = Session.UserId }).ToList();
        if (!years.Contains(DateTime.Today.Year)) years.Insert(0, DateTime.Today.Year);
        return years;
    }

    public static Budget? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Budget>(@"
            SELECT id, type, year, month, category_id AS CategoryId, subcategory_id AS SubcategoryId,
                   currency_id AS CurrencyId, plan, note
            FROM budgets WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }

    public static int Add(Budget b)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO budgets(user_id,type,year,month,category_id,subcategory_id,currency_id,plan,note)
            VALUES(@uid,@t,@y,@m,@c,@s,@cur,@p,@n); SELECT last_insert_rowid();",
            new { uid = Session.UserId, t = b.Type, y = b.Year, m = b.Month, c = b.CategoryId,
                  s = b.SubcategoryId, cur = b.CurrencyId, p = b.Plan, n = b.Note });
    }

    public static void Update(Budget b)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE budgets SET type=@t,year=@y,month=@m,category_id=@c,subcategory_id=@s,
                currency_id=@cur,plan=@p,note=@n WHERE id=@id AND user_id=@uid",
            new { t = b.Type, y = b.Year, m = b.Month, c = b.CategoryId, s = b.SubcategoryId,
                  cur = b.CurrencyId, p = b.Plan, n = b.Note, id = b.Id, uid = Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM budgets WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
