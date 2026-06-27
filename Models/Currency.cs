namespace HomeAccounting.Models;

public class Currency
{
    public int    Id        { get; set; }
    public string Code      { get; set; } = "";
    public string Name      { get; set; } = "";
    public string Symbol    { get; set; } = "";
    public bool   IsDefault { get; set; }
    public int    SortOrder { get; set; }

    public string Display => $"{Symbol}  {Name} ({Code})";
}

public class ExchangeRate
{
    public int      Id           { get; set; }
    public int      CurrencyId   { get; set; }
    public string   CurrencyName { get; set; } = "";
    public DateTime Date         { get; set; }
    public double   Rate         { get; set; }

    public string DateStr => Date.ToString("dd.MM.yyyy");
    public string RateStr => $"{Rate:N4}";
}
