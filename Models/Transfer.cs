namespace HomeAccounting.Models;

public class Transfer
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int? FromAccountId { get; set; }
    public int? ToAccountId { get; set; }
    public double Amount { get; set; }
    public string Note { get; set; } = "";
}
