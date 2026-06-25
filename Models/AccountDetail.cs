using System.ComponentModel;

namespace HomeAccounting.Models;

public class DetailGroup : INotifyPropertyChanged
{
    private bool _isExpanded;

    public int    AccountId    { get; set; }
    public string AccountName  { get; set; } = "";
    public string DateRaw      { get; set; } = "";
    public double TotalExpense { get; set; }
    public double TotalIncome  { get; set; }

    public string DateStr => DateTime.TryParse(DateRaw, out var dt) ? dt.ToString("dd.MM.yyyy") : DateRaw;
    public string ExpStr  => TotalExpense > 0 ? $"{TotalExpense:N2} ₴" : "";
    public string IncStr  => TotalIncome  > 0 ? $"{TotalIncome:N2} ₴"  : "";

    public List<DetailSection> Sections { get; set; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandBtn)));
        }
    }
    public string ExpandBtn => IsExpanded ? "–" : "+";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DetailSection
{
    public string Title { get; set; } = "";
    public List<DetailItem> Items { get; set; } = new();
}

public class DetailItem
{
    public string Col1 { get; set; } = "";
    public string Col2 { get; set; } = "";
    public double Amount { get; set; }
    public string AmountStr => $"{Amount:N2} ₴";
    public string Note { get; set; } = "";
}
