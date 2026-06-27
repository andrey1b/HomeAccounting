namespace HomeAccounting.Models;

public class Budget
{
    public int    Id            { get; set; }
    public string Type          { get; set; } = "expense";   // expense | income
    public int    Year          { get; set; }
    public int    Month         { get; set; }
    public int?   CategoryId    { get; set; }
    public int?   SubcategoryId { get; set; }
    public int?   CurrencyId    { get; set; }
    public double Plan          { get; set; }
    public string Note          { get; set; } = "";

    // Заполняется запросом
    public string CategoryName    { get; set; } = "";
    public string SubcategoryName { get; set; } = "";
    public double Fact            { get; set; }

    public double Diff      => Plan - Fact;
    public string PlanStr   => $"{Plan:N2}";
    public string FactStr   => $"{Fact:N2}";
    public string DiffStr   => $"{Diff:N2}";
}
