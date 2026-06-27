using System.Windows;

namespace HomeAccounting.Views;

public partial class UsersWindow : Window
{
    public UsersWindow()
    {
        InitializeComponent();
        ApplyLoc();
        Refresh();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T("users_title");
        BtnAdd.Content = AppLoc.T("btn_add");
        BtnRename.Content = AppLoc.T("btn_edit");
        BtnPass.Content = AppLoc.T("user_set_pass");
        BtnDefault.Content = AppLoc.T("user_set_default");
        BtnDel.Content = AppLoc.T("btn_delete");
        ColName.Header = AppLoc.T("user_name");
        ColPass.Header = AppLoc.T("user_haspass");
        ColDef.Header = AppLoc.T("user_default");
    }

    private void Refresh() => Dg.ItemsSource = UserService.GetAll();

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new UserDialog { Owner = this };
        if (dlg.ShowDialog() == true) { UserService.Add(dlg.UserName, dlg.Password); Refresh(); }
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not User sel) return;
        var dlg = new UserDialog(sel.Name) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            UserService.Rename(sel.Id, dlg.UserName);
            if (!string.IsNullOrEmpty(dlg.Password)) UserService.ChangePassword(sel.Id, dlg.Password);
            Refresh();
        }
    }

    private void BtnPass_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not User sel) return;
        var dlg = new UserDialog(sel.Name, passwordOnly: true) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            UserService.ChangePassword(sel.Id, dlg.Password);
            Refresh();
        }
    }

    private void BtnDefault_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not User sel) return;
        UserService.SetDefault(sel.Id);
        Refresh();
    }

    private void BtnDel_Click(object sender, RoutedEventArgs e)
    {
        if (Dg.SelectedItem is not User sel) return;
        var all = UserService.GetAll();
        if (all.Count <= 1) { MessageBox.Show(AppLoc.T("users_title")); return; }
        if (sel.Id == Session.UserId) { MessageBox.Show(AppLoc.T("users_title")); return; }
        if (MessageBox.Show(AppLoc.T("msg_confirm_del"), AppLoc.T("msg_confirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        UserService.Delete(sel.Id);
        Refresh();
    }
}
