namespace HomeAccounting.Models;

// Строка предпросмотра расходов
public class ExpImportRow
{
    public bool   Include     { get; set; } = true;
    public string DateStr     { get; set; } = "";
    public string Account     { get; set; } = "";
    public string Category    { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string Unit        { get; set; } = "";
    public string Qty         { get; set; } = "";
    public double Amount      { get; set; }
    public string Note        { get; set; } = "";
}

// Строка предпросмотра доходов
public class IncImportRow
{
    public bool   Include     { get; set; } = true;
    public string DateStr     { get; set; } = "";
    public string Account     { get; set; } = "";
    public string Category    { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string Qty         { get; set; } = "";
    public string Unit        { get; set; } = "";
    public double Amount      { get; set; }
    public string Note        { get; set; } = "";
}

// Строка предпросмотра счетов
public class AccImportRow
{
    public bool   Include     { get; set; } = true;
    public string Name        { get; set; } = "";
    public double InitBalance { get; set; }
    public string Note        { get; set; } = "";
}

// Строка «Счета подробно» (формат ДомБух7)
public class DetImportRow
{
    public bool   Include     { get; set; } = true;
    public string DateStr     { get; set; } = "";
    public string AccountName { get; set; } = "";
    public double Expense     { get; set; }
    public double Income      { get; set; }
    public double Other       { get; set; }   // Прочие операции
    public double Turnover    { get; set; }   // Оборот
    public string Note        { get; set; } = "";
}
