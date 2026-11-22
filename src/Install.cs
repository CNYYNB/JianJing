using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XinCleaner
{
    static class AppInfo
    {
        public const int Code = 8;
        public const string Name = "2.2";
    }

    // First run from the zip copies the exe to %LocalAppData%\XinCleaner and puts shortcuts on the desktop and Start menu.
    static class Installer
    {
        const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\XinCleaner";
        public static string InstalledExe => Path.Combine(Paths.AppDir, "XinCleaner.exe");
        static string DesktopLnk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "简境.lnk");
        static string StartLnk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "简境.lnk");

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool DeleteFileW(string path);

        static string Q(string s) => "'" + s.Replace("'", "''") + "'";

        // returns true when the caller should exit because the installed copy was started
        public static bool EnsureInstalled()
        {
            var me = Environment.ProcessPath;
            if (string.Equals(Path.GetFullPath(me), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
            {
                if (Store.Get("installed") != AppInfo.Name) { Shortcuts(); Register(); Store.Set("installed", AppInfo.Name); }
                return false;
            }
            try
            {
                foreach (var p in Process.GetProcessesByName("XinCleaner"))
                    if (p.Id != Environment.ProcessId) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
                File.Copy(me, InstalledExe, true);
                DeleteFileW(InstalledExe + ":Zone.Identifier"); // so the desktop shortcut doesn't trip SmartScreen again
            }
            catch (Exception e) { Store.Log("install copy failed: " + e.Message); return false; }
            Shortcuts();
            Register();
            Store.Set("installed", AppInfo.Name);
            try { Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = false, WorkingDirectory = Paths.AppDir }); return true; }
            catch (Exception e) { Store.Log("install start failed: " + e.Message); return false; }
        }

        public const string LaunchTask = "XinCleaner Launch";

        public static bool IsAdmin()
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        // Not elevated: start through the elevated launch task (no UAC prompt), or ask for admin once. Returns true = exit now.
        public static bool HandOffIfNotAdmin(string[] args)
        {
            if (IsAdmin()) return false;
            bool installed = string.Equals(Path.GetFullPath(Environment.ProcessPath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);
            if (installed && args.Length == 0 && Proc.Run("schtasks.exe", $"/Query /TN \"{LaunchTask}\"").code == 0
                && Proc.Run("schtasks.exe", $"/Run /TN \"{LaunchTask}\"").code == 0)
                return true;
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath, string.Join(" ", args.Select(a => "\"" + a + "\"")))
                { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(Environment.ProcessPath) });
            }
            catch { } // user said no
            return true;
        }

        // elevated task with no trigger; the app runs it to start itself as admin without a UAC prompt
        public static void EnsureLaunchTask()
        {
            if (Store.Get("launch-task") == InstalledExe && Proc.Run("schtasks.exe", $"/Query /TN \"{LaunchTask}\"").code == 0) return;
            var ps = $"$a=New-ScheduledTaskAction -Execute {Q(InstalledExe)} -WorkingDirectory {Q(Paths.AppDir)};" +
                     "$p=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Highest;" +
                     "$s=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances Parallel;" +
                     $"Register-ScheduledTask -TaskName {Q(LaunchTask)} -Action $a -Principal $p -Settings $s -Force | Out-Null;'ok'";
            if (Proc.PowerShell(ps) == "ok") Store.Set("launch-task", InstalledExe);
        }

        public static void Shortcuts()
        {
            var exe = InstalledExe;
            string one(string lnk) =>
                $"$l=$s.CreateShortcut({Q(lnk)});$l.TargetPath={Q(exe)};$l.WorkingDirectory={Q(Paths.AppDir)};$l.IconLocation={Q(exe + ",0")};$l.Description='简境';$l.Save();";
            foreach (var old in new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "信·清理.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "信·清理.lnk") })
                try { if (File.Exists(old)) File.Delete(old); } catch { }
            Proc.PowerShell("$s=New-Object -ComObject WScript.Shell;" + one(DesktopLnk) + one(StartLnk));
            try { Process.Start(new ProcessStartInfo("ie4uinit.exe", "-show") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
        }

        static void Register()
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(UninstKey);
                k.SetValue("DisplayName", "简境");
                k.SetValue("DisplayVersion", AppInfo.Name);
                k.SetValue("Publisher", "简境");
                k.SetValue("DisplayIcon", InstalledExe + ",0");
                k.SetValue("InstallLocation", Paths.AppDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 66000, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void Uninstall()
        {
            if (!Ui.Confirm("确定要卸载简境吗？")) return;
            Proc.Run("schtasks.exe", "/Delete /TN \"XinCleaner AutoClean\" /F");
            Proc.Run("schtasks.exe", $"/Delete /TN \"{LaunchTask}\" /F");
            try { File.Delete(DesktopLnk); } catch { }
            try { File.Delete(StartLnk); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
            var script = $"Wait-Process -Id {Environment.ProcessId} -Timeout 30 -EA SilentlyContinue;Start-Sleep -Milliseconds 500;Remove-Item -LiteralPath {Q(Paths.AppDir)} -Recurse -Force -EA SilentlyContinue";
            Updater.Detached(script);
            Ui.Info("已卸载。");
        }
    }
}
