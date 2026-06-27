using Dapper;

namespace HomeAccounting.Services;

public class ItemMapping
{
    public int    Id             { get; set; }
    public string NameKey        { get; set; } = "";
    public int?   CategoryId     { get; set; }
    public int?   SubcategoryId  { get; set; }
    public int?   UnitId         { get; set; }
}

public static class ItemMappingService
{
    private static string Key(string name) => name.Trim().ToLowerInvariant();

    public static ItemMapping? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        using var conn = Db.Open();
        return conn.QueryFirstOrDefault<ItemMapping>(
            "SELECT id, name_key AS NameKey, category_id AS CategoryId, subcategory_id AS SubcategoryId, unit_id AS UnitId FROM item_mappings WHERE user_id=@uid AND name_key = @k",
            new { uid = Session.UserId, k = Key(name) });
    }

    public static ItemMapping? FindBarcode(string barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : Find("bc:" + barcode.Trim());

    public static void SaveBarcode(string barcode, int? catId, int? subcatId)
    {
        if (!string.IsNullOrWhiteSpace(barcode))
            Save("bc:" + barcode.Trim(), catId, subcatId, null);
    }

    public static List<ItemMapping> GetAll()
    {
        using var conn = Db.Open();
        return conn.Query<ItemMapping>(
            "SELECT id, name_key AS NameKey, category_id AS CategoryId, " +
            "subcategory_id AS SubcategoryId, unit_id AS UnitId FROM item_mappings WHERE user_id=@uid",
            new { uid = Session.UserId }
        ).ToList();
    }

    public static void Save(string name, int? catId, int? subcatId, int? unitId)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        using var conn = Db.Open();
        conn.Execute(@"
            INSERT INTO item_mappings(user_id, name_key, category_id, subcategory_id, unit_id)
            VALUES(@uid, @k, @c, @s, @u)
            ON CONFLICT(user_id, name_key) DO UPDATE SET
                category_id    = @c,
                subcategory_id = @s,
                unit_id        = @u",
            new { uid = Session.UserId, k = Key(name), c = catId, s = subcatId, u = unitId });
    }
}
