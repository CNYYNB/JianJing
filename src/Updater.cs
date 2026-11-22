using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace XinCleaner
{
    // Update source: https://jianjing.example.com/xinclean/version.json + latest.exe
    static class Updater
    {
        const string Base = "https://jianjing.example.com/xinclean/";
        public record Release(int Code, string Name, string Notes, long Size, string Sha256);

        static HttpClient Http() => new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        public static async Task<Release> Check()
        {
            using var h = Http();
            h.Timeout = TimeSpan.FromSeconds(15);
            var s = await h.GetStringAsync(Base + "version.json?t=" + DateTime.UtcNow.Ticks);
            using var d = JsonDocument.Parse(s);
            var r = d.RootElement;
            return new Release(r.GetProperty("versionCode").GetInt32(), r.GetProperty("versionName").GetString(),
                r.TryGetProperty("notes", out var n) ? n.GetString() : "", r.GetProperty("size").GetInt64(), r.GetProperty("sha256").GetString());
        }

        public static void Detached(string psScript)
        {
            var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + enc)
            { UseShellExecute = false, CreateNoWindow = true });
        }

        static string Q(string s) => "'" + s.Replace("'", "''") + "'";

        // downloads, verifies, swaps the exe after this process exits, restarts. progress 0..1
        public static async Task Install(Release rel, IProgress<double> progress)
        {
            var part = Path.Combine(Paths.AppDir, "update.exe.part");
            var ready = Path.Combine(Paths.AppDir, "update.exe");
            using (var h = Http())
            using (var resp = await h.GetAsync(Base + "latest.exe?t=" + DateTime.UtcNow.Ticks, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? rel.Size;
                using var src = await resp.Content.ReadAsStreamAsync();
                using var dst = File.Create(part);
                var buf = new byte[1 << 16]; long got = 0; int n;
                while ((n = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n)); got += n;
                    progress?.Report(total > 0 ? (double)got / total : 0);
                }
            }
            string sha;
            using (var f = File.OpenRead(part)) sha = Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
            if (sha != rel.Sha256.ToLowerInvariant()) { File.Delete(part); throw new Exception("下载的文件校验不对，请稍后再试"); }
            File.Move(part, ready, true);

            var exe = Installer.InstalledExe;
            var script = $"Wait-Process -Id {Environment.ProcessId} -Timeout 60 -EA SilentlyContinue;Start-Sleep -Milliseconds 500;" +
                         $"for($i=0;$i -lt 30;$i++){{try{{Move-Item -LiteralPath {Q(ready)} -Destination {Q(exe)} -Force -EA Stop;break}}catch{{Start-Sleep 1}}}};" +
                         $"Start-Process -FilePath {Q(exe)} -WorkingDirectory {Q(Paths.AppDir)} -ArgumentList '--updated'";
            Detached(script);
            Store.Log($"updating {AppInfo.Name} -> {rel.Name}");
            Application.Current.Shutdown();
        }

        public static string Notes(Release r) => string.IsNullOrWhiteSpace(r.Notes) ? "" : "\n\n" + r.Notes;

        // quiet check a few seconds after launch
        public static async void BackgroundCheck()
        {
            try
            {
                await Task.Delay(3000);
                var r = await Check();
                if (r.Code <= AppInfo.Code || Store.Get("skip-version") == r.Code.ToString()) return;
                var ans = MessageBox.Show($"有新版本 {r.Name}，现在更新吗？{Notes(r)}", "简境", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ans != MessageBoxResult.Yes) { Store.Set("skip-version", r.Code.ToString()); return; }
                await Install(r, null);
            }
            catch (Exception e) { Store.Log("update check: " + e.Message); }
        }
    }
}
