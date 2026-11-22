using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using Gravexel.Core;

namespace Gravexel.Desktop
{
    /// <summary>
    /// Checks the project's GitHub Releases for a newer version and installs it in one click:
    /// downloads the Setup .exe of the latest release, runs it silently and closes the game.
    /// The installer replaces the files and starts the new version. Offline = silently ignored.
    /// Repository: "owner/name" from update.cfg next to the exe, otherwise DefaultRepo.
    /// </summary>
    public sealed class GitHubUpdater : IUpdater
    {
        public const string DefaultRepo = "senesikakashi-design/gravexel";

        readonly string _repo;
        readonly Action _quit;
        volatile UpdateState _state = UpdateState.Idle;
        volatile float _progress;
        UpdateInfo _latest = new UpdateInfo();
        string _error = "";

        public GitHubUpdater(Action quit)
        {
            _quit = quit;
            _repo = DefaultRepo;
            try
            {
                string cfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.cfg");
                if (File.Exists(cfg))
                    foreach (var line in File.ReadAllLines(cfg))
                        if (line.Trim().StartsWith("repo=")) _repo = line.Trim().Substring(5).Trim();
            }
            catch (Exception) { }
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch (Exception) { }
        }

        public UpdateState State { get { return _state; } }
        public UpdateInfo Latest { get { return _latest; } }
        public float Progress { get { return _progress; } }
        public string Error { get { return _error; } }
        public bool CanInstallInApp { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        public void Check()
        {
            if (_state == UpdateState.Checking || _state == UpdateState.Downloading || _state == UpdateState.Installing) return;
            _state = UpdateState.Checking;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + _repo + "/releases/latest");
                    req.UserAgent = "Gravexel/" + GameApp.Version;
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = 10000;
                    string json;
                    using (var resp = req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream())) json = sr.ReadToEnd();
                    var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
                    var info = new UpdateInfo
                    {
                        Version = Str(root, "tag_name"),
                        Notes = Str(root, "body"),
                        PageUrl = Str(root, "html_url")
                    };
                    object assets;
                    if (root.TryGetValue("assets", out assets) && assets is IEnumerable)
                        foreach (var a in (IEnumerable)assets)
                        {
                            var d = a as Dictionary<string, object>;
                            if (d == null) continue;
                            string name = Str(d, "name").ToLowerInvariant();
                            if (name.EndsWith(".exe") && name.Contains("setup")) info.DownloadUrl = Str(d, "browser_download_url");
                        }
                    _latest = info;
                    _state = VersionUtil.IsNewer(info.Version, GameApp.Version) ? UpdateState.Available : UpdateState.UpToDate;
                }
                catch (Exception ex) { _error = Short(ex); _state = UpdateState.Failed; }
            });
        }

        public void Install()
        {
            var info = _latest;
            if (string.IsNullOrEmpty(info.DownloadUrl) || !CanInstallInApp)
            {
                if (!string.IsNullOrEmpty(info.PageUrl)) try { Process.Start(info.PageUrl); } catch (Exception) { }
                return;
            }
            _state = UpdateState.Downloading; _progress = 0;
            string dir = Path.Combine(Path.GetTempPath(), "GravexelUpdate");
            string file = Path.Combine(dir, "Gravexel-Setup-" + info.Version.TrimStart('v') + ".exe");
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            var wc = new WebClient();
            wc.Headers.Add("User-Agent", "Gravexel/" + GameApp.Version);
            wc.DownloadProgressChanged += (s, e) => { if (e.TotalBytesToReceive > 0) _progress = e.BytesReceived / (float)e.TotalBytesToReceive; };
            wc.DownloadFileCompleted += (s, e) =>
            {
                wc.Dispose();
                if (e.Error != null || e.Cancelled) { _error = e.Error != null ? Short(e.Error) : "cancelled"; _state = UpdateState.Failed; return; }
                try
                {
                    _progress = 1; _state = UpdateState.Installing;
                    // /S = silent; the installer closes this game, replaces the files and starts the new version
                    Process.Start(new ProcessStartInfo(file, "/S") { UseShellExecute = true });
                    Thread.Sleep(600);
                    _quit();
                }
                catch (Exception ex) { _error = Short(ex); _state = UpdateState.Failed; }
            };
            try { wc.DownloadFileAsync(new Uri(info.DownloadUrl), file); }
            catch (Exception ex) { _error = Short(ex); _state = UpdateState.Failed; }
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static string Short(Exception ex)
        {
            string m = ex.Message ?? "error";
            return m.Length > 70 ? m.Substring(0, 70) + "..." : m;
        }
    }
}
