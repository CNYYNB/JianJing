using System.Windows;
using System.Windows.Controls;

namespace XinCleaner
{
    class RestorePage : UserControl, IPage
    {
        readonly ContentControl _top = new ContentControl();
        readonly StackPanel _list = new StackPanel();
        readonly TextBlock _usage;

        public RestorePage()
        {
            var head = new StackPanel();
            head.Children.Add(Ui.Title("恢复中心"));
            head.Children.Add(Ui.Text("每次清理都有记录。重复文件、卸载残留这类删掉的东西 30 天内可以一键放回原处。", 13, Ui.Gray).Also(t => t.Margin = new Thickness(0, 4, 0, 0)));

            var body = new StackPanel();
            body.Children.Add(_top);
            var sec = new Grid { Margin = new Thickness(0, 22, 0, 8) };
            sec.Children.Add(Ui.Text("清理记录", 14, Ui.Ink, FontWeights.SemiBold));
            _usage = Ui.Text("", 12.5, Ui.Gray); _usage.HorizontalAlignment = HorizontalAlignment.Right;
            sec.Children.Add(_usage);
            body.Children.Add(sec);
            body.Children.Add(Ui.Card(_list, new Thickness(18, 2, 18, 2)));
            body.Children.Add(Ui.SafeNote("缓存、临时文件、回收站、文件粉碎和隐私清理是直接删除的，删除前都会先让你确认。").Also(n => n.Margin = new Thickness(0, 14, 0, 0)));
            Content = Ui.Frame(head, body, null, card: false);
        }

        public void OnShown()
        {
            try { History.Purge(); } catch { }
            Render();
        }

        static string IconFor(string kind) => kind switch
        {
            "扫描清理" => "scan", "重复文件" => "copy", "卸载残留" => "folder", "自动维护" => "history", "文件粉碎" => "fire", "隐私清理" => "lock", _ => "file",
        };

        static (string, string) PalFor(string kind) => kind switch
        {
            "扫描清理" => Ui.TBlue, "重复文件" => Ui.TPurple, "卸载残留" => Ui.TOrange, "自动维护" => Ui.TGreen, "文件粉碎" => Ui.TRed, _ => Ui.TGray,
        };

        static string Desc(HistoryRecord r)
        {
            var parts = new List<string> { History.Ago(r.Time) };
            if (r.Count > 0) parts.Add(r.Kind == "卸载残留" ? $"{r.Count} 项" : $"{r.Count} 个文件");
            if (r.Bytes > 0) parts.Add(Fmt.Size(r.Bytes));
            if (!string.IsNullOrEmpty(r.Title) && r.Title != r.Kind && r.Title != "清理") parts.Insert(0, r.Title);
            return string.Join(" · ", parts);
        }

        void Render()
        {
            var all = History.All().OrderByDescending(r => r.Time).ToList();
            var latest = all.FirstOrDefault(r => r.CanRestore);
            if (latest != null)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(Ui.Tile("restore", Ui.TBlue, 48).Also(t => t.Margin = new Thickness(0, 0, 16, 0)));
                var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                t.Children.Add(Ui.Text("最近一次：" + History.Ago(latest.Time), 17, Ui.Ink, FontWeights.SemiBold));
                t.Children.Add(Ui.Text(latest.Kind + " · " + Desc(latest), 13, Ui.Gray).Also(x => x.Margin = new Thickness(0, 3, 0, 0)));
                Grid.SetColumn(t, 1); g.Children.Add(t);
                var b = Ui.IconButton("restore", "撤销这次清理", true, (s, e) => DoRestore(latest));
                b.MinWidth = 150;
                Grid.SetColumn(b, 2); g.Children.Add(b);
                _top.Content = Ui.Card(g, new Thickness(18, 16, 18, 16));
            }
            else _top.Content = Ui.Card(Ui.Text("现在没有需要恢复的内容。", 13.5, Ui.Gray), new Thickness(18, 18, 18, 18));

            var vs = History.VaultSize();
            _usage.Text = (vs > 0 ? $"恢复区占用 {Fmt.Size(vs)} · " : "") + "超过 30 天自动清空";

            _list.Children.Clear();
            if (all.Count == 0) { _list.Children.Add(Ui.Text("还没有清理记录", 13, Ui.Gray).Also(x => x.Margin = new Thickness(0, 16, 0, 16))); return; }
            bool first = true;
            foreach (var r in all.Take(60))
            {
                if (!first) _list.Children.Add(Ui.Divider());
                first = false;
                var (label, fg, bg) = r.Restorable
                    ? (r.Restored ? ("已恢复", Ui.Gray, "#F0F1F3") : r.Purged ? ("已过期", Ui.Gray, "#F0F1F3") : ("可恢复", Ui.Green, "#E5F4EA"))
                    : ("直接删除", Ui.Gray, "#F0F1F3");
                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(Ui.Chip(label, fg, bg).Also(c => c.Margin = new Thickness(0, 0, 18, 0)));
                var rec = r;
                right.Children.Add(Ui.Link("查看明细", (s, e) => ShowDetail(rec)).Also(l => l.Margin = new Thickness(0, 0, 14, 0)));
                var rb = Ui.IconButton("restore", "恢复", false, (s, e) => DoRestore(rec));
                rb.Visibility = r.CanRestore ? Visibility.Visible : Visibility.Hidden;
                right.Children.Add(rb);
                _list.Children.Add(Ui.Row(Ui.Tile(IconFor(r.Kind), r.CanRestore ? Ui.TBlue : Ui.TGray, 38), r.Kind, Desc(r), right));
            }
        }

        void DoRestore(HistoryRecord r)
        {
            if (!Ui.Confirm($"把这次「{r.Kind}」删掉的 {r.Count} 项放回原来的位置？")) return;
            var (ok, fail) = History.Restore(r.Id);
            Ui.Info(fail == 0 ? $"已放回 {ok} 项。" : $"已放回 {ok} 项，{fail} 项没能放回（原位置可能被占用或没有权限）。");
            Render();
        }

        static void ShowDetail(HistoryRecord r)
        {
            var items = new List<(string, long)>();
            if (r.Items.Count > 0) items = r.Items.Select(i => (i.Orig, i.Size)).ToList();
            else foreach (var l in r.Lines)
                {
                    var p = l.Split('|');
                    items.Add((p[0], p.Length > 1 && long.TryParse(p[1], out var b) ? b : 0));
                }
            var data = new List<(string, int, long, List<(string, long)>)> { (r.Kind + " · " + r.Time.ToString("yyyy-MM-dd HH:mm"), r.Count, r.Bytes, items) };
            var w = new PreviewWindow("清理明细", data, r.Items.Count > 0 ? "这次处理的文件和文件夹，以及它们原来的位置。" : "这次清理的类别和释放的空间。");
            w.ShowDialog();
        }
    }
}
