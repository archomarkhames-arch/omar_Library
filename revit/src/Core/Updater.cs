using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace OmarKhamis.Revit.Core
{
    /// <summary>
    /// Online updates from the same GitHub repo as the SketchUp library (folder revit/update).
    /// A new version is unpacked next to the old one and the .addin file is pointed at it,
    /// so nothing locked by Revit is ever overwritten; Revit uses it from the next start.
    /// </summary>
    public static class Updater
    {
        public const string Base = "https://raw.githubusercontent.com/archomarkhames-arch/omar_library/main/revit/update/";
        public static string Pending;          // message waiting to be shown on the UI thread
        public static string LatestSeen;

        public static string Current { get { return typeof(Updater).Assembly.GetName().Version.ToString(3); } }

        public static string DataDir
        {
            get
            {
                var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OmarKhamisRevit");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static int Cmp(string a, string b)
        {
            var x = (a ?? "0").Split('.'); var y = (b ?? "0").Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = i < x.Length ? ParseInt(x[i]) : 0, q = i < y.Length ? ParseInt(y[i]) : 0;
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        static int ParseInt(string s) { int v; return int.TryParse(Regex.Replace(s ?? "", "[^0-9]", ""), out v) ? v : 0; }

        static WebClient Client()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            var w = new WebClient { Encoding = Encoding.UTF8 };
            w.Headers[HttpRequestHeader.UserAgent] = "OmarKhamisRevit/" + Current;
            return w;
        }

        static string Field(string json, string name)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }

        /// <summary>Background check at startup. Never throws, never touches the Revit API.</summary>
        public static void CheckInBackground()
        {
            var t = new Thread(() =>
            {
                try { Thread.Sleep(8000); string msg; Run(out msg); if (msg != null) Pending = msg; }
                catch { }
            }) { IsBackground = true, Name = "OKR-update" };
            t.Start();
        }

        /// <summary>Returns true when a new version was installed (active after restarting Revit).</summary>
        public static bool Run(out string message)
        {
            message = null;
            string json;
            using (var w = Client()) json = w.DownloadString(Base + "manifest.json?t=" + DateTime.UtcNow.Ticks);
            var latest = Field(json, "version");
            LatestSeen = latest;
            if (latest == null || Cmp(latest, Current) <= 0) return false;

            var key = "R" + Compat.Version;
            var block = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\{([^}]*)\\}");
            if (!block.Success) return false;
            var file = Field("{" + block.Groups[1].Value + "}", "file");
            var sha = (Field("{" + block.Groups[1].Value + "}", "sha256") ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(file)) return false;

            var target = Path.Combine(DataDir, "versions", latest, key);
            var dll = Path.Combine(target, "OmarKhamis.Revit.dll");
            if (!File.Exists(dll))
            {
                byte[] zip;
                using (var w = Client()) zip = w.DownloadData(Base + file + "?t=" + DateTime.UtcNow.Ticks);
                string got;
                using (var h = SHA256.Create()) got = BitConverter.ToString(h.ComputeHash(zip)).Replace("-", "").ToLowerInvariant();
                if (sha.Length > 0 && got != sha) throw new Exception("update file damaged (sha256)");
                var tmp = target + ".tmp";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);
                using (var ms = new MemoryStream(zip))
                using (var z = new ZipArchive(ms, ZipArchiveMode.Read))
                    foreach (var e in z.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue;
                        var dest = Path.GetFullPath(Path.Combine(tmp, e.FullName));
                        if (!dest.StartsWith(Path.GetFullPath(tmp), StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        using (var src = e.Open()) using (var o = File.Create(dest)) src.CopyTo(o);
                    }
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(tmp, target);
            }
            if (!PointAddinAt(dll)) return false;
            var notes = Field(json, "notes") ?? "";
            message = "تم تنزيل إصدار جديد من مكتبة م. عمر خميس لريفيت (" + latest + ").\n\nاقفل ريفيت وافتحه تاني عشان يشتغل." + (notes.Length > 0 ? "\n\nالجديد:\n" + notes : "");
            return true;
        }

        /// <summary>Rewrites our .addin manifest(s) for this Revit version to load the given dll next time.</summary>
        static bool PointAddinAt(string dll)
        {
            bool done = false;
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            };
            foreach (var r in roots)
            {
                var f = Path.Combine(r, "Autodesk", "Revit", "Addins", Compat.Version.ToString(), "OmarKhamis.addin");
                if (!File.Exists(f)) continue;
                try
                {
                    var x = File.ReadAllText(f, Encoding.UTF8);
                    var y = Regex.Replace(x, "<Assembly>[^<]*</Assembly>", "<Assembly>" + System.Security.SecurityElement.Escape(dll) + "</Assembly>");
                    if (y != x) { File.WriteAllText(f, y, new UTF8Encoding(true)); }
                    done = true;
                }
                catch { }
            }
            return done;
        }

        /// <summary>Removes old downloaded versions (keeps the running one and the newest).</summary>
        public static void Cleanup()
        {
            try
            {
                var root = Path.Combine(DataDir, "versions");
                if (!Directory.Exists(root)) return;
                var running = Path.GetFullPath(typeof(Updater).Assembly.Location);
                foreach (var d in Directory.GetDirectories(root))
                {
                    var v = Path.GetFileName(d);
                    if (Cmp(v, Current) < 0 && !running.StartsWith(Path.GetFullPath(d), StringComparison.OrdinalIgnoreCase))
                    {
                        try { Directory.Delete(Path.Combine(d, "R" + Compat.Version), true); } catch { }
                        try { if (Directory.GetFileSystemEntries(d).Length == 0) Directory.Delete(d); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
