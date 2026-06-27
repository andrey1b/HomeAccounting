using Dapper;

namespace HomeAccounting.Services;

public static class DepositService
{
    private const string Sel = @"
        SELECT d.id, d.name, d.account_id AS AccountId, d.currency_id AS CurrencyId,
               d.amount, d.rate, d.open_date AS OpenDate, d.close_date AS CloseDate, d.note,
               COALESCE(a.name,'') AS AccountName, COALESCE(cur.symbol,'') AS CurrencySym
        FROM deposits d
        LEFT JOIN accounts   a   ON a.id = d.account_id
        LEFT JOIN currencies cur ON cur.id = d.currency_id";

    public static List<Deposit> GetAll()
    {
        using var conn = Db.Open();
        return conn.Query<Deposit>(Sel + " WHERE d.user_id=@uid ORDER BY d.open_date DESC, d.id DESC",
            new { uid = Session.UserId }).ToList();
    }

    public static Deposit? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Deposit>(Sel + " WHERE d.id=@id AND d.user_id=@uid",
            new { id, uid = Session.UserId });
    }

    public static int Add(Deposit d)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO deposits(user_id,name,account_id,currency_id,amount,rate,open_date,close_date,note)
            VALUES(@uid,@n,@a,@cur,@am,@r,@o,@c,@note); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = d.Name, a = d.AccountId, cur = d.CurrencyId, am = d.Amount,
                  r = d.Rate, o = d.OpenDate ?? "", c = d.CloseDate ?? "", note = d.Note });
    }

    public static void Update(Deposit d)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE deposits SET name=@n,account_id=@a,currency_id=@cur,amount=@am,rate=@r,
                open_date=@o,close_date=@c,note=@note WHERE id=@id AND user_id=@uid",
            new { n = d.Name, a = d.AccountId, cur = d.CurrencyId, am = d.Amount, r = d.Rate,
                  o = d.OpenDate ?? "", c = d.CloseDate ?? "", note = d.Note, id = d.Id, uid = Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM deposits WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
