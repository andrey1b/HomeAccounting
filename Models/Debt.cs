namespace HomeAccounting.Models;

/// <summary>Долг: kind = "debtor" (мне должны) | "creditor" (я должен).</summary>
public class Debt
{
    public int      Id           { get; set; }
    public string   Kind         { get; set; } = "debtor";
    public int?     AccountId    { get; set; }
    public DateTime Date         { get; set; } = DateTime.Today;
    public string   Counterparty { get; set; } = "";
    public int?     CurrencyId   { get; set; }
    public double   Amount       { get; set; }
    public double   AmountBack   { get; set; }
    public double   Percent      { get; set; }
    public bool     IsClosed     { get; set; }
    public string   DateClose    { get; set; } = "";   // 'yyyy-MM-dd' или пусто
    public string   Note         { get; set; } = "";

    // Заполняется запросом
    public string AccountName  { get; set; } = "";
    public string CurrencySym  { get; set; } = "";

    public double Remaining => Amount - AmountBack;
    public string DateStr   => Date.ToString("dd.MM.yyyy");
    public string AmountStr    => $"{Amount:N2} {CurrencySym}".Trim();
    public string BackStr      => $"{AmountBack:N2} {CurrencySym}".Trim();
    public string RemainingStr => $"{Remaining:N2} {CurrencySym}".Trim();
    public string StatusStr  => IsClosed ? "Погашен" : "Не погашен";
}
