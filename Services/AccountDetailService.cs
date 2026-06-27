using Dapper;

namespace HomeAccounting.Services;

public static class AccountDetailService
{
    public static List<DetailGroup> GetGroups(DateTime from, DateTime to, int? accountId = null)
    {
        using var conn = Db.Open();

        var summaries = conn.Query(@"
            SELECT combined.account_id AS AccountId, a.name AS AccountName,
                   combined.date AS DateRaw,
                   SUM(combined.expense) AS TotalExpense,
                   SUM(combined.income)  AS TotalIncome
            FROM (
                SELECT account_id, date, amount*(1-discount/100) AS expense, 0.0 AS income
                FROM expenses WHERE user_id = @uid AND date >= @from AND date <= @to
                  AND (@aid IS NULL OR account_id = @aid)
                UNION ALL
                SELECT account_id, date, 0.0, amount
                FROM incomes  WHERE user_id = @uid AND date >= @from AND date <= @to
                  AND (@aid IS NULL OR account_id = @aid)
            ) combined
            JOIN accounts a ON a.id = combined.account_id
            GROUP BY combined.account_id, combined.date
            ORDER BY combined.date DESC, a.name",
            new { uid = Session.UserId, from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), aid = accountId })
            .ToList();

        var groups = new List<DetailGroup>();
        foreach (var s in summaries)
        {
            string dateRaw = s.DateRaw?.ToString() ?? "";
            var g = new DetailGroup
            {
                AccountId    = (int)(long)s.AccountId,
                AccountName  = s.AccountName?.ToString() ?? "",
                DateRaw      = dateRaw,
                TotalExpense = s.TotalExpense  is double te ? te : Convert.ToDouble(s.TotalExpense ?? 0),
                TotalIncome  = s.TotalIncome   is double ti ? ti : Convert.ToDouble(s.TotalIncome  ?? 0),
            };

            var expRows = conn.Query(@"
                SELECT COALESCE(c.name,'') AS Col1, COALESCE(s2.name,'') AS Col2,
                       e.amount*(1-e.discount/100) AS Amount, COALESCE(e.note,'') AS Note
                FROM expenses e
                LEFT JOIN categories    c  ON c.id  = e.category_id
                LEFT JOIN subcategories s2 ON s2.id = e.subcategory_id
                WHERE e.user_id = @uid AND e.account_id = @aid AND e.date = @dt ORDER BY e.id",
                new { uid = Session.UserId, aid = g.AccountId, dt = dateRaw }).ToList();
            if (expRows.Count > 0)
                g.Sections.Add(new DetailSection
                {
                    Title = AppLoc.T("rb_expenses"),
                    Items = expRows.Select<dynamic, DetailItem>(r => ToItem(r)).ToList()
                });

            var incRows = conn.Query(@"
                SELECT COALESCE(c.name,'') AS Col1, COALESCE(s2.name,'') AS Col2,
                       i.amount AS Amount, COALESCE(i.note,'') AS Note
                FROM incomes i
                LEFT JOIN categories    c  ON c.id  = i.category_id
                LEFT JOIN subcategories s2 ON s2.id = i.subcategory_id
                WHERE i.user_id = @uid AND i.account_id = @aid AND i.date = @dt ORDER BY i.id",
                new { uid = Session.UserId, aid = g.AccountId, dt = dateRaw }).ToList();
            if (incRows.Count > 0)
                g.Sections.Add(new DetailSection
                {
                    Title = AppLoc.T("rb_incomes"),
                    Items = incRows.Select<dynamic, DetailItem>(r => ToItem(r)).ToList()
                });

            var trRows = conn.Query(@"
                SELECT
                    CASE WHEN t.from_account_id = @aid THEN '→ ' || af2.name
                         ELSE '← ' || af1.name END AS Col1,
                    '' AS Col2,
                    t.amount AS Amount, COALESCE(t.note,'') AS Note
                FROM transfers t
                JOIN accounts af1 ON af1.id = t.from_account_id
                JOIN accounts af2 ON af2.id = t.to_account_id
                WHERE t.user_id = @uid AND (t.from_account_id = @aid OR t.to_account_id = @aid) AND t.date = @dt
                ORDER BY t.id",
                new { uid = Session.UserId, aid = g.AccountId, dt = dateRaw }).ToList();
            if (trRows.Count > 0)
                g.Sections.Add(new DetailSection
                {
                    Title = AppLoc.T("tab_acc_transfers"),
                    Items = trRows.Select<dynamic, DetailItem>(r => ToItem(r)).ToList()
                });

            groups.Add(g);
        }

        return groups;
    }

    private static DetailItem ToItem(dynamic r) => new()
    {
        Col1   = r.Col1?.ToString()  ?? "",
        Col2   = r.Col2?.ToString()  ?? "",
        Amount = Convert.ToDouble(r.Amount ?? 0),
        Note   = r.Note?.ToString()  ?? ""
    };
}
