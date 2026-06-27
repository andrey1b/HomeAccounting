using Dapper;

namespace HomeAccounting.Services;

public static class CategoryService
{
    public static List<Category> GetAll(string type = "expense")
    {
        using var conn = Db.Open();
        return conn.Query<Category>(
            "SELECT id, name, type FROM categories WHERE user_id=@uid AND type=@t ORDER BY name",
            new { uid = Session.UserId, t = type }).ToList();
    }

    public static List<Subcategory> GetSubcategories(int categoryId)
    {
        using var conn = Db.Open();
        return conn.Query<Subcategory>(
            "SELECT id, category_id, name FROM subcategories WHERE user_id=@uid AND category_id=@cid ORDER BY name",
            new { uid = Session.UserId, cid = categoryId }).ToList();
    }

    public static int AddCategory(string name, string type)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT INTO categories(user_id,name,type) VALUES(@uid,@n,@t); SELECT last_insert_rowid();",
            new { uid = Session.UserId, n = name, t = type });
    }

    public static void UpdateCategory(int id, string name) =>
        Db.Open().Execute("UPDATE categories SET name=@n WHERE id=@id AND user_id=@uid",
            new { n = name, id, uid = Session.UserId });

    public static void DeleteCategory(int id) =>
        Db.Open().Execute("DELETE FROM categories WHERE id=@id AND user_id=@uid",
            new { id, uid = Session.UserId });

    public static int AddSubcategory(int categoryId, string name)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT INTO subcategories(user_id,category_id,name) VALUES(@uid,@cid,@n); SELECT last_insert_rowid();",
            new { uid = Session.UserId, cid = categoryId, n = name });
    }

    public static void UpdateSubcategory(int id, string name) =>
        Db.Open().Execute("UPDATE subcategories SET name=@n WHERE id=@id AND user_id=@uid",
            new { n = name, id, uid = Session.UserId });

    public static void DeleteSubcategory(int id) =>
        Db.Open().Execute("DELETE FROM subcategories WHERE id=@id AND user_id=@uid",
            new { id, uid = Session.UserId });

    public static List<Unit> GetUnits()
    {
        using var conn = Db.Open();
        return conn.Query<Unit>("SELECT id, name FROM units WHERE user_id=@uid ORDER BY name",
            new { uid = Session.UserId }).ToList();
    }

    public static int AddUnit(string name)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT OR IGNORE INTO units(user_id,name) VALUES(@uid,@n); SELECT id FROM units WHERE user_id=@uid AND name=@n;",
            new { uid = Session.UserId, n = name });
    }
}
