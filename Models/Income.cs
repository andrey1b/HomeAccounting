namespace HomeAccounting.Models;

public class Income
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public double? Quantity { get; set; }
    public int? UnitId { get; set; }
    public double Amount { get; set; }
    public string Note { get; set; } = "";
}

public class IncomeRow
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int? SubcategoryId { get; set; }
    public string SubcategoryName { get; set; } = "";
    public double? Quantity { get; set; }
    public int? UnitId { get; set; }
    public string UnitName { get; set; } = "";
    public double Amount { get; set; }
    public string Note { get; set; } = "";

    public string DateStr   => Date.ToString("dd.MM.yyyy");
    public string AmountStr => $"{Amount:N2}";
    public string QtyStr    => Quantity is null or 1 ? "" : $"{Quantity:G}";
}
