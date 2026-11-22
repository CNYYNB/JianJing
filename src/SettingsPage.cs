using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XinCleaner
{
    class SettingsPage : UserControl, IPage
    {
        const string TaskName = "XinCleaner AutoClean";
        readonly ComboBox _freq;
        readonly TextBlock _big, _sub;
        bool _loading;
        Button _updBtn;
        TextBlock _updDesc;

        async Task CheckUpdate()
        {
            _updBtn.IsEnabled = false;
            try
            {
                _updDesc.Text = "正在检查…";
                var r = await Updater.Check();
                if (r.Code <= AppInfo.Code) { _updDesc.Text = $"当前版本 {AppInfo.Name} · 已是最新"; return; }
                if (!Ui.Confirm($"有新版本 {r.Name}，现在更新吗？{Updater.Notes(r)}")) { _updDesc.Text = $"当前版本 {AppInfo.Name} · 可更新到 {r.Name}"; return; }
                var prog = new Progress<double>(p => _updDesc.Text = $"正在下载 {r.Name} · {p:P0}");
                await Updater.Install(r, prog);
            }
            catch (Exception ex) { _updDesc.Text = $"当前版本 {AppInfo.Name} · 检查失败：{ex.Message}"; }
            finally { _updBtn.IsEnabled = true; }
        }

        static readonly (string key, string label)[] Options =
        {
            ("off", "关闭"), ("daily", "每天 03:00"), ("weekly", "每周日 03:00"), ("monthly", "每月 1 号 03:00"),
        };

        public static string ScheduleText()
        {
            var k = Store.Get("schedule", "off");
            var o = Options.FirstOrDefault(x => x.key == k);
            return k == "off" || o.label == null ? "定时清理 · 关闭" : "定时清理 · " + o.label;
        }

        public SettingsPage()
        {
            var header = Ui.Header("设置", "", out _big, out _sub);
            _big.Visibility = Visibility.Collapsed; _sub.Visibility = Visibility.Collapsed;

            var list = new StackPanel();
            list.Children.Add(Ui.Divider());

            _freq = new ComboBox { Width = 170, Padding = new Thickness(10, 6, 10, 6) };
            foreach (var o in Options) _freq.Items.Add(o.label);
            _freq.SelectionChanged += (s, e) => { if (!_loading) ApplySchedule(); };
            list.Children.Add(Ui.Row(null, "定时清理", "自动清理扫描清理里勾选的缓存和临时文件，不动回收站和微信缓存。电脑要开着。", _freq));
            list.Children.Add(Ui.Divider());

            _wl = new StackPanel();
            list.Children.Add(Ui.Row(null, "白名单", "加进来的文件夹，简境任何功能都不会碰", Ui.Ghost("添加文件夹", (s, e) => AddWhite())));
            list.Children.Add(_wl);
            list.Children.Add(Ui.Divider());

            list.Children.Add(Ui.Row(null, "运行记录", "定时清理和出错记录", Ui.Ghost("打开文件夹", (s, e) => Proc.Open(Paths.AppDir))));
            list.Children.Add(Ui.Divider());

            _updBtn = Ui.Ghost("检查更新", async (s, e) => await CheckUpdate());
            list.Children.Add(Ui.Row(null, "更新", $"当前版本 {AppInfo.Name}", _updBtn, out _, out _updDesc));
            list.Children.Add(Ui.Divider());

            list.Children.Add(Ui.Row(null, "关于", $"简境 {AppInfo.Name} · 本地运行，没有广告、弹窗和捆绑", null));
            list.Children.Add(Ui.Divider());

            Content = Ui.Frame(header, list, null);
        }

        StackPanel _wl;

        void AddWhite()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择不让简境碰的文件夹" };
            if (dlg.ShowDialog() != true) return;
            Whitelist.Add(dlg.FolderName);
            RenderWhite();
        }

        void RenderWhite()
        {
            _wl.Children.Clear();
            foreach (var w in Whitelist.Items.ToList())
            {
                var p = w;
                var row = Ui.Cols((Ui.SmallIcon("folder", 16, Ui.Gray), Ui.Auto), (Ui.Text(w, 13, Ui.Ink).Also(t => { t.Margin = new Thickness(10, 0, 0, 0); t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }), Ui.Star()),
                                  (Ui.Link("移除", (s, e) => { Whitelist.Remove(p); RenderWhite(); }), Ui.Auto));
                row.Margin = new Thickness(0, 0, 0, 10);
                _wl.Children.Add(row);
            }
        }

        public void OnShown()
        {
            RenderWhite();
            _loading = true;
            var k = Store.Get("schedule", "off");
            var idx = Array.FindIndex(Options, o => o.key == k);
            _freq.SelectedIndex = idx < 0 ? 0 : idx;
            _loading = false;
        }

        void ApplySchedule()
        {
            var o = Options[Math.Max(0, _freq.SelectedIndex)];
            Proc.Run("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F");
            if (o.key == "off") { Store.Set("schedule", "off"); return; }

            // keep a stable copy of the exe so moving the download doesn't break the task
            string exe = Environment.ProcessPath;
            try
            {
                var dest = Path.Combine(Paths.AppDir, "XinCleaner.exe");
                if (!string.Equals(exe, dest, StringComparison.OrdinalIgnoreCase)) { File.Copy(exe, dest, true); exe = dest; }
            }
            catch { }

            string sched = o.key switch
            {
                "daily" => "/SC DAILY",
                "weekly" => "/SC WEEKLY /D SUN",
                _ => "/SC MONTHLY /D 1",
            };
            var tr = $"\\\"{exe}\\\" --auto-clean";
            var r = Proc.Run("schtasks.exe", $"/Create /TN \"{TaskName}\" /TR \"{tr}\" {sched} /ST 03:00 /RL HIGHEST /F");
            if (r.code == 0) Store.Set("schedule", o.key);
            else
            {
                Ui.Info("设置定时清理失败。");
                _loading = true; _freq.SelectedIndex = 0; _loading = false;
                Store.Set("schedule", "off");
            }
        }
    }
}
