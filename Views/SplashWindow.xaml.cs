namespace HomeAccounting.Views;

public partial class SplashWindow : System.Windows.Window
{
    public SplashWindow() => InitializeComponent();

    public void SetStatus(string text) => TbStatus.Text = text;
}
