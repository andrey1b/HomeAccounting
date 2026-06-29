namespace HomeAccounting.Models;

/// <summary>Позиция списка покупок (подкатегория продуктов/хозтоваров).</summary>
public class ShopItem
{
    public bool   Include  { get; set; }
    public string Name     { get; set; } = "";
    public string Qty      { get; set; } = "1";
    public string Unit     { get; set; } = "";
    public string Category { get; set; } = "";
}
