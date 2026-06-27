namespace HomeAccounting.Models;

public class Deposit
{
    public int      Id         { get; set; }
    public string   Name       { get; set; } = "";
    public int?     AccountId  { get; set; }
    public int?     CurrencyId { get; set; }
    public double   Amount     { get; set; }
    public double   Rate       { get; set; }
    public string   OpenDate   { get; set; } = "";   // 'yyyy-MM-dd' или пусто
    public string   CloseDate  { get; set; } = "";
    public string   Note       { get; set; } = "";

    // Заполняется запросом
    public string AccountName { get; set; } = "";
    public string CurrencySym { get; set; } = "";

    private static string Fmt(string iso) =>
        DateTime.TryParse(iso, out var d) ? d.ToString("dd.MM.yyyy") : "";

    public string AmountStr   => $"{Amount:N2} {CurrencySym}".Trim();
    public string RateStr     => $"{Rate:N2} %";
    public string OpenStr     => Fmt(OpenDate);
    public string CloseStr    => Fmt(CloseDate);
}
