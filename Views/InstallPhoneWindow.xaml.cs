using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using HomeAccounting.Services;
using QRCoder;

namespace HomeAccounting.Views;

public partial class InstallPhoneWindow : Window
{
    public InstallPhoneWindow()
    {
        InitializeComponent();
        ApplyLoc();
        GenerateQr();
    }

    void ApplyLoc()
    {
        Title            = AppLoc.T("dlg_install_title");
        TbStep1.Text     = AppLoc.T("dlg_install_step1");
        TbUrlLabel.Text  = AppLoc.T("dlg_install_url");
        TbHint.Text      = AppLoc.T("dlg_install_hint");
    }

    void GenerateQr()
    {
        var url = $"http://{ReceiptHttpReceiver.LocalIp}:{ReceiptHttpReceiver.Port}/setup";
        TbUrl.Text = url;

        var qrGen  = new QRCodeGenerator();
        var data   = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png    = new PngByteQRCode(data);
        var bytes  = png.GetGraphic(10);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();

        ImgQr.Source = bmp;
    }

    void TbUrl_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Clipboard.SetText(TbUrl.Text);
    }
}
