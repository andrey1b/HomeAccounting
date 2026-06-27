using System.Windows;

namespace HomeAccounting.Views;

public partial class UserDialog : Window
{
    public string UserName { get; private set; } = "";
    public string Password { get; private set; } = "";

    public UserDialog(string? name = null, bool passwordOnly = false)
    {
        InitializeComponent();
        Title = AppLoc.T("menu_user");
        TbLblName.Text = AppLoc.T("user_name");
        TbLblPass.Text = passwordOnly ? AppLoc.T("user_new_pass") : AppLoc.T("login_password");
        BtnOk.Content = AppLoc.T("btn_ok");
        BtnCancel.Content = AppLoc.T("btn_cancel");

        if (name != null) TbName.Text = name;
        if (passwordOnly)
        {
            TbName.IsEnabled = false;
            TbLblName.Visibility = Visibility.Collapsed;
            TbName.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (TbName.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(TbName.Text))
        {
            MessageBox.Show(AppLoc.T("user_name"), AppLoc.T("app_title"));
            return;
        }
        UserName = TbName.Text.Trim();
        Password = PbPass.Password;
        DialogResult = true;
    }
}
