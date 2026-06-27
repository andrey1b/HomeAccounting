using Dapper;

namespace HomeAccounting.Services;

public class TransferRow
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int? FromAccountId { get; set; }
    public string FromAccountName { get; set; } = "";
    public int? ToAccountId { get; set; }
    public string ToAccountName { get; set; } = "";
    public double Amount { get; set; }
    public string Note { get; set; } = "";

    public string DateStr   => Date.ToString("dd.MM.yyyy");
    public string AmountStr => $"{Amount:N2}";
}

public static class TransferService
{
    public static List<TransferRow> GetFiltered(DateTime from, DateTime to)
    {
        using var conn = Db.Open();
        return conn.Query<TransferRow>(@"
            SELECT t.id, t.date, t.from_account_id, COALESCE(fa.name,'') AS FromAccountName,
                   t.to_account_id, COALESCE(ta.name,'') AS ToAccountName,
                   t.amount, t.note
            FROM transfers t
            LEFT JOIN accounts fa ON fa.id = t.from_account_id
            LEFT JOIN accounts ta ON ta.id = t.to_account_id
            WHERE t.user_id = @uid AND t.date >= @df AND t.date <= @dt
            ORDER BY t.date DESC, t.id DESC",
            new { uid = Session.UserId, df = from.ToString("yyyy-MM-dd"), dt = to.ToString("yyyy-MM-dd") }).ToList();
    }

    public static Transfer? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Transfer>(
            "SELECT id, date, from_account_id, to_account_id, amount, note FROM transfers WHERE id=@id AND user_id=@uid",
            new { id, uid = Session.UserId });
    }

    public static int Add(Transfer t)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(@"
            INSERT INTO transfers(user_id, date, from_account_id, to_account_id, amount, note)
            VALUES(@uid, @d, @fa, @ta, @am, @n);
            SELECT last_insert_rowid();",
            new { uid = Session.UserId, d = t.Date.ToString("yyyy-MM-dd"), fa = t.FromAccountId, ta = t.ToAccountId, am = t.Amount, n = t.Note });
    }

    public static void Update(Transfer t)
    {
        using var conn = Db.Open();
        conn.Execute(@"
            UPDATE transfers SET date=@d, from_account_id=@fa, to_account_id=@ta, amount=@am, note=@n
            WHERE id=@id AND user_id=@uid",
            new { d = t.Date.ToString("yyyy-MM-dd"), fa = t.FromAccountId, ta = t.ToAccountId, am = t.Amount, n = t.Note, id = t.Id, uid = Session.UserId });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM transfers WHERE id=@id AND user_id=@uid", new { id, uid = Session.UserId });
    }
}
