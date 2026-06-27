using Dapper;

namespace HomeAccounting.Services;

/// <summary>Валюты и курсы — общие для всех пользователей.</summary>
public static class CurrencyService
{
    public static List<Currency> GetAll()
    {
        using var conn = Db.Open();
        return conn.Query<Currency>(
            "SELECT id, code, name, symbol, is_default AS IsDefault, sort_order AS SortOrder FROM currencies ORDER BY sort_order, id").ToList();
    }

    public static Currency? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<Currency>(
            "SELECT id, code, name, symbol, is_default AS IsDefault, sort_order AS SortOrder FROM currencies WHERE id=@id", new { id });
    }

    public static int DefaultId()
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>("SELECT id FROM currencies WHERE is_default=1 ORDER BY id LIMIT 1");
    }

    public static int Add(Currency c)
    {
        using var conn = Db.Open();
        var maxOrder = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(sort_order),0) FROM currencies");
        return conn.ExecuteScalar<int>(
            @"INSERT INTO currencies(code,name,symbol,is_default,sort_order)
              VALUES(@code,@name,@sym,0,@o); SELECT last_insert_rowid();",
            new { code = c.Code, name = c.Name, sym = c.Symbol, o = maxOrder + 1 });
    }

    public static void Update(Currency c)
    {
        using var conn = Db.Open();
        conn.Execute("UPDATE currencies SET code=@code, name=@name, symbol=@sym WHERE id=@id",
            new { code = c.Code, name = c.Name, sym = c.Symbol, id = c.Id });
    }

    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM currencies WHERE id=@id", new { id });
    }

    public static void SetDefault(int id)
    {
        using var conn = Db.Open();
        using var tr = conn.BeginTransaction();
        conn.Execute("UPDATE currencies SET is_default=0", transaction: tr);
        conn.Execute("UPDATE currencies SET is_default=1 WHERE id=@id", new { id }, tr);
        tr.Commit();
    }

    // ── Курсы ───────────────────────────────────────────────────────────────
    public static List<ExchangeRate> GetRates(int? currencyId = null)
    {
        using var conn = Db.Open();
        return conn.Query<ExchangeRate>(
            @"SELECT r.id, r.currency_id AS CurrencyId, c.name AS CurrencyName, r.date, r.rate
              FROM exchange_rates r JOIN currencies c ON c.id = r.currency_id
              WHERE (@cid IS NULL OR r.currency_id = @cid)
              ORDER BY r.date DESC, c.sort_order",
            new { cid = currencyId }).ToList();
    }

    public static int AddRate(int currencyId, DateTime date, double rate)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            @"INSERT INTO exchange_rates(currency_id,date,rate) VALUES(@c,@d,@r); SELECT last_insert_rowid();",
            new { c = currencyId, d = date.ToString("yyyy-MM-dd"), r = rate });
    }

    public static void DeleteRate(int id)
    {
        using var conn = Db.Open();
        conn.Execute("DELETE FROM exchange_rates WHERE id=@id", new { id });
    }
}
