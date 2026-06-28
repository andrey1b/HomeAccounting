namespace HomeAccounting.Services;

public static class BackupService
{
    private const string Pattern = "homeaccounting_*.db";

    /// <summary>Создаёт резервную копию базы в папке folder и удаляет лишние, оставляя keep свежих.
    /// Возвращает путь созданного файла.</summary>
    public static string RunAutoBackup(string folder, int keep)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"homeaccounting_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        Database.Db.Backup(path);
        Prune(folder, keep);
        return path;
    }

    private static void Prune(string folder, int keep)
    {
        if (keep < 1) keep = 1;
        try
        {
            var files = Directory.GetFiles(folder, Pattern)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            foreach (var f in files.Skip(keep))
                try { f.Delete(); } catch { }
        }
        catch { }
    }
}
