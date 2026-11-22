using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XinCleaner
{
    class Trace
    {
        public string Name, Desc;
        public Func<int> Count;      // items found
        public Func<int> Clear;      // items cleared
        public CheckBox Chk;
        public TextBlock CountText;
    }

    class TracesPage : UserControl, IPage
    {
        readonly TextBlock _big, _sub;
        readonly List<Trace> _traces;
        readonly Button _go;

        public TracesPage()
        {
            _traces = new List<Trace>
            {
                new Trace { Name = "最近打开的文件", Desc = "开始菜单和资源管理器里的「最近使用」", Count = () => RecentFiles().Count(), Clear = () => RecentFiles().Count(f => Fs.DeleteFile(f) > 0 || !File.Exists(f)) },
                new Trace { Name = "运行和地址栏记录", Desc = "Win+R 运行框、资源管理器地址栏输入过的内容", Count = () => MruCount(), Clear = ClearMru },
                new Trace { Name = "浏览器历史记录", Desc = "Edge、Chrome 的浏览记录。要先关掉浏览器；不会退出任何网站的登录，不删密码和书签", Count = () => BrowserHistory().Count(), Clear = ClearBrowserHistory },
                new Trace { Name = "缩略图缓存", Desc = "图片和视频预览图，下次打开文件夹会重新生成", Count = () => Thumbs().Count(), Clear = () => Thumbs().Count(f => Fs.DeleteFile(f) > 0) },
                new Trace { Name = "剪贴板", Desc = "当前复制的内容", Count = () => 1, Clear = () => { Application.Current.Dispatcher.Invoke(() => { try { Clipboard.Clear(); } catch { } }); return 1; } },
            };

            var header = Ui.Header("隐私清理", "正在检查…", out _big, out _sub);
            _sub.Text = "清掉别人能看到的使用记录。";
            var list = new StackPanel();
            list.Children.Add(Ui.Divider());
            foreach (var t in _traces)
            {
                t.Chk = Ui.Check(true);
                t.CountText = Ui.Text("…", 14, Ui.Gray);
                list.Children.Add(Ui.Row(t.Chk, t.Name, t.Desc, t.CountText));
                list.Children.Add(Ui.Divider());
            }
            _go = Ui.Pill("清除选中", async (s, e) => await DoClear());
            Content = Ui.Frame(header, list, Ui.Footer(null, Ui.Ghost("重新检查", async (s, e) => await Check()), _go));
        }

        public async void OnShown() => await Check();

        async Task Check()
        {
            int total = 0;
            foreach (var t in _traces)
            {
                int n = await Task.Run(() => { try { return t.Count(); } catch { return 0; } });
                t.CountText.Text = t.Name == "剪贴板" ? "" : n + " 项";
                total += t.Name == "剪贴板" ? 0 : n;
            }
            _big.Text = total > 0 ? $"{total} 条记录" : "很干净";
        }

        async Task DoClear()
        {
            var sel = _traces.Where(t => t.Chk.IsChecked == true).ToList();
            if (sel.Count == 0) return;
            if (sel.Any(t => t.Name == "浏览器历史记录") && Proc.IsRunning("chrome", "msedge", "brave"))
            {
                if (!Ui.Confirm("浏览器还开着，历史记录删不掉。\n先关掉浏览器再点「确定」，或点「取消」跳过这项。"))
                    sel = sel.Where(t => t.Name != "浏览器历史记录").ToList();
            }
            _go.IsEnabled = false;
            foreach (var t in sel) await Task.Run(() => { try { t.Clear(); } catch { } });
            _go.IsEnabled = true;
            History.Add("隐私清理", "隐私清理", 0, 0, sel.Select(t => t.Name + "|0"));
            await Check();
            _sub.Text = "已清除。";
        }

        static IEnumerable<string> RecentFiles()
        {
            var r = Path.Combine(Paths.Roaming, "Microsoft", "Windows", "Recent");
            return Fs.Files(r).Select(f => f.FullName).ToList();
        }

        static readonly string[] MruKeys =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery",
        };

        static int MruCount()
        {
            int n = 0;
            foreach (var p in MruKeys)
            {
                try { using var k = Registry.CurrentUser.OpenSubKey(p); if (k != null) n += k.GetValueNames().Count(v => v != "MRUList" && v != "MRUListEx"); } catch { }
            }
            return n;
        }

        static int ClearMru()
        {
            int n = 0;
            foreach (var p in MruKeys)
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(p, true);
                    if (k == null) continue;
                    foreach (var v in k.GetValueNames()) { k.DeleteValue(v, false); n++; }
                }
                catch { }
            }
            return n;
        }

        static IEnumerable<string> BrowserHistory()
        {
            foreach (var p in Paths.ChromiumProfiles())
                foreach (var f in new[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal" })
                {
                    var path = Path.Combine(p, f);
                    if (File.Exists(path)) yield return path;
                }
        }

        static int ClearBrowserHistory() => BrowserHistory().ToList().Count(f => Fs.DeleteFile(f) > 0);

        static IEnumerable<string> Thumbs()
        {
            var d = Path.Combine(Paths.Local, "Microsoft", "Windows", "Explorer");
            if (!Directory.Exists(d)) return Array.Empty<string>();
            try { return Directory.GetFiles(d, "thumbcache_*.db").Concat(Directory.GetFiles(d, "iconcache_*.db")).ToList(); }
            catch { return Array.Empty<string>(); }
        }
    }
}
