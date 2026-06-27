namespace HomeAccounting.Services;

/// <summary>Текущий вошедший пользователь. Все сервисы фильтруют данные по Session.UserId.</summary>
public static class Session
{
    public static int    UserId   { get; private set; } = 1;
    public static string UserName { get; private set; } = "";

    /// <summary>Срабатывает при смене пользователя — UI перечитывает данные.</summary>
    public static event Action? UserChanged;

    public static void Set(int id, string name)
    {
        UserId   = id;
        UserName = name;
        UserChanged?.Invoke();
    }
}
