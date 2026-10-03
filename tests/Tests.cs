// SerialScope automated tests. Run with tests\run-tests.bat (also run by GitHub Actions on every push).
// No test framework needed: each test is a method that throws if something is wrong.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SerialScope.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class TestAttribute : Attribute { }

    internal static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(what + ": expected [" + expected + "] but got [" + actual + "]");
        }
    }

    internal static class Runner
    {
        [STAThread]
        private static int Main()
        {
            int passed = 0, failed = 0;
            foreach (Type type in typeof(Runner).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.GetCustomAttributes(typeof(TestAttribute), false).Length == 0) continue;
                    string name = type.Name + "." + method.Name;
                    try
                    {
                        method.Invoke(null, null);
                        passed++;
                        Console.WriteLine("  PASS  " + name);
                    }
                    catch (TargetInvocationException ex)
                    {
                        failed++;
                        Console.WriteLine("  FAIL  " + name + "\n        " + ex.InnerException.Message);
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "All " + passed + " tests passed." : failed + " of " + (passed + failed) + " tests FAILED.");
            return failed == 0 ? 0 : 1;
        }
    }

    internal static class HighlightTests
    {
        private static void Kind(string line, LineKind expected)
        {
            Assert.Equal(expected, LogHighlighter.Classify(line), "Classify(\"" + line + "\")");
        }

        [Test] public static void EspIdfLevels()
        {
            Kind("E (4700) audio: I2S write failed", LineKind.Error);
            Kind("W (3300) wifi: Signal weak", LineKind.Warning);
            Kind("I (1200) app: Heartbeat 0", LineKind.Normal);
            Kind("D (1900) sensor: Raw reading 512", LineKind.Debug);
            Kind("V (2000) sensor: verbose", LineKind.Debug);
        }

        [Test] public static void ArduinoEsp32Levels()
        {
            Kind("[  6100][E][storage.cpp:17] mount(): Card not found", LineKind.Error);
            Kind("[  5400][W][sensor.cpp:88] read(): timeout", LineKind.Warning);
            Kind("[  4000][I][main.cpp:42] loop(): info", LineKind.Normal);
            Kind("[  4000][D][main.cpp:42] loop(): debug", LineKind.Debug);
        }

        [Test] public static void Keywords()
        {
            Kind("Guru Meditation Error: Core 1 panic'ed", LineKind.Error);
            Kind("Backtrace: 0x400d1234:0x3ffb1f20", LineKind.Error);
            Kind("*wm:AutoConnect: FAILED for 91 ms", LineKind.Warning);
            Kind("Connection timed out", LineKind.Warning);
            Kind("Temperature 24 C", LineKind.Normal);
            Kind("", LineKind.Normal);
        }

        [Test] public static void NoFalsePositivesInsideWords()
        {
            Kind("terrorist movie review", LineKind.Normal);   // "error" only as a whole word
            Kind("Software update available", LineKind.Normal);
        }
    }

    internal static class PlotParsingTests
    {
        private static bool Parse(string line, out List<string> names, out List<double> values)
        {
            names = new List<string>();
            values = new List<double>();
            return new PlotView().AddLine(line, names, values);
        }

        [Test] public static void SingleValue()
        {
            List<string> n; List<double> v;
            Assert.True(Parse("23.5", out n, out v), "23.5 should plot");
            Assert.Equal(1, v.Count, "value count");
            Assert.Equal(23.5, v[0], "value");
            Assert.Equal("Value 1", n[0], "default name");
        }

        [Test] public static void SeparatorsAndExponents()
        {
            List<string> n; List<double> v;
            Assert.True(Parse("1 2,3\t-1.5e3", out n, out v), "mixed separators");
            Assert.Equal(4, v.Count, "value count");
            Assert.Equal(-1500.0, v[3], "exponent value");
        }

        [Test] public static void NamedValues()
        {
            List<string> n; List<double> v;
            Assert.True(Parse("temp: 21.5, hum=40", out n, out v), "named values");
            Assert.Equal("temp", n[0], "first name");
            Assert.Equal("hum", n[1], "second name");
            Assert.Equal(40.0, v[1], "second value");
        }

        [Test] public static void TextIsRejected()
        {
            List<string> n; List<double> v;
            Assert.True(!Parse("*wm:StartAP with SSID: ESP32", out n, out v), "log text must not plot");
            Assert.True(!Parse("", out n, out v), "empty line must not plot");
            Assert.True(!Parse("status: ok", out n, out v), "named non-number must not plot");
            Assert.True(!Parse("NaN", out n, out v), "NaN must not plot");
        }
    }

    internal static class UpdateTests
    {
        [Test] public static void VersionComparison()
        {
            Assert.True(UpdateChecker.IsNewer("1.1.0", "1.0.0"), "1.1.0 > 1.0.0");
            Assert.True(UpdateChecker.IsNewer("v1.10.0", "1.9.2"), "1.10.0 > 1.9.2 (numeric, not text)");
            Assert.True(UpdateChecker.IsNewer("2", "1.9"), "2 > 1.9");
            Assert.True(UpdateChecker.IsNewer("1.2.0-beta", "1.1.0"), "suffix ignored");
            Assert.True(!UpdateChecker.IsNewer("1.0.0", "1.0.0"), "same version");
            Assert.True(!UpdateChecker.IsNewer("1.0.0", "1.1.0"), "older version");
            Assert.True(!UpdateChecker.IsNewer("garbage", "1.0.0"), "invalid version");
        }

        [Test] public static void ParsesGitHubResponse()
        {
            var r = UpdateChecker.Parse("{\"html_url\":\"https://github.com/Tjs4002/SerialScope/releases/tag/v1.3.0\",\"tag_name\":\"v1.3.0\"}");
            Assert.True(r.Success, "parse succeeded");
            Assert.Equal("1.3.0", r.LatestVersion, "version");
            Assert.True(r.ReleaseUrl.EndsWith("/v1.3.0"), "release url");
            Assert.True(!UpdateChecker.Parse("{\"message\":\"Not Found\"}").Success, "error response");
        }
    }

    internal static class RecorderTests
    {
        [Test] public static void CsvColumnsAndGaps()
        {
            var plot = new PlotView();
            var rec = new Recorder();
            var names = new List<string>();
            var values = new List<double>();
            rec.Start();
            foreach (string line in new[] { "temp:21.5,hum:40", "not a number", "temp:22,hum:41,pressure:1013.2", "3.5" })
                if (plot.AddLine(line, names, values)) rec.Add(names, values);
            rec.Stop();
            Assert.Equal(3, rec.RowCount, "rows (text line skipped)");

            string file = Path.GetTempFileName();
            try
            {
                rec.Save(file);
                string[] lines = File.ReadAllLines(file);
                Assert.Equal("time,elapsed_s,temp,hum,pressure,Value 1", lines[0], "header");
                Assert.True(lines[1].EndsWith(",21.5,40,,"), "first row has gaps for later columns: " + lines[1]);
                Assert.True(lines[3].EndsWith(",,,,3.5"), "last row only has Value 1: " + lines[3]);
            }
            finally { File.Delete(file); }
        }
    }

    internal static class CommandLineTests
    {
        [Test] public static void FullCommand()
        {
            var c = CommandLine.Parse(new[] { "--port", "com3", "--baud", "115200", "--connect", "--plot", "--theme", "light", "--log" });
            Assert.Equal(0, c.Errors.Count, "errors");
            Assert.Equal("COM3", c.Port, "port upper-cased");
            Assert.Equal(115200, c.Baud, "baud");
            Assert.True(c.Connect && c.Plot && c.Log, "flags");
            Assert.Equal("light", c.Theme, "theme");
        }

        [Test] public static void AlternativeSpellings()
        {
            var c = CommandLine.Parse(new[] { "--port=COM7", "/baud", "9600", "-hex", "COM9" });
            Assert.Equal(0, c.Errors.Count, "errors");
            Assert.Equal("COM9", c.Port, "bare port name wins as the last one given");
            Assert.Equal(9600, c.Baud, "baud with slash prefix");
            Assert.True(c.Hex, "hex with single dash");
        }

        [Test] public static void Mistakes()
        {
            Assert.Equal(1, CommandLine.Parse(new[] { "--baud", "fast" }).Errors.Count, "bad baud");
            Assert.Equal(1, CommandLine.Parse(new[] { "--port" }).Errors.Count, "missing port");
            Assert.True(CommandLine.Parse(new[] { "--colour", "red" }).Errors.Count >= 1, "unknown option reported");
            Assert.True(CommandLine.Parse(new[] { "--help" }).ShowHelp, "help");
            Assert.Equal(0, CommandLine.Parse(new string[0]).Errors.Count, "no arguments is fine");
        }
    }

    internal static class CrashReportTests
    {
        [Test] public static void ReportAndIssueLink()
        {
            Exception ex;
            try { throw new InvalidOperationException("Test failure & <details>"); }
            catch (Exception e) { ex = e; }
            string report = CrashHandler.BuildReport(ex);
            Assert.True(report.Contains("SerialScope " + AppInfo.Version), "version in report");
            Assert.True(report.Contains("InvalidOperationException"), "exception type in report");

            string url = CrashHandler.IssueUrl(ex, report);
            Assert.True(url.StartsWith(AppInfo.RepoUrl + "/issues/new?template=bug_report.yml"), "uses the bug report form");
            Assert.True(url.Contains("&version=" + Uri.EscapeDataString(AppInfo.Version)), "version field filled");
            Assert.True(url.Contains("%26%20%3Cdetails%3E"), "special characters are escaped");
            Assert.True(url.Length < 8000, "link short enough for browsers: " + url.Length);
        }
    }

    internal static class SettingsTests
    {
        [Test] public static void RoundTripAndMerge()
        {
            string file = Path.Combine(Path.GetTempPath(), "serialscope-settings-test.ini");
            File.Delete(file);
            try
            {
                var a = Settings.Load(file);
                var b = Settings.Load(file);
                a.Set("baud", 115200);
                a.Set("theme", "light");
                a.Save();
                b.Set("hex", true);       // a second window changes something else
                b.Save();

                var c = Settings.Load(file);
                Assert.Equal(115200, c.GetInt("baud", 0), "baud kept");
                Assert.Equal("light", c.Get("theme", ""), "theme kept after the other window saved");
                Assert.True(c.GetBool("hex", false), "hex saved by second window");
                Assert.Equal("fallback", c.Get("missing", "fallback"), "missing key");
            }
            finally { File.Delete(file); }
        }
    }
}
