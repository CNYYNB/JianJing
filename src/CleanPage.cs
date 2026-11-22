using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XinCleaner
{
    class CleanPage : UserControl, IPage
    {
        class RowUi { public CheckBox Chk; public ContentControl Status; public TextBlock Size; }

        readonly List<CleanCategory> _cats = Cleaner.Categories();
        readonly Dictionary<CleanCategory, RowUi> _rows = new();
        readonly TextBlock _title, _sub, _pct, _total, _totalSub, _footText;
        readonly Grid _progress;
        readonly Button _go, _rescan;
        readonly ContentControl _view = new ContentControl();
        readonly Grid _scanView;
        readonly StackPanel _bars = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right };
        bool _busy, _scanned;
        double _frac;

        public CleanPage()
        {
            // ----- header -----
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var hl = new StackPanel();
            _title = Ui.Title("正在扫描你的电脑…");
            _sub = Ui.Text("大概需要一分钟，扫描时可以照常用电脑。", 13, Ui.Gray); _sub.Margin = new Thickness(0, 4, 0, 0);
            hl.Children.Add(_title); hl.Children.Add(_sub);
            var pg = new Grid { Margin = new Thickness(0, 14, 0, 0), MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left, Width = 600 };
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            _progress = Ui.Bar(0, Ui.Accent);
            _progress.VerticalAlignment = VerticalAlignment.Center;
            pg.Children.Add(_progress);
            _pct = Ui.Text("0%", 14, Ui.Accent, FontWeights.SemiBold); _pct.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_pct, 1); pg.Children.Add(_pct);
            hl.Children.Add(pg);
            head.Children.Add(hl);
            _rescan = Ui.Ghost("重新扫描", async (s, e) => await Scan());
            _rescan.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(_rescan, 1); head.Children.Add(_rescan);

            // ----- list card -----
            var list = new StackPanel();
            var colHead = Ui.Cols((Ui.Text("类别", 12.5, Ui.Gray, FontWeights.Medium), Ui.Star()), (Ui.Text("状态", 12.5, Ui.Gray, FontWeights.Medium), Ui.Px(110)),
                                  (Ui.Text("找到", 12.5, Ui.Gray, FontWeights.Medium).Also(t => t.TextAlignment = TextAlignment.Right), Ui.Px(90)), (null, Ui.Px(28)));
            colHead.Margin = new Thickness(0, 12, 0, 6);
            list.Children.Add(colHead);
            bool first = true;
            foreach (var c in _cats)
            {
                if (!first) list.Children.Add(Ui.Divider());
                first = false;
                var chk = Ui.Check(c.Checked);
                var cat = c;
                chk.Checked += (s, e) => { cat.Checked = true; Store.Set("cat." + cat.Key, "1"); Refresh(); };
                chk.Unchecked += (s, e) => { cat.Checked = false; Store.Set("cat." + cat.Key, "0"); Refresh(); };
                var left = new StackPanel { Orientation = Orientation.Horizontal };
                left.Children.Add(chk);
                var tile = Ui.Tile(c.Icon, c.Pal, 38); tile.Margin = new Thickness(12, 0, 0, 0);
                left.Children.Add(tile);
                var status = new ContentControl { Content = W.Status(W.Waiting(), "等待"), Width = 110, VerticalAlignment = VerticalAlignment.Center };
                var size = Ui.Text("—", 14, Ui.Gray, FontWeights.SemiBold); size.Width = 90; size.TextAlignment = TextAlignment.Right;
                var more = new Button { Style = (Style)Application.Current.FindResource("LinkButton"), Content = Ui.SmallIcon("chev", 14, Ui.Light), Width = 28, ToolTip = "看看要删哪些文件" };
                more.Click += async (s, e) => await ShowPreview(new[] { cat });
                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(status); right.Children.Add(size); right.Children.Add(more);
                var row = Ui.Row(left, c.Name, c.Desc, right);
                row.MinHeight = 66;
                list.Children.Add(row);
                _rows[c] = new RowUi { Chk = chk, Status = status, Size = size };
            }
            var listCard = Ui.Card(list, new Thickness(18, 0, 14, 6));

            // ----- right column -----
            var rc = new StackPanel();
            var relGrid = new Grid();
            var relText = new StackPanel();
            relText.Children.Add(Ui.Text("可以释放", 13, Ui.Ink, FontWeights.Medium));
            relText.Children.Add(Ui.SmallIcon("leaf", 15, Ui.GreenSoft, 1.7).Also(x => { x.HorizontalAlignment = HorizontalAlignment.Left; x.Margin = new Thickness(0, 12, 0, 0); }));
            _total = Ui.Text("—", 28, Ui.Green, FontWeights.SemiBold); _total.Margin = new Thickness(0, 4, 0, 0);
            _totalSub = Ui.Text("正在统计", 12.5, Ui.Gray);
            relText.Children.Add(_total); relText.Children.Add(_totalSub);
            relGrid.Children.Add(relText);
            relGrid.Children.Add(_bars);
            rc.Children.Add(Ui.Card(relGrid, new Thickness(18, 16, 18, 16)));

            var pv = new StackPanel();
            pv.Children.Add(Ui.Text("删除前先预览", 13, Ui.Ink, FontWeights.Medium));
            pv.Children.Add(PreviewArt().Also(a => a.Margin = new Thickness(0, 14, 0, 12)));
            pv.Children.Add(Ui.Text("每一类要删什么都能点开看，你点了清理才会动。", 12.5, Ui.Gray));
            var pvBtn = Ui.IconButton("list", "查看要删的文件", false, async (s, e) => await ShowPreview(_cats.Where(c => c.Checked)));
            pvBtn.Margin = new Thickness(0, 14, 0, 0); pvBtn.HorizontalAlignment = HorizontalAlignment.Stretch; pvBtn.Foreground = Ui.Accent;
            pv.Children.Add(pvBtn);
            var pvCard = Ui.Card(pv, new Thickness(18, 16, 18, 16)); pvCard.Margin = new Thickness(0, 14, 0, 0);
            rc.Children.Add(pvCard);

            _scanView = new Grid();
            _scanView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _scanView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(264) });
            var col = new StackPanel();
            col.Children.Add(listCard);
            col.Children.Add(Ui.SafeNote("只清理确认安全的缓存和临时文件，不碰系统文件、软件本体、聊天记录和浏览器登录。").Also(n => n.Margin = new Thickness(0, 14, 0, 0)));
            _scanView.Children.Add(col);
            rc.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(rc, 1);
            _scanView.Children.Add(rc);
            _view.Content = _scanView;

            _footText = Ui.Text("", 13, Ui.Gray);
            _go = Ui.Pill("清理", async (s, e) => await DoClean());
            _go.MinWidth = 150;

            var root = new Grid { Margin = new Thickness(32, 22, 32, 22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(head);
            var sv = new ScrollViewer { Content = _view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 18, 0, 14), Padding = new Thickness(0, 0, 4, 6) };
            Grid.SetRow(sv, 1); root.Children.Add(sv);
            var foot = Ui.Footer(_footText, _go);
            Grid.SetRow(foot, 2); root.Children.Add(foot);
            _footer = foot;
            Content = root;
        }
        readonly Grid _footer;

        static FrameworkElement PreviewArt()
        {
            var c = new Canvas { Width = 160, Height = 96 };
            c.Children.Add(new Border { Width = 64, Height = 82, CornerRadius = new CornerRadius(6), Background = Brushes.White, BorderBrush = Ui.Br("#DDE3EA"), BorderThickness = new Thickness(1) }.At(62, 6));
            for (int j = 0; j < 4; j++)
            {
                c.Children.Add(W.Done(9).At(72, 20 + j * 16));
                c.Children.Add(new Border { Width = 34 - j * 4, Height = 4, CornerRadius = new CornerRadius(2), Background = Ui.Br("#C9D3DE") }.At(86, 23 + j * 16));
            }
            c.Children.Add(new System.Windows.Shapes.Ellipse { Width = 40, Height = 40, Stroke = Ui.Br("#5B9BE0"), StrokeThickness = 4.5, Fill = new SolidColorBrush(Color.FromArgb(0xB0, 0xEA, 0xF3, 0xFD)) }.At(34, 34));
            c.Children.Add(new System.Windows.Shapes.Line { X1 = 38, Y1 = 70, X2 = 26, Y2 = 84, Stroke = Ui.Br("#5E6B7A"), StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            return new Viewbox { Width = 160, Height = 96, Child = c, HorizontalAlignment = HorizontalAlignment.Center };
        }

        public async void OnShown()
        {
            _footText.Text = SettingsPage.ScheduleText();
            if (!_scanned && !_busy) await Scan();
        }

        void SetProgress(double f)
        {
            _frac = f;
            Ui.SetBar(_progress, f);
            _pct.Text = $"{(int)(f * 100)}%";
        }

        public async Task Scan()
        {
            if (_busy) return;
            _busy = true; _go.IsEnabled = false; _rescan.IsEnabled = false;
            _view.Content = _scanView; _footer.Visibility = Visibility.Visible;
            _title.Text = "正在扫描你的电脑…";
            _sub.Text = "大概需要一分钟，扫描时可以照常用电脑。";
            _progress.Visibility = Visibility.Visible; _pct.Visibility = Visibility.Visible;
            SetProgress(0);
            foreach (var r in _rows.Values) { r.Status.Content = W.Status(W.Waiting(), "等待"); r.Size.Text = "—"; r.Size.Foreground = Ui.Gray; }
            _total.Text = "—"; _totalSub.Text = "正在统计";
            int i = 0;
            foreach (var c in _cats)
            {
                var r = _rows[c];
                r.Status.Content = W.Status(W.Spinner(), "扫描中…");
                r.Size.Foreground = Ui.Accent; r.Size.Text = "…";
                await Task.Run(() => c.Scan());
                r.Status.Content = W.Status(W.Done(), "已完成");
                r.Size.Text = c.Size == 0 ? "0" : Fmt.Size(c.Size);
                r.Size.Foreground = c.Size > 0 ? Ui.Green : Ui.Gray;
                i++; SetProgress((double)i / _cats.Count);
                Refresh(partial: true);
            }
            _scanned = true; _busy = false; _rescan.IsEnabled = true;
            _progress.Visibility = Visibility.Collapsed; _pct.Visibility = Visibility.Collapsed;
            Refresh();
        }

        void Refresh(bool partial = false)
        {
            long total = _cats.Where(c => c.Checked).Sum(c => c.Size);
            _total.Text = "≈ " + Fmt.Size(total);
            _totalSub.Text = partial ? "目前找到" : "已勾选的项目";
            if (!partial)
            {
                _title.Text = total > 0 ? $"找到 {Fmt.Size(total)} 可以清理" : "很干净，没什么要清理的";
                _sub.Text = UpdateLastText();
            }
            _go.Content = total > 0 ? "清理 " + Fmt.Size(total) : "清理";
            _go.IsEnabled = total > 0 && !_busy && _scanned;
            // mini bars: checked categories by size
            _bars.Children.Clear();
            var sizes = _cats.Where(c => c.Checked).Select(c => c.Size).OrderBy(x => x).ToList();
            long max = Math.Max(1, sizes.DefaultIfEmpty(0).Max());
            for (int k = 0; k < 8; k++)
            {
                double h = 12 + k * 8.5;
                _bars.Children.Add(new Border { Width = 7, Height = h, CornerRadius = new CornerRadius(2.5), Margin = new Thickness(2.5, 0, 2.5, 0), Background = Ui.GreenSoft, Opacity = 0.25 + k * 0.1, VerticalAlignment = VerticalAlignment.Bottom });
            }
        }

        static string UpdateLastText()
        {
            var last = History.LastCleanText();
            return last == null ? "勾选要清理的项目，点右下角清理。" : "上次清理：" + last;
        }

        async Task ShowPreview(IEnumerable<CleanCategory> cats)
        {
            var list = cats.ToList();
            if (list.Count == 0) return;
            var data = new List<(string, int, long, List<(string, long)>)>();
            foreach (var c in list)
            {
                var items = await Task.Run(() => c.Preview());
                data.Add((c.Name, c.IsRecycleBin ? 0 : Math.Max(c.Count, items.Count), c.Size, items));
            }
            new PreviewWindow("要删除的文件", data).ShowDialog();
        }

        async Task DoClean()
        {
            if (_busy) return;
            var picked = _cats.Where(c => c.Checked && c.Size > 0).ToList();
            if (picked.Count == 0) return;
            if (picked.Any(c => c.IsRecycleBin) && !Ui.Confirm("会清空回收站，清空后里面的文件找不回来。继续吗？")) return;
            var blocked = picked.Where(c => c.Blocked).ToList();
            _busy = true; _go.IsEnabled = false; _go.Content = "清理中…"; _rescan.IsEnabled = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long before = FreeC();
            var results = new List<(CleanCategory c, long freed, int files)>();
            foreach (var c in picked)
            {
                _rows[c].Status.Content = W.Status(W.Spinner(), "清理中…");
                var (f, n) = await Task.Run(() => c.Clean());
                results.Add((c, f, n));
                _rows[c].Status.Content = W.Status(W.Done(), "已清理");
            }
            sw.Stop();
            long freed = results.Sum(r => r.freed); int files = results.Sum(r => r.files);
            long after = FreeC();
            Store.Set("lastClean", DateTime.Now.ToString("o"));
            History.Add("扫描清理", "清理", files, freed, results.Where(r => r.freed > 0).Select(r => $"{r.c.Name}|{r.freed}"));
            _busy = false; _rescan.IsEnabled = true;
            ShowReport(results, freed, files, sw.Elapsed, before, after, blocked);
        }

        static long FreeC()
        {
            try { return new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory)).AvailableFreeSpace; } catch { return 0; }
        }

        void ShowReport(List<(CleanCategory c, long freed, int files)> results, long freed, int files, TimeSpan took, long before, long after, List<CleanCategory> blocked)
        {
            _title.Text = "清理完成";
            _sub.Text = $"用时 {Math.Max(1, (int)took.TotalSeconds)} 秒" + (blocked.Count > 0 ? $"，{string.Join("、", blocked.Select(b => b.Name))}因为软件开着已跳过" : "，正在使用的文件已自动跳过");
            _footer.Visibility = Visibility.Collapsed;

            var sp = new StackPanel();
            // hero
            var hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hero.Children.Add(W.Done(56).Also(d => d.Margin = new Thickness(6, 0, 22, 0)));
            var ht = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            ht.Children.Add(Ui.Text("电脑清爽多了", 22, Ui.Ink, FontWeights.SemiBold));
            var sub = before > 0 && after > 0 ? $"C 盘可用 {Fmt.Size(before)} → {Fmt.Size(after)}" : "";
            ht.Children.Add(Ui.Text(sub, 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 4, 0, 0)));
            Grid.SetColumn(ht, 1); hero.Children.Add(ht);
            var box = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            box.Children.Add(Ui.Text("本次释放", 12.5, Ui.Gray).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
            box.Children.Add(Ui.Text(Fmt.Size(freed), 30, Ui.Green, FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
            box.Children.Add(Ui.Text(files > 0 ? $"{files} 个文件" : "", 12, Ui.Gray).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
            var boxB = new Border { Background = Ui.Br("#EAF6EE"), CornerRadius = new CornerRadius(8), Padding = new Thickness(36, 12, 36, 12), Child = box };
            Grid.SetColumn(boxB, 2); hero.Children.Add(boxB);
            sp.Children.Add(Ui.Card(hero, new Thickness(20, 16, 16, 16)));

            sp.Children.Add(Ui.Section("清理明细", 22));
            var list = new StackPanel();
            bool first = true;
            foreach (var (c, f, n) in results)
            {
                if (!first) list.Children.Add(Ui.Divider());
                first = false;
                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(Ui.Text(f > 0 ? Fmt.Size(f) : "0", 14, f > 0 ? Ui.Green : Ui.Gray, FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 14, 0)));
                right.Children.Add(W.Done(16));
                var desc = c.Blocked ? "软件开着，这次跳过了" : (n > 0 ? $"{n} 个文件" : c.Desc);
                list.Children.Add(Ui.Row(Ui.Tile(c.Icon, c.Pal, 38), c.Name, desc, right));
            }
            sp.Children.Add(Ui.Card(list, new Thickness(18, 2, 18, 2)));

            var bar = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lk = Ui.Link("查看清理记录", (s, e) => MainWindow.Go("restore"));
            lk.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Children.Add(lk);
            var done = Ui.Pill("完成", async (s, e) => await Scan()); done.MinWidth = 140;
            Grid.SetColumn(done, 1); bar.Children.Add(done);
            sp.Children.Add(bar);
            sp.Children.Add(Ui.SafeNote("缓存和临时文件会被软件重新生成，所以直接删除；重复文件、卸载残留这类会先放进恢复中心，30 天内可以放回。").Also(n => n.Margin = new Thickness(0, 16, 0, 0)));
            _view.Content = sp;
        }
    }
}
