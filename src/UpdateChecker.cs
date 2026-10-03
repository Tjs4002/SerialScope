using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SerialScope
{
    // Asks GitHub for the latest release, off the UI thread
    internal static class UpdateChecker
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/Tjs4002/SerialScope/releases/latest";

        public sealed class Result
        {
            public bool Success;
            public string LatestVersion;   // e.g. "1.2.0"
            public string ReleaseUrl;
            public string Error;
        }

        public static void CheckAsync(Control owner, Action<Result> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Result result = Fetch();
                try { owner.BeginInvoke(done, result); } catch (Exception) { }   // window may have closed meanwhile
            });
        }

        private static Result Fetch()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
                request.UserAgent = AppInfo.Name + "/" + AppInfo.Version;
                request.Accept = "application/vnd.github+json";
                request.Timeout = 10000;
                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();
                return Parse(json);
            }
            catch (Exception ex)
            {
                return new Result { Success = false, Error = ex.Message };
            }
        }

        public static Result Parse(string json)
        {
            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
            Match url = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
            if (!tag.Success) return new Result { Success = false, Error = "Unexpected response from GitHub" };
            return new Result
            {
                Success = true,
                LatestVersion = tag.Groups[1].Value,
                ReleaseUrl = url.Success ? url.Groups[1].Value : AppInfo.RepoUrl + "/releases/latest"
            };
        }

        // True if "latest" is a higher version than "current" (e.g. 1.10.0 > 1.9.2)
        public static bool IsNewer(string latest, string current)
        {
            Version a, b;
            if (!Version.TryParse(Normalize(latest), out a) || !Version.TryParse(Normalize(current), out b)) return false;
            return a > b;
        }

        private static string Normalize(string v)
        {
            if (v == null) return "";
            v = v.Trim().TrimStart('v', 'V');
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);   // ignore "-beta" style suffixes
            return v.Split('.').Length == 1 ? v + ".0" : v;
        }
    }
}
