using Dapper;

namespace HomeAccounting.Services;

public static class DebtService
{
    private const string Sel = @"
        SELECT d.id, d.kind, d.account_id AS AccountId, d.date, d.counterparty AS Counterparty,
               d.currency_id AS CurrencyId, d.amount, d.amount_back AS AmountBack, d.percent,
               d.is_closed AS IsClosed, d.date_close AS DateClose, d.note,
               COALESCE(a.name,'') AS AccountName, COALESCE(cur.symbol,'') AS CurrencySym
        FROM debts d
        LEFT JOIN accounts   a   ON a.id = d.account_id
        LEFT JOIN currencies cur ON cur.id = d.currency_id";

    public static List<Debt> GetAll(string? kind = null)
    {
        using var conn = Db.Open();
        return conn.Query<Debt>(Sel + @"
            WHERE d.user_id=@uid AND (@kind IS NULL OR d.kind=@kind)
            ORDER BY d.is_closed, d.date DESC",
            new { uid = Session.UserId, kind }).ToList();
    }

    public static Debt? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Debt>(Sel + " WHERE d.id=@id AND d.user_id=@uid",
            new { id, uid = Session.UserId });
    }

    public static int Add(Debt d)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO debts(user_id,kind,account_id,date,counterparty,currency_id,amount,amount_back,percent,is_closed,date_close,note)
            VALUES(@uid,@k,@a,@dt,@cp,@cur,@am,@ab,@pc,@cl,@dc,@n); SELECT last_insert_rowid();",
            new { uid = Session.UserId, k = d.Kind, a = d.AccountId, dt = d.Date.ToString("yyyy-MM-dd"),
                  cp = d.Counterparty, cur = d.CurrencyId, am = d.Amount, ab = d.AmountBack, pc = d.Percent,
                  cl = d.IsClosed ? 1 : 0, dc = d.DateClose ?? "", n = d.Note });
    }

    public static void Update(Debt d)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE debts SET kind=@k,account_id=@a,date=@dt,counterparty=@cp,currency_id=@cur,
                amount=@am,amount_back=@ab,percent=@pc,is_closed=@cl,date_close=@dc,note=@n
            WHERE id=@id AND user_id=@uid",
            new { k = d.Kind, a = d.AccountId, dt = d.Date.ToString("yyyy-MM-dd"), cp = d.Counterparty,
                  cur = d.CurrencyId, am = d.Amount, ab = d.AmountBack, pc = d.Percent,
                  cl = d.IsClosed ? 1 : 0, dc = d.DateClose ?? "", n = d.Note,
                  id = d.Id, uid = Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM debts WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
