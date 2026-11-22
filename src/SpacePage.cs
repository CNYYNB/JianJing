using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XinCleaner
{
    class SpaceNode
    {
        public string Name, Path;
        public long Size, Files;
        public bool IsDir, IsOther;
        public SpaceNode Parent;
        public List<SpaceNode> Children = new();
        public long SmallSize, SmallCount;
    }

    class SpaceScan
    {
        public SpaceNode Root;
        public ConcurrentBag<(string path, long size, DateTime t)> Big = new();
        public ConcurrentDictionary<string, (long size, long count)> Types = new();
        public long FileCount;
        public volatile bool Cancel;

        static readonly EnumerationOptions Opt = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint, ReturnSpecialDirectories = false };
        const long KeepFile = 200L << 20, BigFile = 100L << 20;

        public static string TypeOf(string ext) => ext.ToLowerInvariant() switch
        {
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".ts" => "视频",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" or ".raw" or ".cr2" or ".nef" or ".psd" or ".tif" or ".tiff" => "图片",
            ".mp3" or ".flac" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".wma" or ".ape" => "音乐",
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".pdf" or ".txt" or ".md" or ".wps" or ".epub" => "文档",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".xz" or ".iso" => "压缩包和镜像",
            ".exe" or ".msi" or ".dll" or ".sys" or ".pak" or ".bin" or ".dat" => "程序和游戏数据",
            _ => "其他",
        };

        public void Run(string root)
        {
            Root = new SpaceNode { Name = root, Path = root, IsDir = true };
            Walk(Root, 0);
        }

        void Walk(SpaceNode n, int depth)
        {
            if (Cancel) return;
            List<FileSystemInfo> entries;
            try { entries = new DirectoryInfo(n.Path).EnumerateFileSystemInfos("*", Opt).ToList(); } catch { return; }
            var dirs = new List<SpaceNode>();
            foreach (var e in entries)
            {
                if (e is DirectoryInfo d) { var c = new SpaceNode { Name = d.Name, Path = d.FullName, IsDir = true, Parent = n }; dirs.Add(c); }
                else if (e is FileInfo f)
                {
                    long len; try { len = f.Length; } catch { continue; }
                    n.Size += len; n.Files++;
                    Interlocked.Increment(ref FileCount);
                    var tp = TypeOf(f.Extension);
                    Types.AddOrUpdate(tp, (len, 1), (k, v) => (v.size + len, v.count + 1));
                    if (len >= BigFile) { DateTime t; try { t = f.LastWriteTime; } catch { t = DateTime.MinValue; } Big.Add((f.FullName, len, t)); }
                    if (len >= KeepFile) n.Children.Add(new SpaceNode { Name = f.Name, Path = f.FullName, Size = len, Files = 1, Parent = n });
                    else { n.SmallSize += len; n.SmallCount++; }
                }
            }
            if (depth < 2) Parallel.ForEach(dirs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, c => Walk(c, depth + 1));
            else foreach (var c in dirs) Walk(c, depth + 1);
            foreach (var c in dirs) { n.Size += c.Size; n.Files += c.Files; }
            n.Children.AddRange(dirs.Where(c => c.Size > 0));
            n.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
        }
    }

    class SpacePage : UserControl, IPage
    {
        static readonly string[] Palette = { "#B9B4F5", "#F59CC8", "#7FD3B0", "#8EC9F5", "#F5B880", "#F08A8A", "#FFD979", "#A8E0D8", "#D7C4F2", "#BFD8A6", "#F7C6A3", "#9FB8E8" };
        readonly TextBlock _sub;
        readonly StackPanel _drives = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        readonly ContentControl _body = new ContentControl();
        readonly Canvas _map = new Canvas { ClipToBounds = true, Background = Brushes.Transparent };
        readonly StackPanel _top = new StackPanel();
        readonly StackPanel _crumbs = new StackPanel { Orientation = Orientation.Horizontal };
        readonly ContentControl _detail = new ContentControl();
        readonly Grid _mapView;
        RadioButton _tabMap, _tabType, _tabBig;
        SpaceScan _scan; SpaceNode _cur, _sel; string _drive; bool _loaded;
        readonly Dictionary<string, SpaceScan> _cache = new(StringComparer.OrdinalIgnoreCase);

        public SpacePage()
        {
            var head = new Grid();
            var hl = new StackPanel();
            hl.Children.Add(Ui.Title("空间分析"));
            _sub = Ui.Text("", 13, Ui.Gray); _sub.Margin = new Thickness(0, 4, 0, 0);
            hl.Children.Add(_sub);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _tabMap = Tab("磁盘地图", true); _tabType = Tab("按类型", false); _tabBig = Tab("大文件", false);
            tabs.Children.Add(_tabMap); tabs.Children.Add(_tabType); tabs.Children.Add(_tabBig);
            hl.Children.Add(tabs);
            head.Children.Add(hl);
            head.Children.Add(_drives);

            // map view
            _mapView = new Grid();
            _mapView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mapView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _mapView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _crumbs.Margin = new Thickness(2, 0, 0, 10);
            _mapView.Children.Add(_crumbs);
            var mid = new Grid();
            mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            var mapCard = Ui.Card(_map, new Thickness(8));
            mapCard.MinHeight = 360;
            _map.SizeChanged += (s, e) => DrawMap();
            mid.Children.Add(mapCard);
            var topCard = Ui.Card(new StackPanel { Children = { Ui.Text("占用最多", 13, Ui.Ink, FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 6)), _top } }, new Thickness(16, 14, 16, 14));
            topCard.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(topCard, 1); mid.Children.Add(topCard);
            Grid.SetRow(mid, 1); _mapView.Children.Add(mid);
            _detail.Margin = new Thickness(0, 14, 0, 0);
            Grid.SetRow(_detail, 2); _mapView.Children.Add(_detail);

            var root = new Grid { Margin = new Thickness(32, 22, 32, 22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(head);
            _body.Margin = new Thickness(0, 14, 0, 0);
            Grid.SetRow(_body, 1); root.Children.Add(_body);
            Content = root;
        }

        RadioButton Tab(string s, bool on)
        {
            var r = new RadioButton { Content = s, GroupName = "spacetab", IsChecked = on, Style = (Style)Application.Current.FindResource("TabItem") };
            r.Checked += (a, e) => ShowTab();
            return r;
        }

        public async void OnShown()
        {
            if (_loaded) return;
            _loaded = true;
            var drives = DriveInfo.GetDrives().Where(d => { try { return d.DriveType == DriveType.Fixed && d.IsReady; } catch { return false; } }).ToList();
            bool first = true;
            foreach (var d in drives)
            {
                var name = d.Name.TrimEnd('\\').TrimEnd(':') + " 盘";
                var rb = new RadioButton { Content = name, GroupName = "drive", IsChecked = first, Style = (Style)Application.Current.FindResource("TabItem"), Margin = new Thickness(18, 4, 0, 0) };
                var dn = d.Name;
                rb.Checked += async (s, e) => await Load(dn);
                _drives.Children.Add(rb);
                first = false;
            }
            if (drives.Count > 0) await Load(drives[0].Name);
        }

        async Task Load(string drive)
        {
            _drive = drive;
            if (_scan != null && _scan.Root?.Path != drive) _scan.Cancel = true;
            DriveInfo di = null; try { di = new DriveInfo(drive); } catch { }
            var used = di != null ? $"已用 {Fmt.Size(di.TotalSize - di.AvailableFreeSpace)}，共 {Fmt.Size(di.TotalSize)}" : "";
            if (!_cache.TryGetValue(drive, out var sc))
            {
                sc = new SpaceScan();
                _scan = sc;
                var bar = Ui.Bar(0.02, Ui.Accent, 4); bar.Width = 360; bar.HorizontalAlignment = HorizontalAlignment.Left;
                var info = Ui.Text("正在统计，文件多的话要一两分钟…", 13, Ui.Gray);
                _body.Content = Ui.Card(new StackPanel { Children = { info, bar.Also(b => b.Margin = new Thickness(0, 12, 0, 0)) } }, new Thickness(20, 20, 20, 20));
                _sub.Text = drive.TrimEnd('\\') + " · " + used;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                long total = di != null ? di.TotalSize - di.AvailableFreeSpace : 1;
                timer.Tick += (s, e) => { info.Text = $"正在统计… 已经数了 {sc.FileCount:N0} 个文件"; };
                timer.Start();
                await Task.Run(() => sc.Run(drive));
                timer.Stop();
                if (sc.Cancel) return;
                _cache[drive] = sc;
                if (_drive != drive) return;
            }
            _scan = sc;
            _cur = sc.Root; _sel = null;
            _sub.Text = drive.TrimEnd('\\') + " · " + used + "。块越大，占的地方越多。";
            ShowTab();
        }

        void ShowTab()
        {
            if (_scan?.Root == null || !_cache.ContainsKey(_drive ?? "")) return;
            if (_tabMap.IsChecked == true) { _body.Content = _mapView; Navigate(_cur); }
            else if (_tabType.IsChecked == true) _body.Content = TypeView();
            else _body.Content = BigView();
        }

        // ---------- map ----------
        void Navigate(SpaceNode n)
        {
            _cur = n; _sel = null;
            _crumbs.Children.Clear();
            var chain = new List<SpaceNode>();
            for (var x = n; x != null; x = x.Parent) chain.Insert(0, x);
            if (chain.Count > 1)
            {
                var back = new Button { Style = (Style)Application.Current.FindResource("LinkButton"), Content = Ui.SmallIcon("back", 16, Ui.Accent, 1.8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "返回上一层" };
                back.Click += (s, e) => Navigate(n.Parent);
                _crumbs.Children.Add(back);
            }
            for (int i = 0; i < chain.Count; i++)
            {
                var node = chain[i];
                var label = i == 0 ? node.Name.TrimEnd('\\') : node.Name;
                if (i == chain.Count - 1) _crumbs.Children.Add(Ui.Text(label, 13.5, Ui.Ink, FontWeights.SemiBold));
                else
                {
                    _crumbs.Children.Add(Ui.Link(label, (s, e) => Navigate(node)));
                    _crumbs.Children.Add(Ui.Text("  ›  ", 13, Ui.Light));
                }
            }
            _crumbs.Children.Add(Ui.Text($"    {Fmt.Size(n.Size)} · {n.Files:N0} 个文件", 12.5, Ui.Gray));

            _top.Children.Clear();
            var kids = Blocks(n);
            long max = Math.Max(1, kids.Count > 0 ? kids.Max(k => k.Size) : 1);
            int ci = 0;
            foreach (var k in kids.Take(8))
            {
                var color = k.IsOther ? "#C8CDD6" : Palette[ci++ % Palette.Length];
                var row = new StackPanel { Margin = new Thickness(0, 6, 0, 4), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                var line = Ui.Cols((new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = Ui.Br(color), Margin = new Thickness(0, 0, 8, 0) }, Ui.Auto),
                                   (Ui.Text(k.Name, 13, Ui.Ink).Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }), Ui.Star()),
                                   (Ui.Text(Fmt.Size(k.Size), 13, Ui.Ink, FontWeights.SemiBold), Ui.Auto));
                row.Children.Add(line);
                row.Children.Add(Ui.Bar((double)k.Size / max, Ui.Br(color), 4, "#EEF0F3").Also(b => b.Margin = new Thickness(18, 5, 0, 0)));
                var node = k;
                row.MouseLeftButtonUp += (s, e) => Select(node);
                _top.Children.Add(row);
            }
            DrawMap();
            Select(kids.FirstOrDefault(k => !k.IsOther));
        }

        static List<SpaceNode> Blocks(SpaceNode n)
        {
            var l = n.Children.Where(c => c.Size > 0).ToList();
            if (n.SmallSize > 0) l.Add(new SpaceNode { Name = $"{n.SmallCount:N0} 个小文件", Path = n.Path, Size = n.SmallSize, Files = n.SmallCount, IsOther = true, Parent = n });
            l.Sort((a, b) => b.Size.CompareTo(a.Size));
            // fold tiny blocks into "其他"
            long total = Math.Max(1, l.Sum(x => x.Size));
            var keep = l.Where((x, i) => i < 24 && x.Size >= total * 0.004).ToList();
            var rest = l.Except(keep).ToList();
            if (rest.Count > 0) keep.Add(new SpaceNode { Name = $"其他 {rest.Count} 项", Path = n.Path, Size = rest.Sum(x => x.Size), Files = rest.Sum(x => x.Files), IsOther = true, Parent = n });
            return keep;
        }

        readonly Dictionary<SpaceNode, Border> _blocks = new();

        void DrawMap()
        {
            _map.Children.Clear(); _blocks.Clear();
            if (_cur == null || _map.ActualWidth < 20 || _map.ActualHeight < 20) return;
            var kids = Blocks(_cur);
            var rects = Squarify(kids.Select(k => (double)k.Size).ToList(), new Rect(0, 0, _map.ActualWidth, _map.ActualHeight));
            int ci = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                var k = kids[i]; var r = rects[i];
                if (r.Width < 2 || r.Height < 2) continue;
                var color = k.IsOther ? "#D5D9E0" : Palette[ci++ % Palette.Length];
                var b = new Border
                {
                    Width = Math.Max(0, r.Width - 3), Height = Math.Max(0, r.Height - 3), CornerRadius = new CornerRadius(6), Background = Ui.Br(color),
                    BorderBrush = Ui.Accent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                    ToolTip = $"{k.Name}\n{Fmt.Size(k.Size)}" + (k.IsDir ? "\n双击进去看" : ""),
                };
                if (r.Width > 70 && r.Height > 40)
                {
                    var sp = new StackPanel { Margin = new Thickness(10, 8, 8, 0) };
                    sp.Children.Add(Ui.Text(k.Name, r.Width > 130 ? 13 : 11.5, Ui.Br("#1F2430"), FontWeights.SemiBold).Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; t.VerticalAlignment = VerticalAlignment.Top; }));
                    sp.Children.Add(Ui.Text(Fmt.Size(k.Size), 11.5, Ui.Br("#3A4150")).Also(t => t.VerticalAlignment = VerticalAlignment.Top));
                    b.Child = sp;
                }
                var node = k;
                b.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount >= 2 && node.IsDir && node.Children.Count + (node.SmallSize > 0 ? 1 : 0) > 0) Navigate(node);
                    else Select(node);
                };
                Canvas.SetLeft(b, r.X + 1.5); Canvas.SetTop(b, r.Y + 1.5);
                _map.Children.Add(b);
                _blocks[k] = b;
            }
            if (_sel != null && _blocks.TryGetValue(_sel, out var sb)) sb.BorderThickness = new Thickness(2.5);
        }

        static List<Rect> Squarify(List<double> sizes, Rect area)
        {
            var result = new Rect[sizes.Count];
            double total = sizes.Sum();
            if (total <= 0) return result.ToList();
            var areas = sizes.Select(s => s / total * area.Width * area.Height).ToList();
            int idx = 0; var rect = area;
            while (idx < areas.Count)
            {
                double side = Math.Min(rect.Width, rect.Height);
                var row = new List<int> { idx };
                double rowSum = areas[idx];
                double Worst(List<int> r, double sum)
                {
                    double mx = r.Max(i => areas[i]), mn = r.Min(i => areas[i]);
                    if (sum <= 0 || mn <= 0) return double.MaxValue;
                    return Math.Max(side * side * mx / (sum * sum), (sum * sum) / (side * side * mn));
                }
                int j = idx + 1;
                while (j < areas.Count)
                {
                    var trial = new List<int>(row) { j };
                    if (Worst(trial, rowSum + areas[j]) <= Worst(row, rowSum)) { row = trial; rowSum += areas[j]; j++; }
                    else break;
                }
                bool horizontal = rect.Width >= rect.Height; // lay row along the short side
                double thick = rowSum / side;
                double off = 0;
                foreach (var i in row)
                {
                    double len = areas[i] / thick;
                    result[i] = horizontal ? new Rect(rect.X, rect.Y + off, thick, len) : new Rect(rect.X + off, rect.Y, len, thick);
                    off += len;
                }
                rect = horizontal ? new Rect(rect.X + thick, rect.Y, Math.Max(0, rect.Width - thick), rect.Height) : new Rect(rect.X, rect.Y + thick, rect.Width, Math.Max(0, rect.Height - thick));
                idx = j;
            }
            return result.ToList();
        }

        static (string text, Brush color) Protection(SpaceNode n)
        {
            var p = n.Path;
            if (Safety.IsSystem(p) || p.EndsWith("pagefile.sys", StringComparison.OrdinalIgnoreCase) || p.EndsWith("hiberfil.sys", StringComparison.OrdinalIgnoreCase) || p.EndsWith("swapfile.sys", StringComparison.OrdinalIgnoreCase))
                return ("系统文件，别动", Ui.Red);
            if (Safety.IsProgram(p)) return ("软件本体，想删请到软件管理里卸载", Ui.Amber);
            if (p.IndexOf(@"\AppData\", StringComparison.OrdinalIgnoreCase) >= 0) return ("软件的数据和缓存，可清理的部分在扫描清理里", Ui.Gray);
            return ("你自己的文件，简境不会自动删除", Ui.Gray);
        }

        void Select(SpaceNode n)
        {
            if (_sel != null && _blocks.TryGetValue(_sel, out var ob)) ob.BorderThickness = new Thickness(0);
            _sel = n;
            if (n == null) { _detail.Content = null; return; }
            if (_blocks.TryGetValue(n, out var nb)) nb.BorderThickness = new Thickness(2.5);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(Ui.Tile(n.IsDir ? "folder" : "file", Ui.TPurple, 38).Also(t => t.Margin = new Thickness(0, 0, 14, 0)));
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(Ui.Text(n.Name, 14.5, Ui.Ink, FontWeights.SemiBold));
            var (pt, pc) = Protection(n);
            var desc = new TextBlock { FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis };
            desc.Inlines.Add(new System.Windows.Documents.Run($"{(n.IsOther ? n.Path : n.Path)} · {Fmt.Size(n.Size)}" + (n.Files > 1 ? $" · {n.Files:N0} 个文件" : "") + " · ") { Foreground = Ui.Gray });
            desc.Inlines.Add(new System.Windows.Documents.Run(pt) { Foreground = pc });
            t.Children.Add(desc);
            Grid.SetColumn(t, 1); g.Children.Add(t);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(Ui.IconButton("open", "打开位置", false, (s, e) => OpenAt(n)));
            if (n.IsDir && !n.IsOther && (n.Children.Count > 0 || n.SmallSize > 0))
                btns.Children.Add(Ui.Pill("进去看看", (s, e) => Navigate(n)).Also(b => b.Margin = new Thickness(10, 0, 0, 0)));
            Grid.SetColumn(btns, 2); g.Children.Add(btns);
            _detail.Content = Ui.Card(g, new Thickness(16, 12, 16, 12));
        }

        static void OpenAt(SpaceNode n)
        {
            try
            {
                if (n.IsDir || n.IsOther) Proc.Open(n.Path);
                else System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{n.Path}\"");
            }
            catch { }
        }

        // ---------- by type ----------
        FrameworkElement TypeView()
        {
            var sp = new StackPanel();
            var items = _scan.Types.OrderByDescending(kv => kv.Value.size).ToList();
            long max = Math.Max(1, items.Count > 0 ? items.Max(i => i.Value.size) : 1);
            var icons = new Dictionary<string, (string, (string, string))>
            {
                ["视频"] = ("image", Ui.TRed), ["图片"] = ("image", Ui.TPurple), ["音乐"] = ("file", Ui.TOrange), ["文档"] = ("file", Ui.TBlue),
                ["压缩包和镜像"] = ("cube", Ui.TOrange), ["程序和游戏数据"] = ("apps", Ui.TGreen), ["其他"] = ("folder", Ui.TGray),
            };
            bool first = true;
            foreach (var (name, v) in items.Select(kv => (kv.Key, kv.Value)))
            {
                if (!first) sp.Children.Add(Ui.Divider());
                first = false;
                var (ic, pal) = icons.TryGetValue(name, out var x) ? x : ("file", Ui.TGray);
                var right = new StackPanel { Width = 300, Orientation = Orientation.Horizontal };
                var bar = Ui.Bar((double)v.size / max, Ui.Br(pal.Item2), 5, "#EEF0F3"); bar.Width = 180; bar.Margin = new Thickness(0, 0, 18, 0);
                right.Children.Add(bar);
                right.Children.Add(Ui.Text(Fmt.Size(v.size), 14, Ui.Ink, FontWeights.SemiBold).Also(t => { t.Width = 100; t.TextAlignment = TextAlignment.Right; }));
                sp.Children.Add(Ui.Row(Ui.Tile(ic, pal, 38), name, $"{v.count:N0} 个文件", right));
            }
            var outer = new StackPanel();
            outer.Children.Add(Ui.Card(sp, new Thickness(18, 2, 18, 2)));
            outer.Children.Add(Ui.SafeNote("这里只是统计，不会删除任何文件。").Also(n => n.Margin = new Thickness(0, 14, 0, 0)));
            return new ScrollViewer { Content = outer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 4) };
        }

        // ---------- big files ----------
        FrameworkElement BigView()
        {
            var sp = new StackPanel();
            var list = _scan.Big.OrderByDescending(b => b.size).Take(100).ToList();
            if (list.Count == 0) sp.Children.Add(Ui.Text("没有超过 100 MB 的文件", 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 16, 0, 16)));
            bool first = true;
            foreach (var (path, size, t) in list)
            {
                if (!first) sp.Children.Add(Ui.Divider());
                first = false;
                var n = new SpaceNode { Name = Path.GetFileName(path), Path = path, Size = size, Files = 1 };
                var (pt, pc) = Protection(n);
                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(Ui.Text(Fmt.Size(size), 14, Ui.Ink, FontWeights.SemiBold).Also(x => { x.Width = 90; x.TextAlignment = TextAlignment.Right; x.Margin = new Thickness(0, 0, 16, 0); }));
                right.Children.Add(Ui.IconButton("open", "打开位置", false, (s, e) => OpenAt(n)));
                var desc = $"{Path.GetDirectoryName(path)}" + (t > DateTime.MinValue ? $" · {t:yyyy-MM-dd} 修改" : "") + " · " + pt;
                var row = Ui.Row(Ui.Tile("file", SpaceScan.TypeOf(Path.GetExtension(path)) == "视频" ? Ui.TRed : Ui.TBlue, 38), n.Name, desc, right, out _, out var dt);
                dt.Foreground = pc == Ui.Gray ? Ui.Gray : pc;
                dt.TextTrimming = TextTrimming.CharacterEllipsis; dt.TextWrapping = TextWrapping.NoWrap;
                sp.Children.Add(row);
            }
            var outer = new StackPanel();
            outer.Children.Add(Ui.Text("超过 100 MB 的文件，按大小排。要不要删由你决定，简境不会自动删。", 13, Ui.Gray).Also(t => t.Margin = new Thickness(2, 0, 0, 10)));
            outer.Children.Add(Ui.Card(sp, new Thickness(18, 2, 18, 2)));
            return new ScrollViewer { Content = outer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 4) };
        }
    }
}
