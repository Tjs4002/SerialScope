using System;
using System.Collections.Generic;
using System.IO;

namespace SerialScope
{
    // Simple key=value settings.
    // Normally stored in %APPDATA%\SerialScope\settings.ini. In portable mode (a SerialScope.ini file
    // next to the exe) they live in that file instead, so the app can run from a USB stick.
    internal sealed class Settings
    {
        public const string PortableFileName = "SerialScope.ini";

        public static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name, "settings.ini");

        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string path;

        private Settings(string path)
        {
            this.path = path;
        }

        public static string AppFolder
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string PortablePath
        {
            get { return Path.Combine(AppFolder, PortableFileName); }
        }

        public static bool PortableMode
        {
            get { return File.Exists(PortablePath); }
        }

        public string FilePath
        {
            get { return path; }
        }

        public static Settings Load()
        {
            return Load(PortableMode ? PortablePath : AppDataPath);
        }

        public static Settings Load(string path)
        {
            var s = new Settings(path);
            ReadInto(path, s.values);
            return s;
        }

        private static void ReadInto(string path, Dictionary<string, string> target)
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) target[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception)
            {
                // Unreadable settings: start with defaults
            }
        }

        // Saves only what this window changed on top of the current file, so two open
        // windows don't wipe out each other's settings
        public void Save()
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReadInto(path, merged);
            foreach (string key in changed) merged[key] = values[key];
            Write(path, merged);
        }

        // Writes every setting to a new file and uses that file from now on (for switching portable mode)
        public void SaveAs(string newPath)
        {
            Write(newPath, values, true);
            path = newPath;
            changed.Clear();
        }

        private static void Write(string file, Dictionary<string, string> data, bool throwOnError = false)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                var lines = new List<string>();
                foreach (var pair in data) lines.Add(pair.Key + "=" + pair.Value);
                lines.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(file, lines.ToArray());
            }
            catch (Exception)
            {
                if (throwOnError) throw;
            }
        }

        public string Get(string key, string fallback)
        {
            string v;
            return values.TryGetValue(key, out v) && v.Length > 0 ? v : fallback;
        }

        public int GetInt(string key, int fallback)
        {
            int v;
            return int.TryParse(Get(key, ""), out v) ? v : fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            string v = Get(key, "");
            return v.Length == 0 ? fallback : v == "1";
        }

        public void Set(string key, string value)
        {
            value = value ?? "";
            string old;
            if (values.TryGetValue(key, out old) && old == value) return;
            values[key] = value;
            changed.Add(key);
        }

        public void Set(string key, int value) { Set(key, value.ToString()); }
        public void Set(string key, bool value) { Set(key, value ? "1" : "0"); }
    }
}
