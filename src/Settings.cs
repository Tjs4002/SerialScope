using System;
using System.Collections.Generic;
using System.IO;

namespace SerialScope
{
    // Simple key=value settings stored in %APPDATA%\SerialScope\settings.ini
    internal sealed class Settings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.Name, "settings.ini");

        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) s.values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable settings: start with defaults
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var lines = new List<string>();
                foreach (var pair in values) lines.Add(pair.Key + "=" + pair.Value);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception)
            {
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

        public void Set(string key, string value) { values[key] = value ?? ""; }
        public void Set(string key, int value) { values[key] = value.ToString(); }
        public void Set(string key, bool value) { values[key] = value ? "1" : "0"; }
    }
}
