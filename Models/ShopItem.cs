using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HomeAccounting.Models;

/// <summary>Позиция списка покупок (подкатегория продуктов/хозтоваров/аптеки).</summary>
public class ShopItem : INotifyPropertyChanged
{
    private bool   _include;
    private string _qty = "1";

    public bool Include
    {
        get => _include;
        set { if (_include != value) { _include = value; OnChanged(); } }
    }

    public string Qty
    {
        get => _qty;
        set { if (_qty != value) { _qty = value; OnChanged(); } }
    }

    public string Name     { get; set; } = "";
    public string Unit     { get; set; } = "";
    public string Category { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
