using System.Windows;

namespace HomeAccounting.Views;

public partial class BackupSettingsWindow : Window
{
    public BackupSettingsWindow()
    {
        InitializeComponent();
        Title = AppLoc.T("backup_title");
        ChkEnable.Content = AppLoc.T("backup_enable");
        TbLblFolder.Text  = AppLoc.T("backup_folder");
        BtnBrowse.Content  = AppLoc.T("backup_browse");
        TbLblKeep.Text     = AppLoc.T("backup_keep");
        BtnBackupNow.Content = AppLoc.T("backup_now");
        BtnOk.Content      = AppLoc.T("btn_ok");
        BtnCancel.Content  = AppLoc.T("btn_cancel");

        var s = AppSettings.Load();
        ChkEnable.IsChecked = s.AutoBackupEnabled;
        TbFolder.Text = s.AutoBackupFolder;
        TbKeep.Text   = s.AutoBackupKeep.ToString();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(TbFolder.Text) && Directory.Exists(TbFolder.Text))
            dlg.SelectedPath = TbFolder.Text;
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TbFolder.Text = dlg.SelectedPath;
    }

    private int Keep()
    {
        if (!int.TryParse(TbKeep.Text.Trim(), out var k) || k < 1) k = 10;
        return k;
    }

    private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbFolder.Text))
        {
            MessageBox.Show(AppLoc.T("backup_need_folder"), AppLoc.T("backup_title"));
            return;
        }
        try
        {
            var path = BackupService.RunAutoBackup(TbFolder.Text.Trim(), Keep());
            MessageBox.Show(AppLoc.T("backup_saved_now", "path", path), AppLoc.T("backup_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppLoc.T("backup_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (ChkEnable.IsChecked == true && string.IsNullOrWhiteSpace(TbFolder.Text))
        {
            MessageBox.Show(AppLoc.T("backup_need_folder"), AppLoc.T("backup_title"));
            return;
        }
        var s = AppSettings.Load();
        s.AutoBackupEnabled = ChkEnable.IsChecked == true;
        s.AutoBackupFolder  = TbFolder.Text.Trim();
        s.AutoBackupKeep    = Keep();
        s.Save();
        DialogResult = true;
    }
}
