using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace SerialScope
{
    internal sealed class PortInfo
    {
        public string Name;
        public string Description;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Description) ? Name : Name + "  —  " + Description;
        }

        public static string[] GetNames()
        {
            var names = new List<string>(SerialPort.GetPortNames());
            names.Sort(Compare);
            return names.ToArray();
        }

        public static List<PortInfo> List()
        {
            var descriptions = GetDescriptions();
            var result = new List<PortInfo>();
            foreach (string name in GetNames())
            {
                string desc;
                descriptions.TryGetValue(name, out desc);
                result.Add(new PortInfo { Name = name, Description = desc });
            }
            return result;
        }

        private static int Compare(string a, string b)
        {
            int na, nb;
            if (int.TryParse(a.Replace("COM", ""), out na) && int.TryParse(b.Replace("COM", ""), out nb))
                return na.CompareTo(nb);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // Friendly device names, e.g. "Silicon Labs CP210x USB to UART Bridge (COM3)"
        private static Dictionary<string, string> GetDescriptions()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"] as string;
                        if (name == null) continue;
                        Match m = Regex.Match(name, @"^(.*?)\s*\((COM\d+)\)");
                        if (m.Success) result[m.Groups[2].Value] = m.Groups[1].Value;
                    }
                }
            }
            catch (Exception)
            {
                // WMI unavailable: show bare port names
            }
            return result;
        }
    }
}
