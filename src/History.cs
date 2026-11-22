using System.IO;
using System.Text.Json;

namespace XinCleaner
{
    class VaultEntry { public string Orig { get; set; } public string Stored { get; set; } public long Size { get; set; } public bool IsDir { get; set; } public bool IsReg { get; set; } }

    class HistoryRecord
    {
        public string Id { get; set; }
        public DateTime Time { get; set; }
        public string Kind { get; set; }      // 扫描清理 / 重复文件 / 卸载残留 / 自动维护 / 文件粉碎 / 隐私清理 / 大文件
        public string Title { get; set; }
        public int Count { get; set; }
        public long Bytes { get; set; }
        public bool Restorable { get; set; }
        public bool Restored { get; set; }
        public bool Purged { get; set; }
        public List<VaultEntry> Items { get; set; } = new();
        public List<string> Lines { get; set; } = new(); // detail lines for report
        public bool CanRestore => Restorable && !Restored && !Purged;
    }

    /// Clean history + 30-day restore area.
    static class History
    {
        public const int KeepDays = 30;
        static readonly object L = new object();
        static string FilePath => Path.Combine(Paths.AppDir, "history.json");
        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { WriteIndented = false };

        public static List<HistoryRecord> All()
        {
            lock (L)
            {
                try { return JsonSerializer.Deserialize<List<HistoryRecord>>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); }
            }
        }

        static void Save(List<HistoryRecord> l)
        {
            lock (L)
            {
                try
                {
                    var tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(l.OrderByDescending(r => r.Time).Take(300).ToList(), Opt));
                    File.Move(tmp, FilePath, true);
                }
                catch { }
            }
        }

        public static HistoryRecord Add(string kind, string title, int count, long bytes, IEnumerable<string> lines = null)
        {
            var r = new HistoryRecord { Id = NewId(), Time = DateTime.Now, Kind = kind, Title = title, Count = count, Bytes = bytes, Lines = lines?.ToList() ?? new() };
            var l = All(); l.Add(r); Save(l);
            return r;
        }

        static string NewId() => DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);

        public static string LastCleanText()
        {
            var r = All().Where(x => x.Kind != "文件粉碎").OrderByDescending(x => x.Time).FirstOrDefault();
            return r == null ? null : $"{Ago(r.Time)}，{r.Kind} {Fmt.Size(r.Bytes)}";
        }

        public static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            if (d.TotalMinutes < 2) return "刚刚";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} 分钟前";
            if (t.Date == DateTime.Today) return "今天 " + t.ToString("HH:mm");
            if (t.Date == DateTime.Today.AddDays(-1)) return "昨天 " + t.ToString("HH:mm");
            if (d.TotalDays < 7) return $"{(int)Math.Ceiling(d.TotalDays)} 天前";
            return t.ToString("M月d日 HH:mm");
        }

        // ---------- restore area ----------
        static string VaultRoot(string path)
        {
            var sysRoot = Path.GetPathRoot(Paths.AppDir);
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.Equals(root, sysRoot, StringComparison.OrdinalIgnoreCase)) return Path.Combine(Paths.AppDir, "Restore");
            var d = Path.Combine(root, ".JianJingRestore");
            try
            {
                if (!Directory.Exists(d)) { var di = Directory.CreateDirectory(d); di.Attributes |= FileAttributes.Hidden; }
                return d;
            }
            catch { return Path.Combine(Paths.AppDir, "Restore"); }
        }

        /// Move files/folders (and HKCU registry keys, prefixed "HKCU\") into the restore area; records history. Returns the record.
        public static HistoryRecord MoveToVault(string kind, string title, IEnumerable<string> paths)
        {
            var id = NewId();
            var rec = new HistoryRecord { Id = id, Time = DateTime.Now, Kind = kind, Title = title, Restorable = true };
            int n = 0;
            foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (p.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
                    {
                        var dir = Path.Combine(Paths.AppDir, "Restore", id); Directory.CreateDirectory(dir);
                        var file = Path.Combine(dir, $"{n++}.reg");
                        var r = Proc.Run("reg.exe", $"export \"{p}\" \"{file}\" /y", 30000);
                        if (r.code != 0) continue;
                        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(p.Substring(5), false);
                        rec.Items.Add(new VaultEntry { Orig = p, Stored = file, IsReg = true });
                        continue;
                    }
                    if (Safety.IsSystem(p) || Safety.IsProtected(p)) continue;
                    bool isDir = Directory.Exists(p);
                    if (!isDir && !File.Exists(p)) continue;
                    long size = isDir ? Fs.DirSize(p) : new FileInfo(p).Length;
                    var vdir = Path.Combine(VaultRoot(p), id); Directory.CreateDirectory(vdir);
                    var dest = Path.Combine(vdir, $"{n++}_{Path.GetFileName(p.TrimEnd('\\'))}");
                    if (isDir) MoveDir(p, dest);
                    else { File.SetAttributes(p, FileAttributes.Normal); File.Move(p, dest); }
                    rec.Items.Add(new VaultEntry { Orig = p, Stored = dest, Size = size, IsDir = isDir });
                    rec.Bytes += size;
                }
                catch (Exception ex) { Store.Log("vault move failed " + p + ": " + ex.Message); }
            }
            rec.Count = rec.Items.Count;
            if (rec.Count > 0) { var l = All(); l.Add(rec); Save(l); }
            return rec;
        }

        static void MoveDir(string src, string dest)
        {
            try { Directory.Move(src, dest); return; } catch (IOException) when (!SameRoot(src, dest)) { }
            // cross-volume fallback
            CopyDir(src, dest);
            Directory.Delete(src, true);
        }

        static bool SameRoot(string a, string b) => string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);

        static void CopyDir(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
        }

        /// Put everything back. Returns (restored, failed).
        public static (int ok, int fail) Restore(string id)
        {
            var l = All();
            var rec = l.FirstOrDefault(r => r.Id == id);
            if (rec == null || !rec.CanRestore) return (0, 0);
            int ok = 0, fail = 0;
            foreach (var e in rec.Items)
            {
                try
                {
                    if (e.IsReg)
                    {
                        var r = Proc.Run("reg.exe", $"import \"{e.Stored}\"", 30000);
                        if (r.code == 0) ok++; else fail++;
                        continue;
                    }
                    var target = e.Orig;
                    if (File.Exists(target) || Directory.Exists(target))
                    {
                        var dir = Path.GetDirectoryName(target);
                        var name = Path.GetFileNameWithoutExtension(target); var ext = e.IsDir ? "" : Path.GetExtension(target);
                        target = Path.Combine(dir, name + " (恢复)" + ext);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if (e.IsDir) MoveDir(e.Stored, target); else File.Move(e.Stored, target);
                    ok++;
                }
                catch (Exception ex) { fail++; Store.Log("restore failed " + e.Orig + ": " + ex.Message); }
            }
            rec.Restored = true;
            Save(l);
            CleanupDirs(id);
            return (ok, fail);
        }

        static void CleanupDirs(string id)
        {
            foreach (var root in VaultRoots())
            {
                var d = Path.Combine(root, id);
                try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
            }
        }

        static IEnumerable<string> VaultRoots()
        {
            yield return Path.Combine(Paths.AppDir, "Restore");
            foreach (var di in DriveInfo.GetDrives())
            {
                string d = null;
                try { if (di.DriveType == DriveType.Fixed) d = Path.Combine(di.RootDirectory.FullName, ".JianJingRestore"); } catch { }
                if (d != null && Directory.Exists(d)) yield return d;
            }
        }

        public static long VaultSize() => All().Where(r => r.CanRestore).Sum(r => r.Items.Sum(i => i.Size));

        /// Permanently remove restore data older than 30 days.
        public static void Purge()
        {
            var l = All(); bool changed = false;
            foreach (var r in l.Where(r => r.Restorable && !r.Restored && !r.Purged && (DateTime.Now - r.Time).TotalDays > KeepDays))
            {
                CleanupDirs(r.Id); r.Purged = true; changed = true;
            }
            if (changed) Save(l);
        }
    }
}
