using System.IO;

namespace XinCleaner
{
    // Hard rule: never touch system files or anything that keeps Windows / installed programs working.
    static class Safety
    {
        static string N(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\') + "\\"; } catch { return null; }
        }
        static string F(Environment.SpecialFolder f) { var s = Environment.GetFolderPath(f); return string.IsNullOrEmpty(s) ? null : N(s); }

        static readonly string Win = F(Environment.SpecialFolder.Windows);
        static readonly string[] WinAllowed =
        {
            Win + "Temp\\", Win + "SoftwareDistribution\\Download\\",
        };
        static readonly string[] Programs = new[]
        {
            F(Environment.SpecialFolder.ProgramFiles), F(Environment.SpecialFolder.ProgramFilesX86),
            F(Environment.SpecialFolder.CommonApplicationData),
        }.Where(x => x != null).ToArray();
        static readonly string[] ProgramDataAllowed =
        {
            N(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER")),
        };
        // folders that must never be removed themselves
        static readonly string[] Roots = new[]
        {
            F(Environment.SpecialFolder.UserProfile), F(Environment.SpecialFolder.ApplicationData), F(Environment.SpecialFolder.LocalApplicationData),
            F(Environment.SpecialFolder.Desktop), F(Environment.SpecialFolder.MyDocuments), F(Environment.SpecialFolder.MyPictures),
            F(Environment.SpecialFolder.MyMusic), F(Environment.SpecialFolder.MyVideos), F(Environment.SpecialFolder.Programs),
            F(Environment.SpecialFolder.CommonProgramFiles), F(Environment.SpecialFolder.CommonProgramFilesX86),
            N(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")),
            N(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        }.Concat(Programs).Where(x => x != null).ToArray();
        static readonly string[] SysNames = { "\\$Recycle.Bin\\", "\\System Volume Information\\", "\\Recovery\\", "\\Boot\\", "\\EFI\\", "\\WindowsApps\\", "\\$WinREAgent\\", "\\Config.Msi\\" };

        static bool Under(string p, string root) => root != null && p.StartsWith(root, StringComparison.OrdinalIgnoreCase);

        /// Files of Windows itself, drive roots, system-flagged files. Never deleted by anything.
        public static bool IsSystem(string path)
        {
            var p = N(path);
            if (p == null) return true;
            if (p.Length <= 4) return true; // C:\
            if (Whitelist.Contains(p)) return true;
            if (SysNames.Any(s => p.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            if (Under(p, Win) && !WinAllowed.Any(a => Under(p, a))) return true;
            if (Roots.Any(r => string.Equals(p, r, StringComparison.OrdinalIgnoreCase))) return true;
            try
            {
                var a = File.GetAttributes(path);
                if ((a & FileAttributes.System) != 0) return true;
            }
            catch { }
            return false;
        }

        /// Also blocks files inside installed programs (Program Files, ProgramData) — deleting those breaks the program.
        public static bool IsProgram(string path)
        {
            var p = N(path);
            if (p == null) return true;
            if (ProgramDataAllowed.Any(a => Under(p, a))) return false;
            return Programs.Any(r => Under(p, r));
        }

        public static bool CanDeleteFile(string path) => !IsSystem(path) && !IsProgram(path) && !IsProtected(path);

        // keys, certificates, wallets, SSH: never deleted by anything
        static readonly string[] SecretExt = { ".pem", ".key", ".pfx", ".p12", ".ppk", ".kdbx", ".jks", ".keystore", ".gpg", ".asc", ".ovpn", ".cer", ".crt" };
        static readonly string[] SecretNames = { "wallet.dat", "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa", "known_hosts", "authorized_keys" };
        static readonly string[] SecretDirs = { "\\.ssh\\", "\\.gnupg\\", "\\Ethereum\\keystore\\", "\\Bitcoin\\", "\\Electrum\\wallets\\", "\\MetaMask\\", "\\Microsoft\\Crypto\\", "\\Microsoft\\Protect\\", "\\Microsoft\\SystemCertificates\\" };

        public static bool IsProtected(string path)
        {
            var p = N(path);
            if (p == null) return true;
            if (SecretDirs.Any(d => p.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            var name = Path.GetFileName(path.TrimEnd('\\'));
            if (SecretNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) return true;
            if (SecretExt.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase))) return true;
            return Whitelist.Contains(path);
        }
    }
}

namespace XinCleaner
{
    /// Folders/files the user told us never to touch.
    static class Whitelist
    {
        static List<string> _cache;
        public static List<string> Items
        {
            get
            {
                if (_cache != null) return _cache;
                var raw = Store.Get("whitelist") ?? "";
                return _cache = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != "").ToList();
            }
        }
        static void Save() => Store.Set("whitelist", string.Join("\n", _cache));
        public static void Add(string p) { var l = Items; if (!l.Contains(p, StringComparer.OrdinalIgnoreCase)) { l.Add(p); Save(); } }
        public static void Remove(string p) { Items.RemoveAll(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)); Save(); }
        public static bool Contains(string path)
        {
            if (Items.Count == 0 || string.IsNullOrEmpty(path)) return false;
            string full;
            try { full = System.IO.Path.GetFullPath(path).TrimEnd('\\') + "\\"; } catch { return false; }
            return Items.Any(w => full.StartsWith(w.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
        }
    }
}
