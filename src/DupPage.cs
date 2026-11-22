using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XinCleaner
{
    class DupPage : UserControl, IPage
    {
        readonly TextBlock _big, _sub, _foot;
        readonly StackPanel _list = new StackPanel();
        readonly Button _del, _pick;
        readonly List<(string path, CheckBox chk, long size)> _items = new();
        string _folder; // null = auto
        bool _busy, _scanned;

        public DupPage()
        {
            var header = Ui.Header("重复文件", "正在查找…", out _big, out _sub);
            _sub.Text = "按内容比对，不看文件名。删掉的文件先放进恢复中心，30 天内能放回。";
            _foot = Ui.Text("", 13, Ui.Gray);
            _pick = Ui.Ghost("换个文件夹", async (s, e) => await Pick());
            _del = Ui.Pill("删除选中", (s, e) => Delete());
            _del.IsEnabled = false;
            Content = Ui.Frame(header, _list, Ui.Footer(_foot, _pick, _del));
        }

        public async void OnShown()
        {
            if (_scanned || _busy) return;
            _scanned = true;
            await ScanFolder();
        }

        async Task Pick()
        {
            if (_busy) return;
            var dlg = new OpenFolderDialog { Title = "选择要查找重复文件的文件夹" };
            if (dlg.ShowDialog() != true) return;
            _folder = dlg.FolderName;
            await ScanFolder();
        }

        async Task ScanFolder()
        {
            _busy = true; _del.IsEnabled = false; _pick.IsEnabled = false;
            _list.Children.Clear(); _items.Clear();
            _big.Text = "正在比对…";
            _foot.Text = _folder ?? "桌面、文档、下载、图片、视频、音乐和其他磁盘";
            var progress = new Progress<string>(m => _sub.Text = m);
            var groups = await Task.Run(() => FindDuplicates(_folder, progress));
            _foot.Text = (_folder ?? "桌面、文档、下载、图片、视频、音乐和其他磁盘") + " · 已跳过系统和软件文件夹";
            _busy = false; _pick.IsEnabled = true;

            if (groups.Count == 0)
            {
                _big.Text = "没有重复文件";
                _sub.Text = "按内容比对，不看文件名。";
                return;
            }

            long waste = groups.Sum(g => g[0].Length * (g.Count - 1));
            _big.Text = $"{groups.Count} 组重复，可省 {Fmt.Size(waste)}";
            _sub.Text = "每组保留最早的那个，其余已勾选。删除的文件会进回收站。";

            foreach (var g in groups.OrderByDescending(g => g[0].Length * (g.Count - 1)).Take(300))
            {
                var head = Ui.Text($"{g[0].Name} · {Fmt.Size(g[0].Length)} × {g.Count}", 13, Ui.Gray, FontWeights.SemiBold);
                head.Margin = new Thickness(0, 18, 0, 4);
                _list.Children.Add(head);
                _list.Children.Add(Ui.Divider());
                for (int i = 0; i < g.Count; i++)
                {
                    var f = g[i];
                    var chk = Ui.Check(i > 0);
                    chk.Checked += (s, e) => UpdateFoot();
                    chk.Unchecked += (s, e) => UpdateFoot();
                    var open = Ui.Ghost("位置");
                    open.Click += (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + f.FullName + "\""); } catch { } };
                    var row = Ui.Row(chk, f.Name, f.DirectoryName + (i == 0 ? "  （保留）" : ""), open);
                    _list.Children.Add(row);
                    _list.Children.Add(Ui.Divider());
                    _items.Add((f.FullName, chk, f.Length));
                }
            }
            UpdateFoot();
        }

        void UpdateFoot()
        {
            var sel = _items.Where(i => i.chk.IsChecked == true).ToList();
            _del.IsEnabled = sel.Count > 0 && !_busy;
            _del.Content = sel.Count > 0 ? $"删除 {sel.Count} 个（{Fmt.Size(sel.Sum(i => i.size))}）" : "删除选中";
        }

        void Delete()
        {
            var sel = _items.Where(i => i.chk.IsChecked == true).Select(i => i.path).ToList();
            if (sel.Count == 0) return;
            if (!Ui.Confirm($"删除 {sel.Count} 个重复文件？\n它们会先放进恢复中心，30 天内可以一键放回原处。")) return;
            History.MoveToVault("重复文件", "重复文件", sel);
            _ = ScanFolder();
        }

        static readonly string[] SkipNames =
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData", "$Recycle.Bin", "System Volume Information",
            "Recovery", "Boot", "EFI", "$WinREAgent", "PerfLogs", "WindowsApps", "steamapps", "SteamLibrary", "Steam", "Epic Games",
            "Ubisoft", "Ubisoft Game Launcher", "EA Games", "Origin Games", "Rockstar Games", "Riot Games", "WeGameApps", "Tencent",
            "node_modules", ".git", ".vs", "XboxGames", "MSOCache", "Config.Msi", "Intel", "AMD", "NVIDIA",
        };

        static IEnumerable<string> AutoRoots()
        {
            var sysDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var list = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(user, "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            };
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                    if (d.DriveType == DriveType.Fixed && d.IsReady && !string.Equals(d.Name, sysDrive, StringComparison.OrdinalIgnoreCase)) list.Add(d.Name);
            }
            catch { }
            return list.Where(x => !string.IsNullOrEmpty(x) && Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        // walks a folder, skipping system folders, game/program folders (anything holding .dll files) and hidden/system files
        static IEnumerable<FileInfo> Walk(string root)
        {
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                if (Safety.IsProgram(dir)) continue;
                FileInfo[] files; DirectoryInfo[] subs;
                try
                {
                    var di = new DirectoryInfo(dir);
                    if (dir != root && (di.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden)) != 0) continue;
                    files = di.GetFiles(); subs = di.GetDirectories();
                }
                catch { continue; }
                if (root.Length <= 3 && files.Any(f => f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".sys", StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var f in files)
                    if ((f.Attributes & (FileAttributes.System | FileAttributes.Hidden)) == 0) yield return f;
                foreach (var sd in subs)
                    if (!SkipNames.Contains(sd.Name, StringComparer.OrdinalIgnoreCase)) stack.Push(sd.FullName);
            }
        }

        static List<List<FileInfo>> FindDuplicates(string root, IProgress<string> progress)
        {
            var bySize = new Dictionary<long, List<FileInfo>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int n = 0;
            long minLen = root == null ? 100 * 1024 : 1024;
            foreach (var f in (root == null ? AutoRoots() : new[] { root }).SelectMany(Walk))
            {
                if (!seen.Add(f.FullName)) continue;
                long len;
                try { len = f.Length; } catch { continue; }
                if (len < minLen) continue;
                if (!bySize.TryGetValue(len, out var l)) bySize[len] = l = new List<FileInfo>();
                l.Add(f);
                if (++n % 2000 == 0) progress.Report($"已读取 {n} 个文件…");
            }
            var result = new List<List<FileInfo>>();
            var candidates = bySize.Values.Where(l => l.Count > 1).ToList();
            int done = 0;
            foreach (var group in candidates)
            {
                if (++done % 50 == 0) progress.Report($"正在比对 {done}/{candidates.Count} …");
                foreach (var g1 in group.GroupBy(f => Hash(f.FullName, 65536)).Where(g => g.Key != null && g.Count() > 1))
                {
                    var list = g1.ToList();
                    var full = list[0].Length > 65536
                        ? list.GroupBy(f => Hash(f.FullName, long.MaxValue)).Where(g => g.Key != null && g.Count() > 1).Select(g => g.ToList())
                        : new[] { list };
                    var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    result.AddRange(full.Select(g => g.OrderBy(f => f.FullName.StartsWith(dl, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                        .ThenBy(f => { try { return f.CreationTime; } catch { return DateTime.MaxValue; } }).ToList()));
                }
            }
            return result;
        }

        static string Hash(string path, long limit)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
                using var sha = SHA256.Create();
                var buf = new byte[1 << 16];
                long left = limit;
                int r;
                while (left > 0 && (r = fs.Read(buf, 0, (int)Math.Min(buf.Length, left))) > 0)
                {
                    sha.TransformBlock(buf, 0, r, null, 0);
                    left -= r;
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Convert.ToHexString(sha.Hash);
            }
            catch { return null; }
        }
    }
}
