using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace XinCleaner
{
    class StartupItem
    {
        public string Name, Command, Where; // Where: "HKCU", "HKLM", "HKLM32", "Folder:<path>"
        public string Id => Where + "|" + Name;
    }

    class ProtectPage : UserControl, IPage
    {
        readonly TextBlock _big, _sub;
        readonly StackPanel _list = new StackPanel();
        readonly Button _scan;
        bool _busy;

        public ProtectPage()
        {
            var header = Ui.Header("防护", "正在检查…", out _big, out _sub);
            _scan = Ui.Pill("快速扫描", async (s, e) => await QuickScan());
            var full = Ui.Ghost("打开 Windows 安全中心", (s, e) => Proc.Open("windowsdefender:"));
            Content = Ui.Frame(header, _list, Ui.Footer(null, full, _scan));
        }

        public async void OnShown() => await Check();

        async Task Check()
        {
            if (_busy) return;
            _busy = true;
            _big.Text = "正在检查…"; _sub.Text = "";
            _list.Children.Clear();
            _list.Children.Add(Ui.Divider());

            int problems = 0;

            // Defender
            var def = await Task.Run(DefenderStatus);
            problems += AddStatus("Windows Defender", def.desc, def.text, def.ok);

            // Firewall
            var fw = await Task.Run(FirewallStatus);
            problems += AddStatus("防火墙", "公用、专用、域三个网络都要开着", fw.text, fw.ok);

            // Homepage
            var hp = await Task.Run(HomepageCheck);
            Button accept = null;
            if (!hp.ok && hp.changed)
                accept = Ui.Ghost("是我改的", (s, e) => { SaveHomepageBaseline(); _ = Check(); });
            problems += AddStatus("浏览器主页", hp.desc, hp.text, hp.ok, accept);

            // hosts
            var hs = await Task.Run(HostsCheck);
            var openHosts = Ui.Ghost("查看");
            openHosts.Click += (s, e) => { try { System.Diagnostics.Process.Start("notepad.exe", HostsPath); } catch { } };
            problems += AddStatus("hosts 文件", "被改了可能导致网站被劫持", hs.text, hs.ok, hs.ok ? null : openHosts);

            // Startup
            var items = await Task.Run(ReadStartup);
            var baseline = LoadSet("startupBaseline");
            var added = baseline == null ? new List<StartupItem>() : items.Where(i => !baseline.Contains(i.Id)).ToList();
            if (baseline == null) SaveSet("startupBaseline", items.Select(i => i.Id));
            var stText = added.Count == 0 ? $"{items.Count} 个，无新增" : $"新增 {added.Count} 个";
            Button okNew = added.Count > 0 ? Ui.Ghost("都是我装的", (s, e) => { SaveSet("startupBaseline", items.Select(i => i.Id)); _ = Check(); }) : null;
            problems += AddStatus("启动项", added.Count > 0 ? "新增：" + string.Join("、", added.Select(a => a.Name)) : "开机自动运行的程序", stText, added.Count == 0, okNew);

            var link = Ui.Link("开机启动的程序在「启动管理」里管理", (s, e) => MainWindow.Go("startup"));
            link.Margin = new Thickness(0, 14, 0, 6); link.HorizontalAlignment = HorizontalAlignment.Left;
            _list.Children.Add(link);

            _big.Text = problems == 0 ? "一切正常" : $"{problems} 项需要注意";
            _sub.Text = "杀毒用的是 Windows 自带的 Defender，本软件负责检查和提醒。";
            _busy = false;
        }

        int AddStatus(string name, string desc, string status, bool ok, Button action = null)
        {
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            if (action != null) { action.Margin = new Thickness(0, 0, 14, 0); right.Children.Add(action); }
            right.Children.Add(Ui.Text(status, 14, ok ? Ui.Gray : Ui.Red));
            right.Children.Add(Ui.Dot(ok ? Ui.Green : Ui.Red));
            _list.Children.Add(Ui.Row(null, name, desc, right));
            _list.Children.Add(Ui.Divider());
            return ok ? 0 : 1;
        }

        async Task QuickScan()
        {
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
            if (!File.Exists(exe)) { Ui.Info("没找到 Windows Defender，可能装了别的杀毒软件。"); return; }
            _scan.IsEnabled = false; _scan.Content = "扫描中…";
            _sub.Text = "快速扫描一般要几分钟，可以先做别的。";
            var r = await Task.Run(() => Proc.Run(exe, "-Scan -ScanType 1", 60 * 60 * 1000));
            _scan.IsEnabled = true; _scan.Content = "快速扫描";
            if (r.code == 0) { _sub.Text = "快速扫描完成，没有发现威胁。"; Store.Set("lastScan", DateTime.Now.ToString("o")); }
            else if (r.code == 2)
            {
                _sub.Text = "发现威胁，已交给 Windows 安全中心处理。";
                if (Ui.Confirm("扫描发现了威胁。打开 Windows 安全中心查看和处理？")) Proc.Open("windowsdefender://threat");
            }
            else _sub.Text = "扫描没能完成，可以在 Windows 安全中心里手动扫描。";
        }

        // ---------- Defender / firewall ----------
        static (bool ok, string text, string desc) DefenderStatus()
        {
            var json = Proc.PowerShell("Get-MpComputerStatus | Select-Object AntivirusEnabled,RealTimeProtectionEnabled,AntivirusSignatureAge | ConvertTo-Json -Compress");
            if (string.IsNullOrEmpty(json)) return (true, "未检测到", "可能装了别的杀毒软件，那也没问题");
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                bool av = r.GetProperty("AntivirusEnabled").GetBoolean();
                bool rt = r.GetProperty("RealTimeProtectionEnabled").GetBoolean();
                int age = r.TryGetProperty("AntivirusSignatureAge", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : 0;
                var desc = age > 1000 ? "病毒库" : $"病毒库 {(age == 0 ? "今天" : age + " 天前")}更新";
                if (!av) return (false, "已关闭", desc);
                if (!rt) return (false, "实时保护已关闭", desc);
                if (age > 7 && age < 1000) return (false, "病毒库过旧", desc);
                return (true, "已开启", desc);
            }
            catch { return (true, "未检测到", "可能装了别的杀毒软件"); }
        }

        static (bool ok, string text) FirewallStatus()
        {
            var json = Proc.PowerShell("Get-NetFirewallProfile | Select-Object Name,Enabled | ConvertTo-Json -Compress");
            if (string.IsNullOrEmpty(json)) return (true, "未知");
            try
            {
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
                var off = arr.Where(e =>
                {
                    var en = e.GetProperty("Enabled");
                    return en.ValueKind == JsonValueKind.False || (en.ValueKind == JsonValueKind.Number && en.GetInt32() == 0);
                }).Select(e => e.GetProperty("Name").GetString()).ToList();
                return off.Count == 0 ? (true, "已开启") : (false, off.Count == arr.Count ? "已关闭" : "部分关闭");
            }
            catch { return (true, "未知"); }
        }

        // ---------- Homepage ----------
        static Dictionary<string, string> ReadHomepages()
        {
            var d = new Dictionary<string, string>();
            foreach (var p in Paths.ChromiumProfiles())
            {
                var pref = Path.Combine(p, "Preferences");
                if (!File.Exists(pref)) continue;
                try
                {
                    using var fs = new FileStream(pref, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var doc = JsonDocument.Parse(fs);
                    var r = doc.RootElement;
                    string home = r.TryGetProperty("homepage", out var h) ? h.GetString() : "";
                    string startup = "";
                    if (r.TryGetProperty("session", out var s))
                    {
                        if (s.TryGetProperty("restore_on_startup", out var ros)) startup += ros.ToString() + ":";
                        if (s.TryGetProperty("startup_urls", out var su) && su.ValueKind == JsonValueKind.Array)
                            startup += string.Join(",", su.EnumerateArray().Select(x => x.GetString()));
                    }
                    d[p] = home + "|" + startup;
                }
                catch { }
            }
            return d;
        }

        static void SaveHomepageBaseline() => Store.Set("homepageBaseline", JsonSerializer.Serialize(ReadHomepages()));

        static (bool ok, bool changed, string text, string desc) HomepageCheck()
        {
            var now = ReadHomepages();
            if (now.Count == 0) return (true, false, "未检测到", "没找到 Edge 或 Chrome");
            var raw = Store.Get("homepageBaseline");
            if (raw == null) { SaveHomepageBaseline(); return (true, false, "已记录", "以后主页或启动页被改会提醒你"); }
            Dictionary<string, string> b;
            try { b = JsonSerializer.Deserialize<Dictionary<string, string>>(raw); } catch { b = new(); }
            var changed = now.Where(kv => b.TryGetValue(kv.Key, out var old) && old != kv.Value).Select(kv => Paths.BrowserName(kv.Key)).Distinct().ToList();
            if (changed.Count == 0) return (true, false, "未被修改", "Edge、Chrome 的主页和启动页");
            return (false, true, "被修改", string.Join("、", changed) + " 的主页或启动页变了，不是你改的话去浏览器设置里改回来");
        }

        // ---------- hosts ----------
        static string HostsPath => Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

        static (bool ok, string text) HostsCheck()
        {
            try
            {
                var lines = File.ReadAllLines(HostsPath)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith("#"))
                    .Where(l => !(l.Contains("localhost") && (l.StartsWith("127.0.0.1") || l.StartsWith("::1"))))
                    .ToList();
                return lines.Count == 0 ? (true, "正常") : (false, $"有 {lines.Count} 条自定义记录");
            }
            catch { return (true, "无法读取"); }
        }

        // ---------- Startup ----------
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal static List<StartupItem> ReadStartup()
        {
            var list = new List<StartupItem>();
            void FromKey(RegistryKey baseKey, string where)
            {
                try
                {
                    using var k = baseKey.OpenSubKey(RunKey);
                    if (k == null) return;
                    foreach (var n in k.GetValueNames())
                        if (!string.IsNullOrEmpty(n)) list.Add(new StartupItem { Name = n, Command = k.GetValue(n)?.ToString(), Where = where });
                }
                catch { }
            }
            FromKey(Registry.CurrentUser, "HKCU");
            using (var lm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)) FromKey(lm64, "HKLM");
            using (var lm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) FromKey(lm32, "HKLM32");

            foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    foreach (var f in Directory.GetFiles(folder).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                        list.Add(new StartupItem { Name = Path.GetFileNameWithoutExtension(f), Command = f, Where = "Folder:" + folder });
                }
                catch { }
            }
            return list;
        }

        static RegistryKey OpenRun(string where, bool write)
        {
            RegistryKey b = where switch
            {
                "HKCU" => Registry.CurrentUser,
                "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
                "HKLM32" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32),
                _ => null,
            };
            return b?.OpenSubKey(RunKey, write);
        }

        static string DisabledDir { get { var d = Path.Combine(Paths.AppDir, "disabled-startup"); Directory.CreateDirectory(d); return d; } }

        internal static List<StartupItem> LoadDisabled()
        {
            try { return JsonSerializer.Deserialize<List<StartupItem>>(Store.Get("disabledStartup") ?? "[]", JsonOpts) ?? new(); }
            catch { return new(); }
        }

        static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { IncludeFields = true };

        static void SaveDisabled(List<StartupItem> l) => Store.Set("disabledStartup", JsonSerializer.Serialize(l, JsonOpts));

        static void DisableStartup(StartupItem it)
        {
            try
            {
                if (it.Where.StartsWith("Folder:"))
                {
                    var dest = Path.Combine(DisabledDir, Path.GetFileName(it.Command));
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(it.Command, dest);
                }
                else
                {
                    using var k = OpenRun(it.Where, true);
                    k?.DeleteValue(it.Name, false);
                }
                var l = LoadDisabled(); l.RemoveAll(x => x.Id == it.Id); l.Add(it); SaveDisabled(l);
                var b = LoadSet("startupBaseline"); b?.Remove(it.Id); if (b != null) SaveSet("startupBaseline", b);
            }
            catch (Exception ex) { Ui.Info("禁用失败：" + ex.Message); }
        }

        internal static void RestoreStartup(StartupItem it)
        {
            try
            {
                if (it.Where.StartsWith("Folder:"))
                {
                    var src = Path.Combine(DisabledDir, Path.GetFileName(it.Command));
                    if (File.Exists(src)) File.Move(src, it.Command, true);
                }
                else
                {
                    using var k = OpenRun(it.Where, true);
                    k?.SetValue(it.Name, it.Command ?? "");
                }
                var l = LoadDisabled(); l.RemoveAll(x => x.Id == it.Id); SaveDisabled(l);
                var b = LoadSet("startupBaseline") ?? new HashSet<string>(); b.Add(it.Id); SaveSet("startupBaseline", b);
            }
            catch (Exception ex) { Ui.Info("恢复失败：" + ex.Message); }
        }

        static HashSet<string> LoadSet(string key)
        {
            var raw = Store.Get(key);
            if (raw == null) return null;
            try { return new HashSet<string>(JsonSerializer.Deserialize<List<string>>(raw)); } catch { return null; }
        }

        static void SaveSet(string key, IEnumerable<string> ids) => Store.Set(key, JsonSerializer.Serialize(ids.Distinct().ToList()));
    }
}
