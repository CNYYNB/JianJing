using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XinCleaner
{
    class ShredPage : UserControl, IPage
    {
        readonly TextBlock _big, _sub;
        readonly StackPanel _list = new StackPanel();
        readonly List<string> _files = new();
        readonly Button _go;

        public ShredPage()
        {
            var header = Ui.Header("文件粉碎", "把文件拖进来", out _big, out _sub);
            _sub.Text = "先用随机数据覆盖再删除，不进回收站，找不回来。固态硬盘上无法保证百分之百不可恢复。";
            _go = Ui.Pill("粉碎", async (s, e) => await Shred());
            _go.IsEnabled = false;
            var add = Ui.Ghost("添加文件", (s, e) => AddDialog());
            var clear = Ui.Ghost("清空列表", (s, e) => { _files.Clear(); Render(); });
            AllowDrop = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Drop += (s, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) { AddPaths(paths); }
            };
            Content = Ui.Frame(header, _list, Ui.Footer(null, add, clear, _go));
        }

        public void OnShown() { }

        void AddDialog()
        {
            var dlg = new OpenFileDialog { Multiselect = true, Title = "选择要粉碎的文件" };
            if (dlg.ShowDialog() == true) AddPaths(dlg.FileNames);
        }

        void AddPaths(IEnumerable<string> paths)
        {
            int blocked = 0;
            foreach (var p in paths)
            {
                if (File.Exists(p)) { if (!Safety.CanDeleteFile(p)) blocked++; else if (!_files.Contains(p)) _files.Add(p); }
                else if (Directory.Exists(p))
                {
                    if (Safety.IsSystem(p) || Safety.IsProgram(p)) { blocked++; continue; }
                    foreach (var f in Fs.Files(p)) { if (!Safety.CanDeleteFile(f.FullName)) blocked++; else if (!_files.Contains(f.FullName)) _files.Add(f.FullName); }
                }
            }
            Render();
            if (blocked > 0) Ui.Info($"有 {blocked} 个是系统文件或软件自身的文件，已跳过，粉碎了会影响电脑或软件使用。");
        }

        void Render()
        {
            _list.Children.Clear();
            if (_files.Count == 0)
            {
                _big.Text = "把文件拖进来";
                _go.IsEnabled = false; _go.Content = "粉碎";
                return;
            }
            long total = _files.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            _big.Text = $"{_files.Count} 个文件 · {Fmt.Size(total)}";
            _go.IsEnabled = true; _go.Content = $"粉碎 {_files.Count} 个";
            _list.Children.Add(Ui.Divider());
            foreach (var f in _files.Take(500))
            {
                var path = f;
                var rm = Ui.Ghost("移除", (s, e) => { _files.Remove(path); Render(); });
                _list.Children.Add(Ui.Row(null, Path.GetFileName(f), Path.GetDirectoryName(f), rm));
                _list.Children.Add(Ui.Divider());
            }
        }

        async Task Shred()
        {
            if (_files.Count == 0) return;
            if (!Ui.Confirm($"确定粉碎这 {_files.Count} 个文件？\n粉碎后无法恢复。")) return;
            _go.IsEnabled = false; _go.Content = "粉碎中…";
            var todo = _files.ToList();
            int ok = 0;
            var failed = new List<string>();
            await Task.Run(() =>
            {
                foreach (var f in todo) { if (ShredFile(f)) ok++; else failed.Add(f); }
            });
            if (ok > 0) History.Add("文件粉碎", "文件粉碎", ok, 0, todo.Except(failed).Select(f => f + "|0"));
            _files.Clear(); _files.AddRange(failed);
            Render();
            _sub.Text = failed.Count == 0 ? $"已粉碎 {ok} 个文件。" : $"已粉碎 {ok} 个，{failed.Count} 个正在被使用，先关掉相关程序再试。";
        }

        static bool ShredFile(string path)
        {
            if (!Safety.CanDeleteFile(path)) return false;
            try
            {
                var fi = new FileInfo(path);
                fi.Attributes = FileAttributes.Normal;
                long len = fi.Length;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.WriteThrough))
                {
                    var buf = new byte[1 << 20];
                    long left = len;
                    while (left > 0)
                    {
                        RandomNumberGenerator.Fill(buf);
                        int n = (int)Math.Min(buf.Length, left);
                        fs.Write(buf, 0, n);
                        left -= n;
                    }
                    fs.Flush(true);
                    fs.SetLength(0);
                }
                var dir = Path.GetDirectoryName(path);
                var tmp = Path.Combine(dir, Guid.NewGuid().ToString("N").Substring(0, 12));
                File.Move(path, tmp);
                File.Delete(tmp);
                return true;
            }
            catch { return false; }
        }
    }
}
