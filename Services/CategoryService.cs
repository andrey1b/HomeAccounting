using Dapper;

namespace HomeAccounting.Services;

public static class CategoryService
{
    public static List<Category> GetAll(string type = "expense")
    {
        using var conn = Db.Open();
        return conn.Query<Category>(
            "SELECT id, name, type FROM categories WHERE type=@t ORDER BY name", new { t = type }).ToList();
    }

    public static List<Subcategory> GetSubcategories(int categoryId)
    {
        using var conn = Db.Open();
        return conn.Query<Subcategory>(
            "SELECT id, category_id, name FROM subcategories WHERE category_id=@cid ORDER BY name",
            new { cid = categoryId }).ToList();
    }

    public static int AddCategory(string name, string type)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT INTO categories(name,type) VALUES(@n,@t); SELECT last_insert_rowid();",
            new { n = name, t = type });
    }

    public static void UpdateCategory(int id, string name) =>
        Db.Open().Execute("UPDATE categories SET name=@n WHERE id=@id", new { n = name, id });

    public static void DeleteCategory(int id) =>
        Db.Open().Execute("DELETE FROM categories WHERE id=@id", new { id });

    public static int AddSubcategory(int categoryId, string name)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT INTO subcategories(category_id,name) VALUES(@cid,@n); SELECT last_insert_rowid();",
            new { cid = categoryId, n = name });
    }

    public static void UpdateSubcategory(int id, string name) =>
        Db.Open().Execute("UPDATE subcategories SET name=@n WHERE id=@id", new { n = name, id });

    public static void DeleteSubcategory(int id) =>
        Db.Open().Execute("DELETE FROM subcategories WHERE id=@id", new { id });

    public static List<Unit> GetUnits()
    {
        using var conn = Db.Open();
        return conn.Query<Unit>("SELECT id, name FROM units ORDER BY name").ToList();
    }

    public static int AddUnit(string name)
    {
        using var conn = Db.Open();
        return conn.ExecuteScalar<int>(
            "INSERT OR IGNORE INTO units(name) VALUES(@n); SELECT id FROM units WHERE name=@n;",
            new { n = name });
    }
}
