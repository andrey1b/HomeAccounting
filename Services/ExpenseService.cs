using Dapper;

namespace HomeAccounting.Services;

public record ExpenseFilter(
    DateTime DateFrom, DateTime DateTo,
    int? AccountId = null, int? CategoryId = null, int? SubcategoryId = null);

public class ExpenseSummary { public double Today { get; set; } public double Week { get; set; } public double Month { get; set; } public double Total { get; set; } }

public static class ExpenseService
{
    private const string JoinSql = @"
        SELECT e.id, e.date, e.account_id, COALESCE(a.name,'') AS AccountName,
               e.category_id, COALESCE(c.name,'') AS CategoryName,
               e.subcategory_id, COALESCE(s.name,'') AS SubcategoryName,
               e.quantity, e.unit_id, COALESCE(u.name,'') AS UnitName,
               e.amount, e.discount, e.note, e.receipt_item_name AS ReceiptItemName
        FROM expenses e
        LEFT JOIN accounts     a ON a.id = e.account_id
        LEFT JOIN categories   c ON c.id = e.category_id
        LEFT JOIN subcategories s ON s.id = e.subcategory_id
        LEFT JOIN units         u ON u.id = e.unit_id";

    public static List<ExpenseRow> GetFiltered(ExpenseFilter f)
    {
        using var conn = Db.Open();
        var sql = JoinSql + @"
            WHERE e.user_id = @uid
              AND e.date >= @df AND e.date <= @dt
              AND (@aid IS NULL OR e.account_id = @aid)
              AND (@cid IS NULL OR e.category_id = @cid)
              AND (@sid IS NULL OR e.subcategory_id = @sid)
            ORDER BY e.date DESC, e.id DESC";
        return conn.Query<ExpenseRow>(sql, new {
            uid = Session.UserId,
            df = f.DateFrom.ToString("yyyy-MM-dd"),
            dt = f.DateTo.ToString("yyyy-MM-dd"),
            aid = f.AccountId, cid = f.CategoryId, sid = f.SubcategoryId
        }).ToList();
    }

    public static ExpenseSummary GetSummary(int? accountId = null)
    {
        using var conn = Db.Open();
        var today = DateTime.Today;
        var dow = (int)today.DayOfWeek;
        var weekStart = today.AddDays(-(dow == 0 ? 6 : dow - 1));
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN date = @td THEN amount*(1-discount/100) ELSE 0 END), 0) AS Today,
                COALESCE(SUM(CASE WHEN date >= @ws THEN amount*(1-discount/100) ELSE 0 END), 0) AS Week,
                COALESCE(SUM(CASE WHEN date >= @ms THEN amount*(1-discount/100) ELSE 0 END), 0) AS Month,
                COALESCE(SUM(amount*(1-discount/100)), 0) AS Total
            FROM expenses
            WHERE user_id = @uid AND (@aid IS NULL OR account_id = @aid)";
        return conn.QueryFirst<ExpenseSummary>(sql, new {
            uid = Session.UserId,
            td = today.ToString("yyyy-MM-dd"),
            ws = weekStart.ToString("yyyy-MM-dd"),
            ms = monthStart.ToString("yyyy-MM-dd"),
            aid = accountId
        });
    }

    public static Expense? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Expense>(
            "SELECT id,date,account_id,category_id,subcategory_id,quantity,unit_id,amount,discount,note,receipt_item_name AS ReceiptItemName FROM expenses WHERE id=@id AND user_id=@uid",
            new { id, uid = Session.UserId });
    }

    public static int Add(Expense e)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO expenses(user_id,date,account_id,category_id,subcategory_id,quantity,unit_id,amount,discount,note,receipt_item_name)
            VALUES(@uid,@d,@a,@c,@s,@q,@u,@am,@di,@n,@rn);
            SELECT last_insert_rowid();",
            new { uid=Session.UserId, d=e.Date.ToString("yyyy-MM-dd"), a=e.AccountId, c=e.CategoryId,
                  s=e.SubcategoryId, q=e.Quantity, u=e.UnitId,
                  am=e.Amount, di=e.Discount, n=e.Note, rn=e.ReceiptItemName });
    }

    public static void Update(Expense e)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE expenses SET date=@d,account_id=@a,category_id=@c,subcategory_id=@s,
                quantity=@q,unit_id=@u,amount=@am,discount=@di,note=@n WHERE id=@id AND user_id=@uid",
            new { d=e.Date.ToString("yyyy-MM-dd"), a=e.AccountId, c=e.CategoryId,
                  s=e.SubcategoryId, q=e.Quantity, u=e.UnitId,
                  am=e.Amount, di=e.Discount, n=e.Note, id=e.Id, uid=Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM expenses WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
