using System.Windows;

namespace XinCleaner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Contains("--auto-clean"))
            {
                try { AutoClean.Run(); } catch (Exception ex) { Store.Log("auto-clean failed: " + ex.Message); }
                Shutdown();
                return;
            }
            if (Installer.HandOffIfNotAdmin(e.Args)) { Shutdown(); return; }
            if (e.Args.Contains("--uninstall"))
            {
                Installer.Uninstall();
                Shutdown();
                return;
            }
            if (Installer.EnsureInstalled()) { Shutdown(); return; }
            try { Installer.EnsureLaunchTask(); } catch { }
            DispatcherUnhandledException += (s, ex) =>
            {
                Store.Log("crash: " + ex.Exception);
                MessageBox.Show("出了点问题：" + ex.Exception.Message, "简境");
                ex.Handled = true;
            };
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
            w.Activate(); w.Topmost = true; w.Topmost = false;
            if (e.Args.Contains("--updated")) Ui.Info($"已更新到 {AppInfo.Name}。");
            Updater.BackgroundCheck();
            Task.Run(() => { try { History.Purge(); } catch { } });
        }
    }
}
