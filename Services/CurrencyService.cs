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

    public static string DefaultSymbol()
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<string>("SELECT symbol FROM currencies WHERE is_default=1 ORDER BY id LIMIT 1") ?? "₴";
    }

    /// <summary>Курс каждой валюты к базовой (последний по дате). Базовая = 1; без курса = 1.</summary>
    public static Dictionary<int, double> RatesToBase()
    {
        using var conn = Db.Open();
        var map = new Dictionary<int, double>();
        int baseId = DefaultId();
        foreach (var c in conn.Query("SELECT id, is_default AS IsDefault FROM currencies"))
        {
            int id = (int)(long)c.id;
            if ((long)c.IsDefault == 1) { map[id] = 1.0; continue; }
            var rate = conn.ExecuteScalar<double?>(
                "SELECT rate FROM exchange_rates WHERE currency_id=@id ORDER BY date DESC, id DESC LIMIT 1",
                new { id });
            map[id] = rate is > 0 ? rate.Value : 1.0;
        }
        return map;
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

    /// <summary>Скачивает курсы валют с сайта НБУ (bank.gov.ua) и записывает в exchange_rates.
    /// Возвращает (число обновлённых валют, дата курса). Бросает исключение при ошибке сети.</summary>
    public static (int count, string date) DownloadRatesNbu()
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var json = http.GetStringAsync("https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?json")
                       .GetAwaiter().GetResult();

        // code -> (rate, isoDate) из ответа НБУ (UAH за 1 единицу)
        var nbu = new Dictionary<string, (double rate, string date)>(StringComparer.OrdinalIgnoreCase);
        using (var doc = System.Text.Json.JsonDocument.Parse(json))
        {
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var cc = el.GetProperty("cc").GetString() ?? "";
                double rate = el.GetProperty("rate").GetDouble();
                var exdate = el.TryGetProperty("exchangedate", out var d) ? d.GetString() ?? "" : "";
                string iso = DateTime.TryParseExact(exdate, "dd.MM.yyyy", null,
                                System.Globalization.DateTimeStyles.None, out var dt)
                             ? dt.ToString("yyyy-MM-dd") : DateTime.Today.ToString("yyyy-MM-dd");
                if (!string.IsNullOrEmpty(cc)) nbu[cc] = (rate, iso);
            }
        }

        using var conn = Db.Open();
        int count = 0; string usedDate = DateTime.Today.ToString("yyyy-MM-dd");
        // курс ставим всем валютам кроме базовой (по умолчанию)
        var curs = conn.Query<Currency>(
            "SELECT id, code, is_default AS IsDefault FROM currencies WHERE is_default=0").ToList();
        foreach (var c in curs)
        {
            if (!nbu.TryGetValue(c.Code, out var v)) continue;
            conn.Execute("DELETE FROM exchange_rates WHERE currency_id=@id AND date=@d",
                new { id = c.Id, d = v.date });
            conn.Execute("INSERT INTO exchange_rates(currency_id,date,rate) VALUES(@id,@d,@r)",
                new { id = c.Id, d = v.date, r = v.rate });
            usedDate = v.date; count++;
        }
        return (count, usedDate);
    }
}
