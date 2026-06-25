using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace HomeAccounting.Services;

public class ReceiptItem
{
    public string Name     { get; set; } = "";
    public string Barcode  { get; set; } = "";
    public string UnitName { get; set; } = "";
    public double Quantity { get; set; } = 1;
    public double Cost     { get; set; }
}

public class ParsedReceipt
{
    public DateTime          Date        { get; set; } = DateTime.Today;
    public string            Store       { get; set; } = "";
    public string            ReceiptNo   { get; set; } = "";
    public string            TimeStr     { get; set; } = "";
    public string            PaymentKind { get; set; } = "";
    public string            PaymentMask { get; set; } = "";
    public string            TN          { get; set; } = "";
    public List<ReceiptItem> Items       { get; set; } = new();
}

public static class ReceiptXmlParser
{
    static ReceiptXmlParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static ParsedReceipt Parse(string xmlPath)
    {
        try
        {
            var enc = Encoding.GetEncoding("windows-1251");
            string raw;
            using (var fs = File.OpenRead(xmlPath))
            using (var sr = new StreamReader(fs, enc))
                raw = sr.ReadToEnd();

            // Strip XML declaration to avoid encoding mismatch in XDocument.Parse
            if (raw.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                var end = raw.IndexOf("?>");
                if (end >= 0) raw = raw[(end + 2)..].TrimStart();
            }

            var root = XDocument.Parse(raw).Root;
            if (root == null) return new();

            return root.Name.LocalName switch
            {
                "CHECK" => ParseCheck(root),
                "RQ"    => ParseRq(root),
                _       => new()
            };
        }
        catch { return new(); }
    }

    // ─── CHECK format ────────────────────────────────────────────────────────
    // <CHECK><CHECKHEAD><ORGNM/><ORDERDATE>DDMMYYYY</ORDERDATE>
    //   <ORDERTIME>HHMMSS</ORDERTIME><ORDERNUM>N</ORDERNUM></CHECKHEAD>
    //   <CHECKBODY><ROW><CODE/><NAME/><UNITNM/><AMOUNT/><COST/></ROW></CHECKBODY>
    //   <CHECKPAY><ROW><PAYFORMCODE/><PAYFORMNM/><SUM/></ROW></CHECKPAY></CHECK>

    private static ParsedReceipt ParseCheck(XElement root)
    {
        var head      = root.Element("CHECKHEAD");
        var store     = head?.Element("ORGNM")?.Value ?? "";
        var date      = ParseCheckDate(head?.Element("ORDERDATE")?.Value);
        var timeStr   = FormatTime6(head?.Element("ORDERTIME")?.Value);
        var receiptNo = (head?.Element("ORDERNUM")?.Value ?? "").Trim();

        var (payKind, payMask) = ParseCheckPayment(root.Element("CHECKPAY"));

        var items = new List<ReceiptItem>();
        var body  = root.Element("CHECKBODY");
        if (body != null)
        {
            foreach (var row in body.Elements("ROW"))
            {
                var name = (row.Element("NAME")?.Value ?? "").Trim();
                if (string.IsNullOrEmpty(name)) continue;

                var cost = ParseDecimal(row.Element("COST")?.Value);
                if (cost <= 0) continue;

                items.Add(new ReceiptItem
                {
                    Name     = name,
                    Barcode  = (row.Element("CODE")?.Value ?? "").Trim(),
                    UnitName = (row.Element("UNITNM")?.Value ?? "").Trim(),
                    Quantity = ParseDecimal(row.Element("AMOUNT")?.Value, 1),
                    Cost     = cost
                });
            }
        }

        return new ParsedReceipt
        {
            Date = date, Store = store, ReceiptNo = receiptNo, TimeStr = timeStr,
            PaymentKind = payKind, PaymentMask = payMask, Items = items
        };
    }

    private static (string kind, string mask) ParseCheckPayment(XElement? checkPay)
    {
        if (checkPay == null) return ("", "");
        foreach (var row in checkPay.Elements("ROW"))
        {
            var nm   = (row.Element("PAYFORMNM")?.Value ?? "").Trim();
            var code = (row.Element("PAYFORMCODE")?.Value ?? "").Trim();
            if (string.IsNullOrEmpty(nm))
                nm = code == "0" ? "Готівка" : code == "1" ? "Картка" : "";
            if (!string.IsNullOrEmpty(nm)) return (nm, "");
        }
        return ("", "");
    }

    // ─── RQ format ───────────────────────────────────────────────────────────
    // <RQ NDv="store"><DAT TN="..."><C T="0">
    //   <P CD="barcode" NM="name" SM="kopecks" Q="thousandths"/>
    //   <M NM="ГОТIВКА|КАРТКА" T="0|1" SM="kopecks"/>
    //   <E NO="receipt_no" TS="YYYYMMDDHHmmss"/></C>
    //   <TS>YYYYMMDDHHmmss</TS></DAT></RQ>

    private static ParsedReceipt ParseRq(XElement root)
    {
        var store = (root.Attribute("NDv")?.Value ?? "").Trim();
        var dat   = root.Element("DAT");
        if (dat == null) return new();

        var tn    = (dat.Attribute("TN")?.Value ?? "").Trim();
        var c     = dat.Element("C");
        var eElem = c?.Element("E");
        var tsVal = eElem?.Attribute("TS")?.Value ?? dat.Element("TS")?.Value;

        var date      = ParseTs(tsVal);
        var timeStr   = ParseTsTime(tsVal);
        var receiptNo = (eElem?.Attribute("NO")?.Value ?? "").Trim();

        var (payKind, payMask) = ParseRqPayment(c);

        var items = new List<ReceiptItem>();
        if (c != null)
        {
            foreach (var p in c.Elements("P"))
            {
                var name = (p.Attribute("NM")?.Value ?? "").Trim();
                if (string.IsNullOrEmpty(name)) continue;

                var smStr = p.Attribute("SM")?.Value;
                var qStr  = p.Attribute("Q")?.Value;

                // SM is in kopecks (÷100), Q is in thousandths (÷1000)
                var cost = smStr != null ? ParseLong(smStr) / 100.0 : 0;
                if (cost <= 0) continue;

                var qty = qStr != null ? ParseLong(qStr) / 1000.0 : 1.0;
                if (qty <= 0) qty = 1;

                items.Add(new ReceiptItem
                {
                    Name     = name,
                    Barcode  = (p.Attribute("CD")?.Value ?? "").Trim(),
                    UnitName = "",
                    Quantity = qty,
                    Cost     = cost
                });
            }
        }

        return new ParsedReceipt
        {
            Date = date, Store = store, TN = tn, ReceiptNo = receiptNo, TimeStr = timeStr,
            PaymentKind = payKind, PaymentMask = payMask, Items = items
        };
    }

    private static (string kind, string mask) ParseRqPayment(XElement? c)
    {
        if (c == null) return ("", "");
        string cardKind = "", cashKind = "";
        foreach (var m in c.Elements("M"))
        {
            var nm = (m.Attribute("NM")?.Value ?? "").Trim();
            var t  = m.Attribute("T")?.Value ?? "";
            var sm = ParseLong(m.Attribute("SM")?.Value ?? "0");
            if (sm > 0)
            {
                if (t == "1" && string.IsNullOrEmpty(cardKind)) cardKind = nm;
                if (t == "0" && string.IsNullOrEmpty(cashKind)) cashKind = nm;
            }
        }
        if (!string.IsNullOrEmpty(cardKind)) return (cardKind, "");
        if (!string.IsNullOrEmpty(cashKind)) return (cashKind, "");
        var first = c.Elements("M").FirstOrDefault();
        return ((first?.Attribute("NM")?.Value ?? "").Trim(), "");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string FormatTime6(string? s)
    {
        // HHMMSS → HH:MM:SS
        if (s != null && s.Length >= 6)
            return $"{s[..2]}:{s[2..4]}:{s[4..6]}";
        return "";
    }

    private static string ParseTsTime(string? s)
    {
        // YYYYMMDDHHmmss → HH:MM:SS
        if (s != null && s.Length >= 14)
            return $"{s[8..10]}:{s[10..12]}:{s[12..14]}";
        return "";
    }

    private static DateTime ParseCheckDate(string? s)
    {
        // Format: DDMMYYYY (8 chars)
        if (s != null && s.Length == 8 &&
            int.TryParse(s[..2], out int d) &&
            int.TryParse(s[2..4], out int m) &&
            int.TryParse(s[4..], out int y))
        {
            try { return new DateTime(y, m, d); } catch { }
        }
        return DateTime.Today;
    }

    private static DateTime ParseTs(string? s)
    {
        // Format: YYYYMMDDHHmmss or YYYYMMDD (14 or 8 chars)
        if (s != null && s.Length >= 8 &&
            int.TryParse(s[..4], out int y) &&
            int.TryParse(s[4..6], out int m) &&
            int.TryParse(s[6..8], out int d))
        {
            try { return new DateTime(y, m, d); } catch { }
        }
        return DateTime.Today;
    }

    private static double ParseDecimal(string? s, double fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        return double.TryParse(s.Replace(',', '.'), NumberStyles.Any,
                               CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private static long ParseLong(string s) =>
        long.TryParse(s, out var v) ? v : 0;
}
