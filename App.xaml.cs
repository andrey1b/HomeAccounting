using System.Text;
using System.Windows;

namespace HomeAccounting;

public partial class App : Application
{
    private FileSystemWatcher? _watcher;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Пока показываем окно входа, открытых окон нет — иначе закрытие диалога
        // запустит автозавершение приложения (OnLastWindowClose) и главное окно не создастся.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try {

        // Инициализация БД и настроек до входа (нужна таблица users)
        Db.Init();
        var settings = AppSettings.Load();
        AppLoc.SetLang(settings.Lang);

        // Экран входа с выбором пользователя и паролем
        var login = new Views.LoginWindow();
        if (login.ShowDialog() != true) { Shutdown(); return; }
        Session.Set(login.SelectedUserId, login.SelectedUserName);

        var splash = new Views.SplashWindow();
        splash.Show();
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });

        StartWatcher(settings.XmlWatchFolder);

        splash.SetStatus("Запуск HTTP-сервера (порт 8772)…");
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
        ReceiptHttpReceiver.Start(Dispatcher, ImportFromBytes, result =>
        {
            if (MainWindow is MainWindow mw) mw.OnReceiptImported(result);
        });

        splash.SetStatus("Загрузка главного окна…");
        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { });
        var main = new MainWindow();
        MainWindow = main;
        // Главное окно создано — возвращаем обычное поведение завершения
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Минимальное время отображения заставки — 2 секунды
        var splashShown = System.Diagnostics.Stopwatch.StartNew();
        main.Show();
        var remaining = 2000 - (int)splashShown.ElapsedMilliseconds;
        if (remaining > 0) Thread.Sleep(remaining);
        splash.Close();

        // Проверка обновлений в фоне (не блокирует запуск)
        Services.UpdateChecker.CheckAsync(tag =>
            Dispatcher.Invoke(() =>
            {
                if (tag != null && MainWindow is MainWindow mw)
                    mw.ShowUpdateBanner(tag);
            }));

        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ha_crash.log"), ex.ToString()); } catch { }
            MessageBox.Show(ex.ToString(), "HomeAccounting — ошибка запуска",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReceiptHttpReceiver.Stop();
        _watcher?.Dispose();

        // Автоматическая резервная копия при выходе
        try
        {
            var s = AppSettings.Load();
            if (s.AutoBackupEnabled && !string.IsNullOrWhiteSpace(s.AutoBackupFolder))
                BackupService.RunAutoBackup(s.AutoBackupFolder, s.AutoBackupKeep);
        }
        catch { /* бэкап не должен мешать выходу */ }

        base.OnExit(e);
    }

    private void ImportFromBytes(byte[] raw)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ha_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
        try
        {
            File.WriteAllBytes(tmp, raw);
            var result = ReceiptImportService.Import(tmp, AppSettings.Load().DefaultAccountId, ConfirmDuplicate);
            if (MainWindow is MainWindow mw)
                mw.OnReceiptImported(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(AppLoc.T("msg_receipt_error") + "\n" + ex.Message,
                            "HomeAccounting", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>Спросить пользователя, вносить ли повторно уже импортированный чек.</summary>
    private bool ConfirmDuplicate(ParsedReceipt r) =>
        Dispatcher.Invoke(() =>
            MessageBox.Show(
                AppLoc.T("receipt_dup_confirm", "no", r.ReceiptNo, "date", r.Date.ToString("dd.MM.yyyy")),
                AppLoc.T("app_title"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);

    private void StartWatcher(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

        _watcher = new FileSystemWatcher(folder, "*.xml")
        {
            NotifyFilter       = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnXmlCreated;
    }

    private void OnXmlCreated(object sender, FileSystemEventArgs e)
    {
        // Wait until the downloader finishes writing the file
        if (!WaitForReadAccess(e.FullPath)) return;

        try
        {
            var settings = AppSettings.Load();
            var result   = ReceiptImportService.Import(e.FullPath, settings.DefaultAccountId, ConfirmDuplicate);

            Dispatcher.Invoke(() =>
            {
                if (MainWindow is MainWindow mw)
                    mw.OnReceiptImported(result);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
                MessageBox.Show(AppLoc.T("msg_receipt_error") + "\n" + ex.Message,
                                "HomeAccounting", MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }

    private static bool WaitForReadAccess(string path, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException) { Thread.Sleep(150); }
        }
        return false;
    }
}
