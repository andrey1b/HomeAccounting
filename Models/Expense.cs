namespace HomeAccounting.Models;

public class Expense
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public double? Quantity { get; set; }
    public int? UnitId { get; set; }
    public double Amount { get; set; }
    public double Discount { get; set; }
    public string Note { get; set; } = "";
    public string ReceiptItemName { get; set; } = "";

    public double EffectiveAmount => Amount * (1 - Discount / 100);
}

// Для отображения в DataGrid (с именами через JOIN)
public class ExpenseRow
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
    public double Discount { get; set; }
    public string Note { get; set; } = "";

    public double EffectiveAmount => Amount * (1 - Discount / 100);
    public string DateStr   => Date.ToString("dd.MM.yyyy");
    public string AmountStr => $"{EffectiveAmount:N2}";
    public string QtyStr    => Quantity is null or 1 ? "" : $"{Quantity:G}";
}
