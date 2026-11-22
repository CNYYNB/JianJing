using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XinCleaner
{
    class InstalledApp
    {
        public string Name, Publisher, Version, InstallLocation, UninstallString, RegPath;
        public RegistryHive Hive;
        public RegistryView View;
        public long SizeKb;
        public string DisplayIcon;
        public DateTime? Installed;
        public long Bytes = -1; // -1 = still measuring, 0 = unknown
        public int Risk;        // 2 = don't, 1 = careful, 0 = fine
        public string RiskText;
    }

    static class AppRisk
    {
        static bool Has(string s, params string[] words) => words.Any(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));

        public static void Classify(InstalledApp a)
        {
            var n = a.Name ?? ""; var p = a.Publisher ?? ""; var np = n + " " + p;
            if (Has(n, "driver", "驱动", "chipset", "serial io", "management engine", "rapid storage", "bluetooth", "wi-fi", "wifi", "wireless", "wlan",
                    "audio", "realtek", "graphics", "dolby", " dts", "synaptics", "elan", "touchpad", "thunderbolt", "dynamic tuning", "bios", "firmware",
                    "microsoft edge", "webview2", "windows", "update health", "microsoft update", "dptf", "innovation platform", "nahimic"))
            { a.Risk = 2; a.RiskText = "别卸载 · 驱动/系统组件"; return; }
            if (Has(p, "intel", "advanced micro devices", "nvidia", "realtek", "qualcomm", "mediatek") && !Has(n, "driver & support assistant", "geforce experience"))
            { a.Risk = 2; a.RiskText = "别卸载 · 硬件厂商组件"; return; }
            if (Has(n, ".net", "asp.net", "visual c++", "redistributable", "runtime", "directx", "vulkan", "physx", "java", "openal", "xna",
                    "easyanticheat", "easy anti-cheat", "battleye", "anti-cheat", "framework", "sdk", "vcredist"))
            { a.Risk = 1; a.RiskText = "谨慎 · 其他软件/游戏要用"; return; }
            if (Has(n, "steam", "epic games launcher", "ubisoft connect", "ea app", "origin", "rockstar games launcher", "wegame", "battle.net"))
            { a.Risk = 1; a.RiskText = "谨慎 · 可能连带删掉游戏"; return; }
            if (Has(np, "机械革命", "mechrevo", "control center", "控制中心", "控制台", "armoury", "vantage", "omen gaming", "dragon center", "msi center",
                    "lenovo", "asus", "dell", "hp ", "acer", "huawei pc manager", "电脑管家"))
            { a.Risk = 1; a.RiskText = "谨慎 · 电脑厂商工具（风扇/键盘灯等）"; return; }
            a.Risk = 0; a.RiskText = "可以卸载";
        }

        static readonly string[] SharedDirs =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        static string ExeDir(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            string path;
            if (s.StartsWith("\"")) { var e = s.IndexOf('"', 1); path = e > 0 ? s.Substring(1, e - 1) : null; }
            else { var i = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase); path = i > 0 ? s.Substring(0, i + 4) : s.Split(',')[0]; }
            if (string.IsNullOrEmpty(path) || path.Contains("msiexec", StringComparison.OrdinalIgnoreCase)) return null;
            try { return Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(path)); } catch { return null; }
        }

        static bool Usable(string d)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return false;
            var f = d.TrimEnd('\\');
            if (f.Length <= 3) return false;
            if (SharedDirs.Any(x => !string.IsNullOrEmpty(x) && string.Equals(x.TrimEnd('\\'), f, StringComparison.OrdinalIgnoreCase))) return false;
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (f.StartsWith(win, StringComparison.OrdinalIgnoreCase)) return false;
            if (f.Contains("Package Cache", StringComparison.OrdinalIgnoreCase) || f.Contains("Installer", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static DateTime? FolderDate(InstalledApp a)
        {
            foreach (var d in new[] { a.InstallLocation, ExeDir(a.DisplayIcon), ExeDir(a.UninstallString) })
                if (Usable(d)) { try { return Directory.GetCreationTime(d).Date; } catch { } }
            return null;
        }

        public static long Measure(InstalledApp a)
        {
            foreach (var d in new[] { a.InstallLocation, ExeDir(a.DisplayIcon), ExeDir(a.UninstallString) })
            {
                if (!Usable(d)) continue;
                var b = Fs.DirSize(d);
                if (b > 0) return b;
            }
            return a.SizeKb > 0 ? a.SizeKb * 1024 : 0;
        }
    }

    class UninstallPage : UserControl, IPage
    {
        readonly TextBlock _big, _sub;
        readonly StackPanel _list = new StackPanel();
        readonly TextBox _search;
        readonly ComboBox _sort;
        List<InstalledApp> _apps = new();
        bool _loaded, _busy;

        public UninstallPage()
        {
            var header = new StackPanel();
            header.Children.Add(Ui.Header("软件管理", "正在读取…", out _big, out _sub));
            _sub.Text = "卸载完会再找一遍残留的文件夹和注册表。";
            _search = new TextBox { FontSize = 14, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 16, 0, 0), BorderBrush = Ui.Br("#DDDDDD"), BorderThickness = new Thickness(1) };
            _search.TextChanged += (s, e) => Render();
            var hint = Ui.Text("搜索软件", 14, Ui.Br("#AAAAAA"));
            hint.IsHitTestVisible = false; hint.Margin = new Thickness(14, 16, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center;
            _search.TextChanged += (s, e) => hint.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;
            var sg = new Grid(); sg.Children.Add(_search); sg.Children.Add(hint);
            _sort = new ComboBox { Width = 150, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(12, 16, 0, 0), VerticalAlignment = VerticalAlignment.Stretch };
            foreach (var o in new[] { "按占用空间", "按安装时间", "按名称" }) _sort.Items.Add(o);
            _sort.SelectedIndex = 0;
            _sort.SelectionChanged += (s, e) => Render();
            var bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_sort, 1);
            bar.Children.Add(sg); bar.Children.Add(_sort);
            header.Children.Add(bar);
            Content = Ui.Frame(header, _list, null);
        }

        public async void OnShown()
        {
            if (_loaded) return;
            _loaded = true;
            await Reload();
        }

        async Task Reload()
        {
            _apps = await Task.Run(ReadApps);
            foreach (var a in _apps) AppRisk.Classify(a);
            _big.Text = $"{_apps.Count} 个软件";
            Render();
            var apps = _apps;
            _sub.Text = "正在统计每个软件占用的空间…";
            await Task.Run(() => Parallel.ForEach(apps, new ParallelOptions { MaxDegreeOfParallelism = 4 }, a =>
            {
                a.Bytes = AppRisk.Measure(a);
                if (a.Installed == null) a.Installed = AppRisk.FolderDate(a);
            }));
            if (apps != _apps) return;
            long total = apps.Sum(a => Math.Max(0, a.Bytes));
            _sub.Text = $"共占用 {Fmt.Size(total)}。标红的是驱动和系统组件，卸了电脑可能出问题。";
            Render();
        }

        void Render()
        {
            _list.Children.Clear();
            var q = _search.Text?.Trim() ?? "";
            var filtered = _apps.Where(a => q == "" || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (a.Publisher ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
            var shown = (_sort?.SelectedIndex ?? 0) switch
            {
                0 => filtered.OrderByDescending(a => a.Bytes).ThenBy(a => a.Name, StringComparer.CurrentCulture),
                1 => filtered.OrderByDescending(a => a.Installed ?? DateTime.MinValue).ThenBy(a => a.Name, StringComparer.CurrentCulture),
                _ => filtered.OrderBy(a => a.Name, StringComparer.CurrentCulture),
            };
            _list.Children.Add(Ui.Divider());
            foreach (var a in shown)
            {
                var size = a.Bytes < 0 ? "统计中…" : a.Bytes == 0 ? "大小未知" : Fmt.Size(a.Bytes);
                var when = a.Installed?.ToString("yyyy-MM-dd") is string w ? w + " 安装" : null;
                var desc = string.Join(" · ", new[] { size, when, a.Publisher, a.Version }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var btn = Ui.Ghost("卸载");
                var app = a;
                btn.Click += async (s, e) => await Uninstall(app);
                var tag = Ui.Text(a.RiskText, 13, a.Risk == 2 ? Ui.Red : a.Risk == 1 ? Ui.Amber : Ui.Gray);
                tag.Margin = new Thickness(0, 0, 14, 0);
                tag.TextWrapping = TextWrapping.NoWrap;
                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(tag); right.Children.Add(btn);
                _list.Children.Add(Ui.Row(null, a.Name, desc, right));
                _list.Children.Add(Ui.Divider());
            }
        }

        async Task Uninstall(InstalledApp a)
        {
            if (_busy) return;
            if (a.Risk == 2 && !Ui.Confirm($"「{a.Name}」是驱动或系统组件，卸载后可能出现没声音、没网、屏幕或触控板异常等问题。\n\n确定还要卸载吗？")) return;
            if (a.Risk == 1 && !Ui.Confirm($"注意：{a.RiskText}。\n\n确定要卸载「{a.Name}」吗？")) return;
            if (!Ui.Confirm($"卸载「{a.Name}」？\n会先打开它自己的卸载程序。")) return;
            _busy = true;
            _sub.Text = $"正在卸载 {a.Name}，请在弹出的窗口里完成…";
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", "/c \"" + a.UninstallString + "\"") { UseShellExecute = false, CreateNoWindow = true };
                    using var p = Process.Start(psi);
                    p.WaitForExit();
                }
                catch (Exception ex) { Store.Log("uninstall: " + ex.Message); }
            });
            // give child installer processes a moment
            await Task.Delay(1500);
            bool stillThere = await Task.Run(() => RegExists(a));
            if (stillThere && !Ui.Confirm("卸载程序看起来还没完成。\n等它结束后点「确定」查找残留，或点「取消」跳过。"))
            {
                _busy = false; _sub.Text = ""; await Reload(); return;
            }
            var leftovers = await Task.Run(() => FindLeftovers(a));
            _busy = false;
            if (leftovers.Count > 0)
            {
                var msg = "找到这些残留：\n\n" + string.Join("\n", leftovers.Take(15)) + (leftovers.Count > 15 ? $"\n…共 {leftovers.Count} 项" : "") + "\n\n删除吗？会先放进恢复中心，30 天内能放回。";
                if (Ui.Confirm(msg)) RemoveLeftovers(leftovers, a.Name);
                _sub.Text = $"{a.Name} 已卸载，残留已处理。";
            }
            else _sub.Text = $"{a.Name} 已卸载，没有发现残留。";
            await Reload();
        }

        static bool RegExists(InstalledApp a)
        {
            try
            {
                using var bk = RegistryKey.OpenBaseKey(a.Hive, a.View);
                using var k = bk.OpenSubKey(a.RegPath);
                return k != null;
            }
            catch { return false; }
        }

        static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (DateTime.TryParseExact(s, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d)) return d;
            if (DateTime.TryParse(s, out d)) return d;
            return null;
        }

        static List<InstalledApp> ReadApps()
        {
            var list = new List<InstalledApp>();
            const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Default) })
            {
                try
                {
                    using var bk = RegistryKey.OpenBaseKey(hive, view);
                    using var root = bk.OpenSubKey(path);
                    if (root == null) continue;
                    foreach (var sub in root.GetSubKeyNames())
                    {
                        try
                        {
                            using var k = root.OpenSubKey(sub);
                            var name = k?.GetValue("DisplayName") as string;
                            var un = k?.GetValue("UninstallString") as string;
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(un)) continue;
                            if (Convert.ToInt32(k.GetValue("SystemComponent", 0)) == 1) continue;
                            if (k.GetValue("ParentKeyName") != null) continue;
                            if (k.GetValue("ReleaseType") is string rt && (rt.Contains("Update") || rt.Contains("Hotfix"))) continue;
                            list.Add(new InstalledApp
                            {
                                Name = name.Trim(),
                                Publisher = (k.GetValue("Publisher") as string)?.Trim(),
                                Version = k.GetValue("DisplayVersion") as string,
                                InstallLocation = (k.GetValue("InstallLocation") as string)?.Trim().Trim('"'),
                                UninstallString = un.Trim(),
                                SizeKb = Convert.ToInt64(k.GetValue("EstimatedSize", 0)),
                                DisplayIcon = k.GetValue("DisplayIcon") as string,
                                Installed = ParseDate(k.GetValue("InstallDate") as string),
                                Hive = hive, View = view, RegPath = path + "\\" + sub,
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return list.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.CurrentCulture).ToList();
        }

        static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft", "Windows", "Common Files", "Microsoft Shared", "Intel", "AMD", "NVIDIA", "NVIDIA Corporation",
            "Google", "Mozilla", "Packages", "Temp", "Programs", "Adobe", "Apple", "Realtek", "WindowsApps", "dotnet",
        };

        static string Clean(string name)
        {
            var s = Regex.Replace(name ?? "", @"\(.*?\)|（.*?）", "");
            s = Regex.Replace(s, @"\b(v?\d+(\.\d+)+|x64|x86|64-bit|32-bit)\b", "", RegexOptions.IgnoreCase);
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        static List<string> FindLeftovers(InstalledApp a)
        {
            var found = new List<string>();
            var name = Clean(a.Name);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name, name.Replace(" ", "") };
            names.RemoveWhere(n => n.Length < 3 || Protected.Contains(n));
            if (names.Count == 0) return found;

            if (!string.IsNullOrEmpty(a.InstallLocation) && Directory.Exists(a.InstallLocation) && Path.GetPathRoot(a.InstallLocation) != a.InstallLocation
                && !Protected.Contains(Path.GetFileName(a.InstallLocation.TrimEnd('\\'))))
                found.Add(a.InstallLocation);

            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Paths.Roaming, Paths.Local,
                Path.Combine(Paths.Local, "Programs"),
            }.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase);

            var pub = a.Publisher?.Trim();
            foreach (var r in roots)
            {
                foreach (var n in names)
                {
                    var d = Path.Combine(r, n);
                    if (Directory.Exists(d)) found.Add(d);
                    if (!string.IsNullOrEmpty(pub) && pub.Length >= 3)
                    {
                        var d2 = Path.Combine(r, pub, n);
                        if (Directory.Exists(d2)) found.Add(d2);
                    }
                }
            }

            foreach (var n in names)
            {
                foreach (var key in new[] { $@"Software\{n}", string.IsNullOrEmpty(pub) ? null : $@"Software\{pub}\{n}" })
                {
                    if (key == null) continue;
                    try { using var k = Registry.CurrentUser.OpenSubKey(key); if (k != null) found.Add(@"HKCU\" + key); } catch { }
                }
            }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        static void RemoveLeftovers(List<string> items, string _lastName)
        {
            History.MoveToVault("卸载残留", _lastName ?? "卸载残留", items);
        }
    }
}
