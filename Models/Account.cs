namespace HomeAccounting.Models;

public class Account
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public double InitialBalance { get; set; }
    public bool IsHidden { get; set; }
    public string Currency { get; set; } = "₴";
    public int?   CurrencyId { get; set; }
    public string Icon { get; set; } = "💰";

    // Заполняется запросом с агрегатами
    public double TotalExpense { get; set; }
    public double TotalIncome { get; set; }
    public double TransfersIn { get; set; }
    public double TransfersOut { get; set; }

    public double Balance => InitialBalance + TotalIncome - TotalExpense + TransfersIn - TransfersOut;
    public string BalanceStr => $"{Balance:N2} {Currency}";
    public string ExpenseStr => $"{TotalExpense:N2} {Currency}";
    public string IncomeStr  => $"{TotalIncome:N2} {Currency}";
}
