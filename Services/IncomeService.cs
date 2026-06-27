using Dapper;

namespace HomeAccounting.Services;

public record IncomeFilter(
    DateTime DateFrom, DateTime DateTo,
    int? AccountId = null, int? CategoryId = null, int? SubcategoryId = null);

public class IncomeSummary { public double Today { get; set; } public double Week { get; set; } public double Month { get; set; } public double Total { get; set; } }

public static class IncomeService
{
    private const string JoinSql = @"
        SELECT i.id, i.date, i.account_id, COALESCE(a.name,'') AS AccountName,
               i.category_id, COALESCE(c.name,'') AS CategoryName,
               i.subcategory_id, COALESCE(s.name,'') AS SubcategoryName,
               i.quantity, i.unit_id, COALESCE(u.name,'') AS UnitName,
               i.amount, i.note
        FROM incomes i
        LEFT JOIN accounts      a ON a.id = i.account_id
        LEFT JOIN categories    c ON c.id = i.category_id
        LEFT JOIN subcategories s ON s.id = i.subcategory_id
        LEFT JOIN units         u ON u.id = i.unit_id";

    public static List<IncomeRow> GetFiltered(IncomeFilter f)
    {
        using var conn = Db.Open();
        var sql = JoinSql + @"
            WHERE i.user_id = @uid
              AND i.date >= @df AND i.date <= @dt
              AND (@aid IS NULL OR i.account_id = @aid)
              AND (@cid IS NULL OR i.category_id = @cid)
              AND (@sid IS NULL OR i.subcategory_id = @sid)
            ORDER BY i.date DESC, i.id DESC";
        return conn.Query<IncomeRow>(sql, new {
            uid = Session.UserId,
            df = f.DateFrom.ToString("yyyy-MM-dd"),
            dt = f.DateTo.ToString("yyyy-MM-dd"),
            aid = f.AccountId, cid = f.CategoryId, sid = f.SubcategoryId
        }).ToList();
    }

    public static IncomeSummary GetSummary(int? accountId = null)
    {
        using var conn = Db.Open();
        var today = DateTime.Today;
        var dow = (int)today.DayOfWeek;
        var weekStart = today.AddDays(-(dow == 0 ? 6 : dow - 1));
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN date = @td THEN amount ELSE 0 END), 0) AS Today,
                COALESCE(SUM(CASE WHEN date >= @ws THEN amount ELSE 0 END), 0) AS Week,
                COALESCE(SUM(CASE WHEN date >= @ms THEN amount ELSE 0 END), 0) AS Month,
                COALESCE(SUM(amount), 0) AS Total
            FROM incomes
            WHERE user_id = @uid AND (@aid IS NULL OR account_id = @aid)";
        return conn.QueryFirst<IncomeSummary>(sql, new {
            uid = Session.UserId,
            td = today.ToString("yyyy-MM-dd"),
            ws = weekStart.ToString("yyyy-MM-dd"),
            ms = monthStart.ToString("yyyy-MM-dd"),
            aid = accountId
        });
    }

    public static Income? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Income>(
            "SELECT id,date,account_id,category_id,subcategory_id,quantity,unit_id,amount,note FROM incomes WHERE id=@id AND user_id=@uid",
            new { id, uid = Session.UserId });
    }

    public static int Add(Income i)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO incomes(user_id,date,account_id,category_id,subcategory_id,quantity,unit_id,amount,note)
            VALUES(@uid,@d,@a,@c,@s,@q,@u,@am,@n);
            SELECT last_insert_rowid();",
            new { uid=Session.UserId, d=i.Date.ToString("yyyy-MM-dd"), a=i.AccountId, c=i.CategoryId,
                  s=i.SubcategoryId, q=i.Quantity, u=i.UnitId, am=i.Amount, n=i.Note });
    }

    public static void Update(Income i)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE incomes SET date=@d,account_id=@a,category_id=@c,subcategory_id=@s,
                quantity=@q,unit_id=@u,amount=@am,note=@n WHERE id=@id AND user_id=@uid",
            new { d=i.Date.ToString("yyyy-MM-dd"), a=i.AccountId, c=i.CategoryId,
                  s=i.SubcategoryId, q=i.Quantity, u=i.UnitId, am=i.Amount, n=i.Note, id=i.Id, uid=Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM incomes WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
