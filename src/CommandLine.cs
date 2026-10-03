using System;
using System.Collections.Generic;

namespace SerialScope
{
    // Options for starting SerialScope from a shortcut, script or IDE, e.g.
    //   SerialScope.exe --port COM3 --baud 115200 --connect --plot
    internal sealed class CommandLine
    {
        public string Port;
        public int Baud;            // 0 = not given
        public bool Connect;
        public bool Plot;
        public bool Text;
        public bool Hex;
        public bool Log;
        public string Theme;        // "dark" or "light"
        public bool ShowHelp;
        public readonly List<string> Errors = new List<string>();

        public const string Help =
            "SerialScope " + AppInfo.Version + "\n\n" +
            "Usage: SerialScope.exe [options]\n\n" +
            "  --port COM3        Select a port\n" +
            "  --baud 115200      Select a baud rate\n" +
            "  --connect          Connect straight away\n" +
            "  --plot             Open in plotter view\n" +
            "  --text             Open in text view\n" +
            "  --hex              Turn on hex view\n" +
            "  --log              Save a session log\n" +
            "  --theme dark|light Choose the theme\n" +
            "  --help             Show this help\n\n" +
            "Example: SerialScope.exe --port COM3 --baud 115200 --connect --plot";

        public static CommandLine Parse(string[] args)
        {
            var c = new CommandLine();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].Trim();
                string value = null;

                // Accept "--port=COM3" as well as "--port COM3", and "/port" or "-port"
                int eq = a.IndexOf('=');
                if (eq > 0) { value = a.Substring(eq + 1); a = a.Substring(0, eq); }
                string name = a.TrimStart('-', '/').ToLowerInvariant();

                switch (name)
                {
                    case "port":
                    case "p":
                        value = value ?? Next(args, ref i);
                        if (string.IsNullOrEmpty(value)) c.Errors.Add("--port needs a port name, e.g. --port COM3");
                        else c.Port = value.ToUpperInvariant();
                        break;
                    case "baud":
                    case "b":
                        value = value ?? Next(args, ref i);
                        int baud;
                        if (int.TryParse(value, out baud) && baud > 0) c.Baud = baud;
                        else c.Errors.Add("--baud needs a number, e.g. --baud 115200");
                        break;
                    case "connect":
                    case "c":
                        c.Connect = true; break;
                    case "plot":
                    case "plotter":
                        c.Plot = true; break;
                    case "text":
                        c.Text = true; break;
                    case "hex":
                        c.Hex = true; break;
                    case "log":
                        c.Log = true; break;
                    case "theme":
                        value = (value ?? Next(args, ref i) ?? "").ToLowerInvariant();
                        if (value == "dark" || value == "light") c.Theme = value;
                        else c.Errors.Add("--theme must be dark or light");
                        break;
                    case "help":
                    case "h":
                    case "?":
                        c.ShowHelp = true; break;
                    default:
                        // A bare "COM3" is taken as the port
                        if (a.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("-")) c.Port = a.ToUpperInvariant();
                        else c.Errors.Add("Unknown option: " + args[i]);
                        break;
                }
            }
            return c;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) return args[++i];
            return null;
        }
    }
}
