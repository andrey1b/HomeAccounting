namespace HomeAccounting.Models;

public class User
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int    SortOrder    { get; set; }
    public bool   IsDefault    { get; set; }
    public string RemindQ      { get; set; } = "";
    public string RemindA      { get; set; } = "";

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);
}
