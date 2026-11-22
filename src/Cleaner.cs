using System.IO;

namespace XinCleaner
{
    class CleanCategory
    {
        public string Key, Name, Desc, Icon = "folder";
        public (string bg, string fg) Pal = Ui.TBlue;
        public Func<IEnumerable<string>> Dirs = () => Array.Empty<string>();
        public Func<string, bool> FileFilter;
        public TimeSpan? MinAge;
        public bool IsRecycleBin, DefaultOn = true, AutoOk = true;
        public string[] SkipIfRunning;
        public long Size;
        public int Count;
        public bool Checked;

        IEnumerable<FileInfo> Candidates()
        {
            var cutoff = DateTime.Now - (MinAge ?? TimeSpan.Zero);
            foreach (var d in Dirs())
                foreach (var f in Fs.Files(d))
                {
                    bool ok;
                    try { ok = (MinAge == null || (f.LastWriteTime <= cutoff && f.CreationTime <= cutoff)) && (FileFilter == null || FileFilter(f.Name)); } catch { ok = false; }
                    if (ok && !Safety.IsProtected(f.FullName)) yield return f;
                }
        }

        public long Scan()
        {
            if (IsRecycleBin) { Count = 0; return Size = Native.RecycleBinSize(); }
            long s = 0; int n = 0;
            foreach (var f in Candidates()) { try { s += f.Length; n++; } catch { } }
            Count = n;
            return Size = s;
        }

        public List<(string path, long size)> Preview(int max = 300)
        {
            if (IsRecycleBin) return new();
            var l = new List<(string, long)>();
            foreach (var f in Candidates()) { try { l.Add((f.FullName, f.Length)); } catch { } }
            return l.OrderByDescending(x => x.Item2).Take(max).ToList();
        }

        public bool Blocked => SkipIfRunning != null && Proc.IsRunning(SkipIfRunning);

        public (long freed, int files) Clean()
        {
            if (IsRecycleBin)
            {
                long before = Native.RecycleBinSize();
                Native.EmptyRecycleBin();
                return (Math.Max(0, before - Native.RecycleBinSize()), 0);
            }
            if (Blocked) return (0, 0);
            long freed = 0; int n = 0;
            foreach (var f in Candidates().ToList())
            {
                var len = Fs.DeleteFile(f.FullName);
                if (len > 0) { freed += len; n++; }
            }
            foreach (var d in Dirs()) Fs.RemoveEmptySubdirs(d);
            return (freed, n);
        }
    }

    static class Cleaner
    {
        static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static List<CleanCategory> Categories()
        {
            var l = new List<CleanCategory>
            {
                new CleanCategory
                {
                    Key = "browser", Name = "浏览器缓存", Desc = "Edge、Chrome、Firefox 的网页缓存，不会退出登录", Icon = "globe", Pal = Ui.TBlue,
                    Dirs = BrowserCacheDirs,
                },
                new CleanCategory
                {
                    Key = "temp", Name = "临时文件", Desc = "系统和软件留下的临时文件，只删一天前的", Icon = "folder", Pal = Ui.TBlue,
                    MinAge = TimeSpan.FromDays(1),
                    Dirs = () => new[] { Path.GetTempPath(), Path.Combine(Paths.Windows, "Temp") }.Distinct(StringComparer.OrdinalIgnoreCase),
                },
                new CleanCategory
                {
                    Key = "logs", Name = "日志和错误报告", Desc = "程序崩溃转储和 Windows 错误报告", Icon = "file", Pal = Ui.TGreen,
                    MinAge = TimeSpan.FromDays(1),
                    Dirs = () => new[]
                    {
                        Path.Combine(Paths.Local, "CrashDumps"),
                        Path.Combine(Paths.Local, "Microsoft", "Windows", "WER"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER"),
                    },
                },
                new CleanCategory
                {
                    Key = "thumbs", Name = "缩略图缓存", Desc = "图片和视频的预览图，打开文件夹时会自动重新生成", Icon = "image", Pal = Ui.TPurple,
                    Dirs = () => new[] { Path.Combine(Paths.Local, "Microsoft", "Windows", "Explorer") },
                    FileFilter = n => n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase),
                },
                new CleanCategory
                {
                    Key = "update", Name = "Windows 更新残留", Desc = "已经装完的更新安装包，只删 10 天前的", Icon = "cube", Pal = Ui.TPurple,
                    MinAge = TimeSpan.FromDays(10),
                    Dirs = () => new[] { Path.Combine(Paths.Windows, "SoftwareDistribution", "Download") },
                },
                new CleanCategory
                {
                    Key = "chat", Name = "微信缓存", Desc = "微信的临时缓存，不含聊天记录和收到的文件。默认不勾，微信开着时跳过", Icon = "chat", Pal = Ui.TGreen,
                    DefaultOn = false, AutoOk = false, MinAge = TimeSpan.FromDays(3),
                    SkipIfRunning = new[] { "WeChat", "Weixin", "WeChatAppEx" },
                    Dirs = WeChatCacheDirs,
                },
                new CleanCategory
                {
                    Key = "recycle", Name = "回收站", Desc = "已经删到回收站的文件，清空后找不回", Icon = "trash", Pal = Ui.TOrange, IsRecycleBin = true, AutoOk = false,
                },
            };
            foreach (var c in l) c.Checked = Store.Get("cat." + c.Key, c.DefaultOn ? "1" : "0") == "1";
            return l;
        }

        static IEnumerable<string> BrowserCacheDirs()
        {
            foreach (var p in Paths.ChromiumProfiles())
            {
                yield return Path.Combine(p, "Cache");
                yield return Path.Combine(p, "Code Cache");
                yield return Path.Combine(p, "GPUCache");
            }
            var ff = Path.Combine(Paths.Local, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(ff))
            {
                string[] profs;
                try { profs = Directory.GetDirectories(ff); } catch { profs = Array.Empty<string>(); }
                foreach (var p in profs) yield return Path.Combine(p, "cache2");
            }
        }

        static IEnumerable<string> WeChatCacheDirs()
        {
            var roots = new[] { Path.Combine(Docs, "WeChat Files"), Path.Combine(Docs, "xwechat_files") };
            foreach (var r in roots)
            {
                if (!Directory.Exists(r)) continue;
                string[] accts;
                try { accts = Directory.GetDirectories(r); } catch { continue; }
                foreach (var a in accts)
                {
                    yield return Path.Combine(a, "FileStorage", "Cache");
                    yield return Path.Combine(a, "cache");
                }
            }
        }
    }

    static class AutoClean
    {
        public static void Run()
        {
            try { History.Purge(); } catch { }
            long freed = 0; int n = 0;
            var lines = new List<string>();
            foreach (var c in Cleaner.Categories().Where(c => c.AutoOk && c.Checked))
            {
                var (f, k) = c.Clean();
                freed += f; n += k;
                if (f > 0) lines.Add($"{c.Name}|{f}");
            }
            Store.Set("lastClean", DateTime.Now.ToString("o"));
            if (freed > 0) History.Add("自动维护", "定时清理", n, freed, lines);
            Store.Log("auto-clean freed " + Fmt.Size(freed));
        }
    }
}
