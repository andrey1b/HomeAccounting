using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HomeAccounting.Views;

public partial class LoginWindow : Window
{
    public int    SelectedUserId   { get; private set; }
    public string SelectedUserName { get; private set; } = "";

    public LoginWindow()
    {
        InitializeComponent();
        ApplyLoc();

        var users = UserService.GetAll();
        CbUser.ItemsSource = users;
        var def = users.FirstOrDefault(u => u.IsDefault) ?? users.FirstOrDefault();
        if (def != null) CbUser.SelectedItem = users.First(u => u.Id == def.Id);

        UpdatePasswordState();
        Loaded += (_, _) => { if (PbPassword.IsEnabled) PbPassword.Focus(); else BtnOk.Focus(); };
    }

    private void ApplyLoc()
    {
        Title              = AppLoc.T("login_title");
        TbTitle.Text       = AppLoc.T("login_header");
        TbLblUser.Text     = AppLoc.T("login_user");
        TbLblPassword.Text = AppLoc.T("login_password");
        BtnOk.Content      = AppLoc.T("login_enter");
        BtnCancel.Content  = AppLoc.T("login_exit");
    }

    private void CbUser_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePasswordState();
        TbError.Visibility = Visibility.Collapsed;
    }

    private void UpdatePasswordState()
    {
        var u = CbUser.SelectedItem as User;
        bool needs = u?.HasPassword == true;
        PbPassword.IsEnabled = needs;
        PbPassword.Password  = "";
        if (needs) PbPassword.Focus();
    }

    private void PbPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void TryLogin()
    {
        if (CbUser.SelectedItem is not User u) return;
        if (UserService.Authenticate(u.Id, PbPassword.Password))
        {
            SelectedUserId   = u.Id;
            SelectedUserName = u.Name;
            DialogResult = true;
        }
        else
        {
            TbError.Text = AppLoc.T("login_wrong");
            TbError.Visibility = Visibility.Visible;
            PbPassword.SelectAll();
            PbPassword.Focus();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
