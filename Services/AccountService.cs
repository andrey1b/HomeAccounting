using Dapper;

namespace HomeAccounting.Services;

public static class AccountService
{
    public static List<Account> GetAll(bool includeHidden = false)
    {
        using var conn = Db.Open();
        var sql = @"
            SELECT a.id, a.sort_order, a.name, a.note, a.initial_balance, a.is_hidden,
                   COALESCE(a.currency, '₴') AS Currency,
                   COALESCE(a.icon,     '💰') AS Icon,
                   COALESCE(e.total,  0.0) AS TotalExpense,
                   COALESCE(i.total,  0.0) AS TotalIncome,
                   COALESCE(ti.total, 0.0) AS TransfersIn,
                   COALESCE(to_.total,0.0) AS TransfersOut
            FROM accounts a
            LEFT JOIN (SELECT account_id,     SUM(amount*(1-discount/100)) AS total FROM expenses  GROUP BY account_id)     e   ON e.account_id    = a.id
            LEFT JOIN (SELECT account_id,     SUM(amount)                  AS total FROM incomes   GROUP BY account_id)     i   ON i.account_id    = a.id
            LEFT JOIN (SELECT to_account_id,  SUM(amount)                  AS total FROM transfers GROUP BY to_account_id)  ti  ON ti.to_account_id = a.id
            LEFT JOIN (SELECT from_account_id,SUM(amount)                  AS total FROM transfers GROUP BY from_account_id) to_ ON to_.from_account_id = a.id
            WHERE (@all = 1 OR a.is_hidden = 0)
            ORDER BY a.sort_order, a.id";
        return conn.Query<Account>(sql, new { all = includeHidden ? 1 : 0 }).ToList();
    }

    public static int GetNextSortOrder()
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>("SELECT COALESCE(MAX(sort_order),0) FROM accounts") + 1;
    }

    public static int Add(Account a)
    {
        using var conn = Db.Open();
        var maxOrder = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(sort_order),0) FROM accounts");
        int sortOrder = a.SortOrder > 0 ? a.SortOrder : maxOrder + 1;
        return conn.ExecuteScalar<int>(
            @"INSERT INTO accounts(sort_order,name,note,initial_balance,is_hidden,currency,icon)
              VALUES(@s,@n,@no,@ib,@h,@cur,@ico); SELECT last_insert_rowid();",
            new { s = sortOrder, n = a.Name, no = a.Note, ib = a.InitialBalance,
                  h = a.IsHidden ? 1 : 0, cur = a.Currency, ico = a.Icon });
    }

    public static void Update(Account a)
    {
        using var conn = Db.Open();
        conn.Execute(
            @"UPDATE accounts SET sort_order=@so, name=@n, note=@no, initial_balance=@ib,
              is_hidden=@h, currency=@cur, icon=@ico WHERE id=@id",
            new { so = a.SortOrder, n = a.Name, no = a.Note, ib = a.InitialBalance,
                  h = a.IsHidden ? 1 : 0, cur = a.Currency, ico = a.Icon, id = a.Id });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM accounts WHERE id=@id", new { id });
    }

    public static void SetHidden(int id, bool hidden)
    {
        using var conn = Db.Open();
        conn.Execute("UPDATE accounts SET is_hidden=@h WHERE id=@id", new { h = hidden ? 1 : 0, id });
    }
}
