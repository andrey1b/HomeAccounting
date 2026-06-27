using Dapper;

namespace HomeAccounting.Services;

public static class UserService
{
    public static List<User> GetAll()
    {
        using var conn = Db.Open();
        return conn.Query<User>(
            @"SELECT id, name, password_hash AS PasswordHash, sort_order AS SortOrder,
                     is_default AS IsDefault, remind_q AS RemindQ, remind_a AS RemindA
              FROM users ORDER BY sort_order, id").ToList();
    }

    public static User? GetById(int id)
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<User>(
            @"SELECT id, name, password_hash AS PasswordHash, sort_order AS SortOrder,
                     is_default AS IsDefault, remind_q AS RemindQ, remind_a AS RemindA
              FROM users WHERE id=@id", new { id });
    }

    public static User? GetDefault()
    {
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<User>(
            @"SELECT id, name, password_hash AS PasswordHash, sort_order AS SortOrder,
                     is_default AS IsDefault, remind_q AS RemindQ, remind_a AS RemindA
              FROM users WHERE is_default=1 ORDER BY id LIMIT 1");
    }

    /// <summary>Проверка пароля. Пустой хеш у пользователя → вход без пароля.</summary>
    public static bool Authenticate(int id, string password)
    {
        var u = GetById(id);
        if (u == null) return false;
        if (!u.HasPassword) return true;
        return string.Equals(u.PasswordHash, Db.HashPassword(password), StringComparison.OrdinalIgnoreCase);
    }

    public static int Add(string name, string? password)
    {
        using var conn = Db.Open();
        var maxOrder = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(sort_order),0) FROM users");
        return conn.ExecuteScalar<int>(
            @"INSERT INTO users(name,password_hash,sort_order,is_default)
              VALUES(@n,@p,@o,0); SELECT last_insert_rowid();",
            new { n = name, p = Db.HashPassword(password), o = maxOrder + 1 });
    }

    public static void Rename(int id, string name)
    {
        using var conn = Db.Open();
        conn.Execute("UPDATE users SET name=@n WHERE id=@id", new { n = name, id });
    }

    public static void ChangePassword(int id, string? password)
    {
        using var conn = Db.Open();
        conn.Execute("UPDATE users SET password_hash=@p WHERE id=@id",
            new { p = Db.HashPassword(password), id });
    }

    public static void SetDefault(int id)
    {
        using var conn = Db.Open();
        using var tr = conn.BeginTransaction();
        conn.Execute("UPDATE users SET is_default=0", transaction: tr);
        conn.Execute("UPDATE users SET is_default=1 WHERE id=@id", new { id }, tr);
        tr.Commit();
    }

    /// <summary>Удаляет пользователя вместе со всеми его данными.</summary>
    public static void Delete(int id)
    {
        using var conn = Db.Open();
        conn.Execute("PRAGMA foreign_keys = OFF;");
        using var tr = conn.BeginTransaction();
        foreach (var t in new[] { "expenses", "incomes", "transfers", "budgets", "debts",
                                  "deposits", "item_mappings", "subcategories", "categories",
                                  "units", "accounts" })
            conn.Execute($"DELETE FROM {t} WHERE user_id=@id", new { id }, tr);
        conn.Execute("DELETE FROM users WHERE id=@id", new { id }, tr);
        tr.Commit();
        conn.Execute("PRAGMA foreign_keys = ON;");
    }
}
