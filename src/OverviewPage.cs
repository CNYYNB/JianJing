using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace XinCleaner
{
    class OverviewPage : UserControl, IPage
    {
        readonly ContentControl _ring = new ContentControl();
        readonly TextBlock _state, _stateSub, _info, _cpu, _cpuSub, _mem, _memSub, _st, _stSub, _last, _health;
        readonly ContentControl _chip = new ContentControl();
        readonly Grid _cpuBar, _memBar, _stBar;
        readonly StackPanel _disks = new StackPanel();
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        long _junk = -1; int _startupOn = -1; string _smart; bool _loaded;

        public OverviewPage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Title("概览"));

            // hero
            var hero = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _ring.Content = W.Ring(0.001, 104, Ui.GreenSoft, "—", "健康分");
            _ring.Margin = new Thickness(4, 0, 26, 0);
            hero.Children.Add(_ring);
            var ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _state = Ui.Text("正在检查…", 21, Ui.Ink, FontWeights.SemiBold);
            _stateSub = Ui.Text("", 13, Ui.Gray); _stateSub.Margin = new Thickness(0, 4, 0, 10);
            ht.Children.Add(_state); ht.Children.Add(_stateSub); ht.Children.Add(_chip);
            _chip.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(ht, 1); hero.Children.Add(ht);
            var scan = Ui.IconButton("scan", "立即扫描", true, (s, e) => MainWindow.Go("clean"));
            scan.MinWidth = 130; scan.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(scan, 2); hero.Children.Add(scan);
            sp.Children.Add(Ui.Card(hero, new Thickness(18, 18, 22, 18)).Also(c => c.Margin = new Thickness(0, 16, 0, 0)));

            _info = Ui.Text("", 12, Ui.Gray);
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 7, 14, 8),
                Margin = new Thickness(0, 12, 0, 0), Child = _info,
            });

            // metric cards
            var cards = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            for (int i = 0; i < 3; i++) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cards.Children.Add(Metric("CPU", "cpu", Ui.TBlue, out _cpu, out _cpuSub, out _cpuBar, 0));
            cards.Children.Add(Metric("内存", "mem", Ui.TPurple, out _mem, out _memSub, out _memBar, 1));
            cards.Children.Add(Metric("开机启动", "rocket", Ui.TOrange, out _st, out _stSub, out _stBar, 2));
            sp.Children.Add(cards);

            // disks
            var dk = new StackPanel();
            var dh = new Grid();
            var dhl = new StackPanel { Orientation = Orientation.Horizontal };
            dhl.Children.Add(Ui.Tile("disk", Ui.TGreen, 30));
            dhl.Children.Add(Ui.Text("磁盘", 13.5, Ui.Ink, FontWeights.Medium).Also(t => t.Margin = new Thickness(10, 0, 0, 0)));
            dh.Children.Add(dhl);
            _health = Ui.Text("", 12.5, Ui.Green, FontWeights.Medium); _health.HorizontalAlignment = HorizontalAlignment.Right;
            dh.Children.Add(_health);
            dk.Children.Add(dh);
            _disks.Margin = new Thickness(0, 8, 0, 0);
            dk.Children.Add(_disks);
            sp.Children.Add(Ui.Card(dk, new Thickness(18, 14, 18, 14)).Also(c => c.Margin = new Thickness(0, 12, 0, 0)));

            // last clean
            var lc = new Grid();
            lc.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lc.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lc.Children.Add(Ui.SmallIcon("history", 20, Ui.Accent, 1.5).Also(x => x.Margin = new Thickness(0, 0, 14, 0)));
            var lct = new StackPanel();
            _last = Ui.Text("", 13.5, Ui.Ink, FontWeights.Medium);
            lct.Children.Add(_last);
            lct.Children.Add(Ui.Text("重复文件、卸载残留删掉后 30 天内都能在恢复中心找回", 12, Ui.Gray));
            Grid.SetColumn(lct, 1); lc.Children.Add(lct);
            var rec = Ui.Ghost("查看记录", (s, e) => MainWindow.Go("restore"));
            Grid.SetColumn(rec, 2); lc.Children.Add(rec);
            sp.Children.Add(Ui.Card(lc, new Thickness(18, 12, 18, 12)).Also(c => c.Margin = new Thickness(0, 12, 0, 0)));

            Content = new ScrollViewer { Content = new Border { Padding = new Thickness(32, 22, 32, 22), Child = sp }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _timer.Tick += (s, e) => Tick();
            IsVisibleChanged += (s, e) => { if (IsVisible) _timer.Start(); else _timer.Stop(); };
        }

        Border Metric(string name, string icon, (string, string) pal, out TextBlock val, out TextBlock sub, out Grid bar, int col)
        {
            var sp = new StackPanel();
            var h = new StackPanel { Orientation = Orientation.Horizontal };
            h.Children.Add(Ui.Tile(icon, pal, 30));
            h.Children.Add(Ui.Text(name, 13.5, Ui.Ink, FontWeights.Medium).Also(t => t.Margin = new Thickness(10, 0, 0, 0)));
            sp.Children.Add(h);
            val = Ui.Text("—", 24, Ui.Br(pal.Item2), FontWeights.SemiBold); val.Margin = new Thickness(0, 12, 0, 4);
            sp.Children.Add(val);
            bar = Ui.Bar(0, Ui.Br(pal.Item2), 4);
            sp.Children.Add(bar);
            sub = Ui.Text("", 11.5, Ui.Gray); sub.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(sub);
            var c = Ui.Card(sp, new Thickness(16, 14, 16, 14));
            c.Margin = new Thickness(col == 0 ? 0 : 8, 0, col == 2 ? 0 : 8, 0);
            Grid.SetColumn(c, col);
            return c;
        }

        public async void OnShown()
        {
            _timer.Start();
            Tick();
            Disks();
            var last = History.LastCleanText();
            _last.Text = last == null ? "还没有清理过" : "上次清理：" + last;
            if (_loaded) { Score(); return; }
            _loaded = true;
            _info.Text = await Task.Run(SysInfo);
            var t1 = Task.Run(() => { var l = StartupPage.ReadAll(); return l.Count(i => i.Enabled); });
            var t2 = Task.Run(() => Proc.PowerShell("(Get-PhysicalDisk | Select-Object -ExpandProperty HealthStatus) -join ','", 20000));
            var t3 = Task.Run(() => Cleaner.Categories().Where(c => c.Checked && !c.IsRecycleBin).Sum(c => c.Scan()));
            try { _startupOn = await t1; } catch { }
            _st.Text = _startupOn < 0 ? "—" : $"{_startupOn} 个";
            _stSub.Text = _startupOn > 10 ? "有点多，开机会变慢" : "正常";
            Ui.SetBar(_stBar, Math.Min(1, Math.Max(0, _startupOn) / 20.0));
            Score();
            try { _smart = await t2; } catch { }
            Score();
            try { _junk = await t3; } catch { }
            Score();
        }

        void Score()
        {
            int score = 100;
            var issues = new List<string>();
            var c = CDrive();
            if (c != null)
            {
                double free = (double)c.AvailableFreeSpace / c.TotalSize;
                if (free < 0.10) { score -= 25; issues.Add($"C 盘只剩 {Fmt.Size(c.AvailableFreeSpace)}"); }
                else if (free < 0.15) { score -= 12; issues.Add($"C 盘只剩 {Fmt.Size(c.AvailableFreeSpace)}"); }
                else if (free < 0.20) score -= 5;
            }
            if (_startupOn > 15) { score -= 10; issues.Add($"开机启动项有 {_startupOn} 个"); } else if (_startupOn > 10) score -= 5;
            bool diskBad = _smart != null && _smart.Split(',').Any(x => x.Trim().Length > 0 && !x.Trim().Equals("Healthy", StringComparison.OrdinalIgnoreCase));
            if (diskBad) { score -= 30; issues.Add("硬盘健康有警告，记得备份"); }
            if (_junk > 5L << 30) { score -= 8; } else if (_junk > 2L << 30) score -= 4;
            score = Math.Clamp(score, 0, 100);
            var color = score >= 85 ? Ui.GreenSoft : score >= 65 ? Ui.Br("#D98A00") : Ui.Red;
            _ring.Content = W.Ring(score / 100.0, 104, color, score.ToString(), "健康分");
            _state.Text = score >= 85 ? "电脑状态良好" : score >= 65 ? "有几处可以改善" : "需要处理一下";
            var junk = _junk < 0 ? "正在统计可以清理的空间" : _junk > 0 ? $"有 {Fmt.Size(_junk)} 可以安全清理" : "没有需要清理的垃圾";
            _stateSub.Text = issues.Count == 0 ? junk + "，其他都正常。" : junk + "；" + string.Join("，", issues) + "。";
            _chip.Content = issues.Count == 0 ? Ui.Chip("没有需要处理的问题", Ui.Green, "#E5F4EA") : Ui.Chip($"{issues.Count} 项需要注意", Ui.Amber, "#FFF4CE");
            _health.Text = _smart == null ? "" : diskBad ? "硬盘健康：有警告" : "硬盘健康：良好";
            _health.Foreground = diskBad ? Ui.Red : Ui.Green;
        }

        static DriveInfo CDrive()
        {
            try { return new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)); } catch { return null; }
        }

        void Disks()
        {
            _disks.Children.Clear();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady || d.TotalSize <= 0) continue;
                    double used = 1 - (double)d.AvailableFreeSpace / d.TotalSize;
                    var g = new Grid { Margin = new Thickness(0, 10, 0, 2) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                    g.Children.Add(Ui.Text(d.Name.TrimEnd('\\'), 13, Ui.Ink, FontWeights.SemiBold));
                    var mid = new StackPanel();
                    mid.Children.Add(Ui.Bar(used, used > 0.85 ? Ui.Br("#D98A00") : Ui.GreenSoft).Also(b => b.Margin = new Thickness(0, 6, 0, 0)));
                    var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : d.VolumeLabel + " · ";
                    mid.Children.Add(Ui.Text($"{label}已用 {Fmt.Size(d.TotalSize - d.AvailableFreeSpace)}，共 {Fmt.Size(d.TotalSize)}，剩 {Fmt.Size(d.AvailableFreeSpace)}", 11.5, Ui.Gray).Also(t => t.Margin = new Thickness(0, 5, 0, 0)));
                    Grid.SetColumn(mid, 1); g.Children.Add(mid);
                    var pct = Ui.Text($"{(int)(used * 100)}%", 13, Ui.Ink, FontWeights.SemiBold); pct.HorizontalAlignment = HorizontalAlignment.Right; pct.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(pct, 2); g.Children.Add(pct);
                    _disks.Children.Add(g);
                }
                catch { }
            }
        }

        // ---------- live metrics ----------
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [StructLayout(LayoutKind.Sequential)]
        class MEMSTAT { public uint dwLength = (uint)Marshal.SizeOf<MEMSTAT>(); public uint dwMemoryLoad; public ulong total, avail, tp, ap, tv, av, ave; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMSTAT m);
        long _pi, _pk, _pu;

        void Tick()
        {
            try
            {
                if (GetSystemTimes(out var i, out var k, out var u))
                {
                    if (_pk != 0)
                    {
                        long di = i - _pi, dt = (k - _pk) + (u - _pu);
                        double cpu = dt > 0 ? Math.Clamp(1.0 - (double)di / dt, 0, 1) : 0;
                        _cpu.Text = (cpu * 100).ToString(cpu < 0.1 ? "0.0" : "0") + "%";
                        Ui.SetBar(_cpuBar, cpu);
                        _cpuSub.Text = Environment.ProcessorCount + " 个逻辑处理器";
                    }
                    _pi = i; _pk = k; _pu = u;
                }
                var m = new MEMSTAT();
                if (GlobalMemoryStatusEx(m))
                {
                    _mem.Text = m.dwMemoryLoad + "%";
                    Ui.SetBar(_memBar, m.dwMemoryLoad / 100.0);
                    _memSub.Text = $"{Fmt.Size((long)(m.total - m.avail))} / {Fmt.Size((long)m.total)}";
                }
            }
            catch { }
        }

        static string SysInfo()
        {
            var parts = new List<string>();
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var name = k?.GetValue("ProductName")?.ToString() ?? "Windows";
                if (Environment.OSVersion.Version.Build >= 22000) name = name.Replace("Windows 10", "Windows 11");
                name = name.Replace("Home China", "家庭中文版").Replace("Home", "家庭版").Replace("Professional", "专业版").Replace("Pro", "专业版");
                var ver = k?.GetValue("DisplayVersion")?.ToString();
                parts.Add(name + (ver != null ? " " + ver : ""));
            }
            catch { parts.Add("Windows"); }
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            parts.Add(up.TotalDays >= 1 ? $"已运行 {(int)up.TotalDays} 天 {up.Hours} 小时" : $"已运行 {up.Hours} 小时 {up.Minutes} 分");
            try
            {
                using var c = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                var cpu = c?.GetValue("ProcessorNameString")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(cpu)) parts.Add(System.Text.RegularExpressions.Regex.Replace(cpu.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", ""), @"\s+", " "));
            }
            catch { }
            var m = new MEMSTAT();
            if (GlobalMemoryStatusEx(m)) parts.Add($"{Math.Round(m.total / 1073741824.0)} GB 内存");
            return string.Join(" · ", parts);
        }
    }
}
