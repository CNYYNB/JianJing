using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace XinCleaner
{
    static class Fmt
    {
        public static string Size(long b)
        {
            if (b < 1024) return b + " B";
            double k = b / 1024.0;
            if (k < 1024) return k.ToString("0") + " KB";
            double m = k / 1024.0;
            if (m < 1024) return m.ToString(m < 10 ? "0.0" : "0") + " MB";
            return (m / 1024.0).ToString("0.0") + " GB";
        }
    }

    static class Paths
    {
        public static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        public static string AppDir
        {
            get { var d = Path.Combine(Local, "XinCleaner"); Directory.CreateDirectory(d); return d; }
        }

        // Chrome / Edge profile folders
        public static IEnumerable<string> ChromiumProfiles()
        {
            string[] roots =
            {
                Path.Combine(Local, "Google", "Chrome", "User Data"),
                Path.Combine(Local, "Microsoft", "Edge", "User Data"),
                Path.Combine(Local, "BraveSoftware", "Brave-Browser", "User Data"),
            };
            foreach (var r in roots)
            {
                if (!Directory.Exists(r)) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(r); } catch { continue; }
                foreach (var d in dirs)
                {
                    var n = Path.GetFileName(d);
                    if (n == "Default" || n.StartsWith("Profile ")) yield return d;
                }
            }
        }

        public static string BrowserName(string profile)
        {
            if (profile.Contains(@"\Google\Chrome\")) return "Chrome";
            if (profile.Contains(@"\Microsoft\Edge\")) return "Edge";
            if (profile.Contains(@"\Brave")) return "Brave";
            return "浏览器";
        }
    }

    static class Store
    {
        static readonly object L = new object();
        static Dictionary<string, string> _d;
        static string FilePath => Path.Combine(Paths.AppDir, "settings.json");

        static Dictionary<string, string> D
        {
            get
            {
                if (_d != null) return _d;
                try { _d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)); } catch { }
                return _d ??= new Dictionary<string, string>();
            }
        }

        public static string Get(string k, string def = null)
        {
            lock (L) return D.TryGetValue(k, out var v) ? v : def;
        }

        public static void Set(string k, string v)
        {
            lock (L)
            {
                D[k] = v;
                try { File.WriteAllText(FilePath, JsonSerializer.Serialize(D)); } catch { }
            }
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(Paths.AppDir, "log.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + Environment.NewLine); } catch { }
        }
    }

    static class Fs
    {
        static readonly EnumerationOptions Opt = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        public static IEnumerable<FileInfo> Files(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) yield break;
            IEnumerable<FileInfo> e;
            try { e = new DirectoryInfo(dir).EnumerateFiles("*", Opt); } catch { yield break; }
            using var it = e.GetEnumerator();
            while (true)
            {
                FileInfo f;
                try { if (!it.MoveNext()) break; f = it.Current; } catch { break; }
                yield return f;
            }
        }

        /// Prefer a readable description over a cryptic registry value name like "RtkAudUService".
        public static bool LooksBetterName(string current, string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return false;
            bool curCryptic = !current.Any(c => c > 0x2E80) && (current.Contains('_') || (current.Length > 12 && !current.Contains(' ')));
            bool descChinese = desc.Any(c => c > 0x2E80);
            return descChinese || curCryptic && desc.Contains(' ');
        }

        public static long DirSize(string dir)
        {
            long s = 0;
            foreach (var f in Files(dir)) { try { s += f.Length; } catch { } }
            return s;
        }

        /// Deletes contents of dir (not dir itself). Returns bytes freed.
        public static long EmptyDir(string dir, TimeSpan? minAge = null)
        {
            long freed = 0;
            if (Safety.IsSystem(dir)) return 0;
            var cutoff = DateTime.Now - (minAge ?? TimeSpan.Zero);
            foreach (var f in Files(dir).ToList())
            {
                try
                {
                    if (minAge != null && (f.LastWriteTime > cutoff || f.CreationTime > cutoff)) continue; // maybe still in use
                    if (Safety.IsSystem(f.FullName) || Safety.IsProtected(f.FullName)) continue;
                    long len = f.Length;
                    if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                    f.Delete();
                    freed += len;
                }
                catch { }
            }
            try
            {
                foreach (var d in Directory.GetDirectories(dir).OrderByDescending(x => x.Length))
                    TryDeleteEmptyTree(d);
            }
            catch { }
            return freed;
        }

        public static void RemoveEmptySubdirs(string dir)
        {
            try
            {
                if (!Directory.Exists(dir) || Safety.IsSystem(dir)) return;
                foreach (var d in Directory.GetDirectories(dir)) TryDeleteEmptyTree(d);
            }
            catch { }
        }

        static void TryDeleteEmptyTree(string d)
        {
            try
            {
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) return;
                foreach (var s in Directory.GetDirectories(d)) TryDeleteEmptyTree(s);
                if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
            }
            catch { }
        }

        public static long DeleteFile(string path)
        {
            try
            {
                var f = new FileInfo(path);
                if (!f.Exists) return 0;
                if (Safety.IsSystem(path) || Safety.IsProtected(path)) return 0;
                long len = f.Length;
                f.Attributes = FileAttributes.Normal;
                f.Delete();
                return len;
            }
            catch { return 0; }
        }

        public static bool ToRecycleBin(IEnumerable<string> paths)
        {
            var list = paths.Where(p => (File.Exists(p) || Directory.Exists(p)) && !Safety.IsSystem(p) && !Safety.IsProtected(p)).ToList();
            if (list.Count == 0) return true;
            var sb = new StringBuilder();
            foreach (var p in list) { sb.Append(p); sb.Append('\0'); }
            sb.Append('\0');
            var op = new Native.SHFILEOPSTRUCT
            {
                wFunc = Native.FO_DELETE,
                pFrom = sb.ToString(),
                fFlags = Native.FOF_ALLOWUNDO | Native.FOF_NOCONFIRMATION | Native.FOF_SILENT | Native.FOF_NOERRORUI,
            };
            return Native.SHFileOperation(ref op) == 0;
        }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public const ushort FO_DELETE = 3;
        public const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        public static long RecycleBinSize()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
        }

        public static void EmptyRecycleBin() => SHEmptyRecycleBin(IntPtr.Zero, null, 0x7);
    }

    static class Proc
    {
        public static (int code, string output) Run(string file, string args, int timeoutMs = 60000)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                var outTask = p.StandardOutput.ReadToEndAsync();
                p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return (-1, ""); }
                return (p.ExitCode, outTask.Result);
            }
            catch (Exception e) { return (-2, e.Message); }
        }

        public static string PowerShell(string script, int timeoutMs = 60000)
        {
            var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding=[Text.Encoding]::UTF8;" + script));
            var r = Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, timeoutMs);
            return r.code == 0 ? r.output.Trim() : null;
        }

        public static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
        }

        public static bool IsRunning(params string[] names) =>
            names.Any(n => { try { return Process.GetProcessesByName(n).Length > 0; } catch { return false; } });
    }
}
