using System.Text.Json;

namespace HomeAccounting.Services;

public class AppSettings
{
    public string Lang             { get; set; } = "ru";
    public string Theme            { get; set; } = "Garden";    // Garden | Windows
    public string XmlWatchFolder   { get; set; } = "";
    public int?   DefaultAccountId { get; set; } = null;
    public float  TableFontSize    { get; set; } = 9f;
    public double WindowTop        { get; set; } = double.NaN;
    public double WindowLeft       { get; set; } = double.NaN;
    public double WindowWidth      { get; set; } = 1100;
    public double WindowHeight     { get; set; } = 680;
    public bool   WindowMaximized  { get; set; } = false;

    // Автоматическое резервное копирование при выходе
    public bool   AutoBackupEnabled { get; set; } = false;
    public string AutoBackupFolder  { get; set; } = "";
    public int    AutoBackupKeep    { get; set; } = 10;

    private static string FilePath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAccounting", "ha_settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); } catch { }
    }
}
