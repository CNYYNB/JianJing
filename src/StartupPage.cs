using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace XinCleaner
{
    class StartupEntry
    {
        public StartupItem Item;
        public string Name, Exe, Publisher;
        public bool Enabled, Legacy, Keep;
        public int Impact; // 0 无 1 低 2 中 3 高
        public string ImpactText => Impact switch { 3 => "高", 2 => "中", 1 => "低", _ => "无" };
    }

    class StartupPage : UserControl, IPage
    {
        readonly StackPanel _list = new StackPanel();
        readonly TextBlock _nTotal, _nOn, _nOff, _nKeep;
        bool _busy;

        public StartupPage()
        {
            var head = new StackPanel();
            head.Children.Add(Ui.Title("启动管理"));
            head.Children.Add(Ui.Text("关掉用不着的开机启动，开机更快。只是禁用，不删除，随时能开回来。", 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 4, 0, 0)));
            var stats = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            for (int i = 0; i < 4; i++) stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stats.Children.Add(Stat("开机启动项", "rocket", Ui.TBlue, "开机时会运行的程序", out _nTotal, 0));
            stats.Children.Add(Stat("已启用", "power", Ui.TGreen, "正在开机启动", out _nOn, 1));
            stats.Children.Add(Stat("已禁用", "lock", Ui.TGray, "不会开机启动", out _nOff, 2));
            stats.Children.Add(Stat("建议保留", "shield", Ui.TOrange, "驱动、安全、输入法", out _nKeep, 3));
            head.Children.Add(stats);

            var body = new StackPanel();
            body.Children.Add(Ui.Card(_list, new Thickness(18, 4, 18, 6)));
            body.Children.Add(Ui.SafeNote("驱动、杀毒软件和输入法会标「建议保留」，关掉前会再提醒你。影响大小按程序体积估算。").Also(n => n.Margin = new Thickness(0, 14, 0, 0)));
            Content = Ui.Frame(head, body, null, card: false);
        }

        Border Stat(string name, string icon, (string, string) pal, string sub, out TextBlock val, int col)
        {
            var g = new Grid();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(Ui.Tile(icon, pal, 30));
            top.Children.Add(Ui.Text(name, 13, Ui.Ink, FontWeights.Medium).Also(t => t.Margin = new Thickness(10, 0, 0, 0)));
            var sp = new StackPanel();
            sp.Children.Add(top);
            var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            val = Ui.Text("—", 22, Ui.Br(pal.Item2), FontWeights.SemiBold);
            row.Children.Add(val);
            var s = Ui.Text(sub, 11, Ui.Gray); s.HorizontalAlignment = HorizontalAlignment.Right; s.VerticalAlignment = VerticalAlignment.Bottom; s.Margin = new Thickness(0, 0, 0, 4);
            row.Children.Add(s);
            sp.Children.Add(row);
            var c = Ui.Card(sp, new Thickness(14, 12, 14, 12));
            c.Margin = new Thickness(col == 0 ? 0 : 6, 0, col == 3 ? 0 : 6, 0);
            Grid.SetColumn(c, col);
            return c;
        }

        public async void OnShown() => await Load();

        async Task Load()
        {
            if (_busy) return;
            _busy = true;
            _list.Children.Clear();
            _list.Children.Add(Ui.Text("正在读取…", 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 14, 0, 14)));
            var items = await Task.Run(ReadAll);
            _busy = false;
            _nTotal.Text = items.Count.ToString();
            _nOn.Text = items.Count(i => i.Enabled).ToString();
            _nOff.Text = items.Count(i => !i.Enabled).ToString();
            _nKeep.Text = items.Count(i => i.Keep).ToString();
            Render(items);
        }

        void Render(List<StartupEntry> items)
        {
            _list.Children.Clear();
            var head = Ui.Cols((Ui.Text("名称", 12.5, Ui.Gray, FontWeights.Medium), Ui.Star(2.2)), (Ui.Text("发布者", 12.5, Ui.Gray, FontWeights.Medium), Ui.Star(1.4)),
                               (Ui.Text("预估影响", 12.5, Ui.Gray, FontWeights.Medium), Ui.Px(96)), (Ui.Text("状态", 12.5, Ui.Gray, FontWeights.Medium), Ui.Px(80)),
                               (Ui.Text("开机启动", 12.5, Ui.Gray, FontWeights.Medium).Also(t => t.TextAlignment = TextAlignment.Right), Ui.Px(72)));
            head.Margin = new Thickness(0, 12, 0, 8);
            _list.Children.Add(head);
            if (items.Count == 0) { _list.Children.Add(Ui.Text("没有开机启动的程序", 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 10, 0, 14))); return; }
            foreach (var e in items.OrderByDescending(i => i.Enabled).ThenByDescending(i => i.Impact).ThenBy(i => i.Name, StringComparer.CurrentCulture))
            {
                _list.Children.Add(Ui.Divider());
                var nameSp = new StackPanel { Orientation = Orientation.Horizontal };
                nameSp.Children.Add(AppIcon(e.Exe, e.Name));
                var nt = Ui.Text(e.Name, 14, e.Enabled ? Ui.Ink : Ui.Gray, FontWeights.Medium); nt.Margin = new Thickness(12, 0, 8, 0);
                nt.TextTrimming = TextTrimming.CharacterEllipsis; nt.TextWrapping = TextWrapping.NoWrap; nt.ToolTip = e.Item.Command;
                nameSp.Children.Add(nt);
                if (e.Keep) nameSp.Children.Add(Ui.Chip("建议保留", Ui.Accent, "#E6F0FB"));
                var pub = Ui.Text(string.IsNullOrEmpty(e.Publisher) ? "未知" : e.Publisher, 13, Ui.Gray);
                pub.TextTrimming = TextTrimming.CharacterEllipsis; pub.TextWrapping = TextWrapping.NoWrap;
                var (fg, bg) = e.Impact switch { 3 => (Ui.Red, "#FDE7E9"), 2 => (Ui.Amber, "#FFF4CE"), 1 => (Ui.Green, "#DFF6DD"), _ => (Ui.Gray, "#F0F1F3") };
                var chip = Ui.Chip(e.ImpactText, fg, bg); chip.HorizontalAlignment = HorizontalAlignment.Left;
                var state = Ui.Text(e.Enabled ? "已启用" : "已禁用", 13, e.Enabled ? Ui.Ink : Ui.Gray);
                var tg = Ui.Toggle(e.Enabled); tg.HorizontalAlignment = HorizontalAlignment.Right;
                var entry = e;
                tg.Click += (s, ev) =>
                {
                    bool on = tg.IsChecked == true;
                    if (!on && entry.Keep && !Ui.Confirm($"「{entry.Name}」是驱动、安全软件或输入法，关掉可能导致声音、触控板、杀毒或打字出问题。\n\n确定要禁止它开机启动吗？")) { tg.IsChecked = true; return; }
                    if (!SetEnabled(entry, on)) { tg.IsChecked = !on; Ui.Info("没能修改这一项，可能被其他软件锁住了。"); return; }
                    _ = Load();
                };
                var row = Ui.Cols((nameSp, Ui.Star(2.2)), (pub, Ui.Star(1.4)), (chip, Ui.Px(96)), (state, Ui.Px(80)), (tg, Ui.Px(72)));
                row.MinHeight = 54;
                foreach (UIElement c in row.Children) if (c is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
                _list.Children.Add(row);
            }
        }

        static readonly Dictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint n);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

        static FrameworkElement AppIcon(string exe, string name)
        {
            ImageSource src = null;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                if (!IconCache.TryGetValue(exe, out src))
                {
                    try
                    {
                        var large = new IntPtr[1];
                        if (ExtractIconEx(exe, 0, large, null, 1) > 0 && large[0] != IntPtr.Zero)
                        {
                            src = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            src.Freeze();
                            DestroyIcon(large[0]);
                        }
                    }
                    catch { }
                    IconCache[exe] = src;
                }
            }
            if (src != null) return new Image { Source = src, Width = 26, Height = 26, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Background = Ui.Br("#E9EDF2"), VerticalAlignment = VerticalAlignment.Center };
            var t = Ui.Text(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), 12, Ui.Gray, FontWeights.SemiBold);
            t.HorizontalAlignment = HorizontalAlignment.Center; b.Child = t;
            return b;
        }

        // ---------- data ----------
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        static (RegistryKey hive, string sub, string value) ApprovedLoc(StartupItem it)
        {
            if (it.Where == "HKCU") return (Registry.CurrentUser, Approved + "Run", it.Name);
            if (it.Where == "HKLM") return (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64), Approved + "Run", it.Name);
            if (it.Where == "HKLM32") return (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64), Approved + "Run32", it.Name);
            var folder = it.Where.Substring("Folder:".Length);
            bool common = string.Equals(folder, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StringComparison.OrdinalIgnoreCase);
            return (common ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64) : Registry.CurrentUser, Approved + "StartupFolder", Path.GetFileName(it.Command));
        }

        static bool IsEnabled(StartupItem it)
        {
            try
            {
                var (h, sub, v) = ApprovedLoc(it);
                using var k = h.OpenSubKey(sub);
                if (k?.GetValue(v) is byte[] b && b.Length > 0) return (b[0] & 1) == 0;
            }
            catch { }
            return true;
        }

        static bool SetEnabled(StartupEntry e, bool on)
        {
            try
            {
                if (e.Legacy)
                {
                    if (on) ProtectPage.RestoreStartup(e.Item);
                    return true;
                }
                var (h, sub, v) = ApprovedLoc(e.Item);
                using var k = h.CreateSubKey(sub, true);
                var data = new byte[12];
                data[0] = on ? (byte)2 : (byte)3;
                if (!on) BitConverter.GetBytes(DateTime.Now.ToFileTimeUtc()).CopyTo(data, 4);
                k.SetValue(v, data, RegistryValueKind.Binary);
                return true;
            }
            catch (Exception ex) { Store.Log("startup toggle failed: " + ex.Message); return false; }
        }

        static readonly string[] KeepWords = { "realtek", "intel", "nvidia", "amd", "advanced micro", "synaptics", "elan", "dolby", "waves", "conexant", "logitech", "lenovo", "asus", "dell", "hp inc", "mechrevo", "机械革命",
            "huorong", "火绒", "360", "qihoo", "kaspersky", "eset", "avast", "avg", "norton", "mcafee", "bitdefender", "securityhealth", "windows defender", "windows security",
            "sogou", "搜狗", "输入法", "ime", "rime", "pinyin", "拼音" };

        public static List<StartupEntry> ReadAll()
        {
            var list = new List<StartupEntry>();
            foreach (var it in ProtectPage.ReadStartup())
            {
                var e = new StartupEntry { Item = it, Name = it.Name, Enabled = IsEnabled(it) };
                Fill(e);
                list.Add(e);
            }
            foreach (var it in ProtectPage.LoadDisabled())
            {
                if (list.Any(x => x.Item.Id == it.Id)) continue;
                var e = new StartupEntry { Item = it, Name = it.Name, Enabled = false, Legacy = true };
                Fill(e);
                list.Add(e);
            }
            return list;
        }

        static void Fill(StartupEntry e)
        {
            e.Exe = ExePath(e.Item.Command);
            try
            {
                if (e.Exe != null && File.Exists(e.Exe))
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(e.Exe);
                    e.Publisher = vi.CompanyName?.Trim();
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription) && vi.FileDescription.Length <= 30 && Fs.LooksBetterName(e.Name, vi.FileDescription)) e.Name = vi.FileDescription.Trim();
                    long dir = 0;
                    try { foreach (var f in new DirectoryInfo(Path.GetDirectoryName(e.Exe)).EnumerateFiles()) dir += f.Length; } catch { }
                    e.Impact = dir >= 150L << 20 ? 3 : dir >= 30L << 20 ? 2 : 1;
                }
            }
            catch { }
            var hay = ((e.Publisher ?? "") + " " + e.Name + " " + e.Exe).ToLowerInvariant();
            e.Keep = KeepWords.Any(w => hay.Contains(w)) && !hay.Contains("onedrive") && !hay.Contains("teams");
        }

        static string ExePath(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            cmd = Environment.ExpandEnvironmentVariables(cmd.Trim());
            if (cmd.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var t = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic sh = Activator.CreateInstance(t);
                    var target = (string)sh.CreateShortcut(cmd).TargetPath;
                    return string.IsNullOrEmpty(target) ? null : target;
                }
                catch { return null; }
            }
            if (cmd.StartsWith("\""))
            {
                var end = cmd.IndexOf('"', 1);
                return end > 1 ? cmd.Substring(1, end - 1) : null;
            }
            var i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return i > 0 ? cmd.Substring(0, i + 4) : cmd.Split(' ')[0];
        }
    }
}
