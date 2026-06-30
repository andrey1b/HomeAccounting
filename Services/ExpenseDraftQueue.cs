using System.IO;
using System.Text.Json;

namespace HomeAccounting.Services;

// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  Общая очередь черновиков расходов Senior Hub.                             ║
// ║                                                                            ║
// ║  Программы-спутники («Еда», «Таблетки», «Коммуналка»…) кладут сюда         ║
// ║  черновик расхода кнопкой «Записать в «Деньги»». «Деньги» при запуске      ║
// ║  показывают список и по подтверждению создают расходы у себя — то есть     ║
// ║  ЗАПИСЬ делает только «Деньги» (правило офиса соблюдено).                   ║
// ║                                                                            ║
// ║  Файл: %LOCALAPPDATA%\SeniorHub\pending_expenses.json                      ║
// ║  Это КАНОНИЧЕСКАЯ версия файла — копируется в каждую программу КАК ЕСТЬ     ║
// ║  (меняется только namespace).                                              ║
// ╚══════════════════════════════════════════════════════════════════════════╝

// Один черновик расхода. Category/Subcategory — ИМЕНА (резолвятся в «Деньгах»).
public sealed record ExpenseDraft(
    string Id, string Source, string Date,
    string Category, string? Subcategory, double Amount, string Note);

public static class ExpenseDraftQueue
{
    private static string QueuePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeniorHub", "pending_expenses.json");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    public static List<ExpenseDraft> ReadAll()
    {
        try
        {
            if (!File.Exists(QueuePath)) return new();
            var json = File.ReadAllText(QueuePath);
            if (string.IsNullOrWhiteSpace(json)) return new();
            return JsonSerializer.Deserialize<List<ExpenseDraft>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void AddRange(IEnumerable<ExpenseDraft> drafts)
    {
        var list = ReadAll();
        list.AddRange(drafts);
        Write(list);
    }

    public static void Add(ExpenseDraft draft) => AddRange(new[] { draft });

    // Удалить обработанные черновики по их Id (остальные — оставить в очереди).
    public static void RemoveByIds(IEnumerable<string> ids)
    {
        var set = new HashSet<string>(ids);
        Write(ReadAll().Where(d => !set.Contains(d.Id)).ToList());
    }

    public static int Count() => ReadAll().Count;

    // Атомарная запись: во временный файл, затем замена — чтобы не оставить
    // повреждённый файл, если запись прервётся.
    private static void Write(List<ExpenseDraft> list)
    {
        try
        {
            var dir = Path.GetDirectoryName(QueuePath)!;
            Directory.CreateDirectory(dir);
            var tmp = QueuePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOpts));
            if (File.Exists(QueuePath)) File.Delete(QueuePath);
            File.Move(tmp, QueuePath);
        }
        catch { /* очередь не должна ронять программу */ }
    }
}
