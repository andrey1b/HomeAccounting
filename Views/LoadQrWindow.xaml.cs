using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeAccounting.Services;

namespace HomeAccounting.Views;

public partial class LoadQrWindow : Window
{
    public string QrUrl { get; private set; } = "";

    public LoadQrWindow()
    {
        InitializeComponent();
        ApplyLoc();
    }

    void ApplyLoc()
    {
        Title                   = AppLoc.T("dlg_load_qr_title");
        TbUrlLabel.Text         = AppLoc.T("lbl_qr_url");
        BtnPasteText.Content    = AppLoc.T("btn_paste_text");
        BtnQrClipboard.Content  = AppLoc.T("btn_qr_clipboard");
        BtnQrFile.Content       = AppLoc.T("btn_qr_file");
        BtnOk.Content           = AppLoc.T("btn_load_receipt");
        BtnCancel.Content       = AppLoc.T("btn_cancel");
    }

    void BtnPasteText_Click(object s, RoutedEventArgs e)
    {
        var text = Clipboard.GetText().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            TbUrl.Text = text;
            ShowStatus(AppLoc.T("msg_qr_decoded"), success: true);
        }
        else
        {
            ShowStatus("Буфер обмена не содержит текст", success: false);
        }
    }

    void BtnQrClipboard_Click(object s, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            ShowStatus("Буфер обмена не содержит изображение", success: false);
            return;
        }
        try
        {
            DecodeAndFill(Clipboard.GetImage());
        }
        catch (Exception ex) { ShowStatus(ex.Message, success: false); }
    }

    void BtnQrFile_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*",
            Title  = "Выберите изображение QR-кода"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var bi = new BitmapImage(new Uri(dlg.FileName));
            bi.Freeze();
            DecodeAndFill(bi);
        }
        catch (Exception ex) { ShowStatus(ex.Message, success: false); }
    }

    void DecodeAndFill(BitmapSource bitmapSource)
    {
        var url = TryDecodeQr(bitmapSource);
        if (url != null)
        {
            TbUrl.Text = url;
            ShowStatus(AppLoc.T("msg_qr_decoded"), success: true);
        }
        else
        {
            ShowStatus(AppLoc.T("msg_qr_decode_fail"), success: false);
        }
    }

    static string? TryDecodeQr(BitmapSource bitmapSource)
    {
        try
        {
            // Convert any format → RGB24 (WPF native, no System.Drawing needed)
            var rgb = new FormatConvertedBitmap(bitmapSource, PixelFormats.Rgb24, null, 0);
            var w = rgb.PixelWidth;
            var h = rgb.PixelHeight;
            var stride = w * 3;
            var data = new byte[stride * h];
            rgb.CopyPixels(data, stride, 0);

            var luminance = new ZXing.RGBLuminanceSource(
                data, w, h, ZXing.RGBLuminanceSource.BitmapFormat.RGB24);
            var reader = new ZXing.BarcodeReaderGeneric
            {
                Options = new ZXing.Common.DecodingOptions
                {
                    PossibleFormats = new[] { ZXing.BarcodeFormat.QR_CODE },
                    TryHarder = true
                }
            };
            return reader.Decode(luminance)?.Text;
        }
        catch { return null; }
    }

    void BtnOk_Click(object s, RoutedEventArgs e)
    {
        var url = TbUrl.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            ShowStatus(AppLoc.T("msg_qr_no_url"), success: false);
            return;
        }
        QrUrl = url;
        DialogResult = true;
    }

    void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;

    void ShowStatus(string text, bool success)
    {
        TbStatus.Text       = text;
        TbStatus.Foreground = success
            ? Brushes.DarkGreen
            : Brushes.OrangeRed;
    }
}
