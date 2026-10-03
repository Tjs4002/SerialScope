using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SerialScope
{
    // Replaces Windows' generic crash dialog with a friendly one that can open a pre-filled GitHub issue
    internal static class CrashHandler
    {
        private static int showing;

        public static void Install()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Report(e.Exception, true); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Report(e.ExceptionObject as Exception, false);
            };
        }

        public static void Report(Exception ex, bool canContinue)
        {
            if (Interlocked.Exchange(ref showing, 1) == 1) return;   // one dialog at a time
            string report = BuildReport(ex);
            string file = SaveReport(report);
            bool keepRunning = false;
            try
            {
                using (var form = new CrashForm(ex, report, file, canContinue))
                {
                    DialogResult result = form.ShowDialog();
                    keepRunning = canContinue && (result == DialogResult.Retry || result == DialogResult.Cancel);
                }
            }
            catch (Exception screenError)
            {
                // The crash screen itself failed: fall back to a plain message box
                Debug.WriteLine("Crash screen failed: " + screenError);
                try
                {
                    MessageBox.Show(AppInfo.Name + " ran into a problem.\n\n" + (ex == null ? "Unknown error" : ex.Message) +
                        (file != null ? "\n\nDetails were saved to " + file : ""), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception) { }
                keepRunning = canContinue;
            }
            finally
            {
                Interlocked.Exchange(ref showing, 0);
            }
            if (!keepRunning) Environment.Exit(1);
        }

        public static string BuildReport(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SerialScope " + AppInfo.Version);
            sb.AppendLine(WindowsVersion() + (Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)"));
            sb.AppendLine(".NET " + Environment.Version);
            sb.AppendLine("Time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine(ex == null ? "Unknown error" : ex.ToString());
            return sb.ToString();
        }

        // e.g. "Windows 10 Pro 22H2, build 19045" (Environment.OSVersion can report an older version for compatibility)
        private static string WindowsVersion()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    string product = key.GetValue("ProductName") as string ?? "Windows";
                    string build = key.GetValue("CurrentBuildNumber") as string ?? "";
                    string display = key.GetValue("DisplayVersion") as string ?? key.GetValue("ReleaseId") as string ?? "";
                    int buildNumber;
                    // Windows 11 still says "Windows 10" in ProductName; build 22000+ is Windows 11
                    if (int.TryParse(build, out buildNumber) && buildNumber >= 22000) product = product.Replace("Windows 10", "Windows 11");
                    return product + (display.Length > 0 ? " " + display : "") + (build.Length > 0 ? ", build " + build : "");
                }
            }
            catch (Exception)
            {
                return "Windows " + Environment.OSVersion.Version;
            }
        }

        private static string SaveReport(string report)
        {
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "SerialScope-crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(file, report);
                return file;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // New-issue link that fills in the bug report form (title, version and details)
        public static string IssueUrl(Exception ex, string report)
        {
            string title = "Crash: " + (ex == null ? "unknown error" : ex.GetType().Name + ": " + ex.Message);
            if (title.Length > 120) title = title.Substring(0, 117) + "...";
            string details = report.Length > 5000 ? report.Substring(0, 5000) + "\n..." : report;
            return AppInfo.RepoUrl + "/issues/new?template=bug_report.yml&labels=bug" +
                   "&title=" + Uri.EscapeDataString(title) +
                   "&version=" + Uri.EscapeDataString(AppInfo.Version) +
                   "&what-happened=" + Uri.EscapeDataString("SerialScope crashed with the error below.") +
                   "&steps=" + Uri.EscapeDataString("1. (What were you doing when it happened?)") +
                   "&log=" + Uri.EscapeDataString("```\n" + details + "\n```");
        }
    }

    internal sealed class CrashForm : Form
    {
        public CrashForm(Exception ex, string report, string savedFile, bool canContinue)
        {
            Theme t = Theme.Dark;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = AppInfo.Name + " ran into a problem";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 420);
            BackColor = t.Window;
            ForeColor = t.Text;
            TopMost = true;

            var heading = new Label
            {
                Text = "Sorry, SerialScope ran into a problem",
                Font = new Font("Segoe UI Semibold", 13F),
                AutoSize = true,
                Location = new Point(22, 18)
            };
            var info = new Label
            {
                Text = "Reporting it helps get it fixed. \"Report on GitHub\" opens a pre-filled bug report in your browser; " +
                       "nothing is sent unless you submit it there." +
                       (savedFile != null ? "\nA copy was saved in your Temp folder as " + Path.GetFileName(savedFile) + "." : ""),
                Location = new Point(24, 52),
                Size = new Size(512, 52),
                ForeColor = t.Muted
            };

            var frame = new BorderPanel { Location = new Point(24, 110), Size = new Size(512, 240), Padding = new Padding(8, 6, 2, 2) };
            var details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                Text = report.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                BackColor = t.Surface,
                ForeColor = t.Text
            };
            frame.Controls.Add(details);
            frame.ApplyTheme(t);

            var reportButton = new FlatButton("Report on GitHub", ButtonKind.Success) { MinimumSize = new Size(140, 30), Location = new Point(24, 368) };
            var copyButton = new FlatButton("Copy details", ButtonKind.Normal) { MinimumSize = new Size(110, 30), Location = new Point(172, 368) };
            var closeButton = new FlatButton(canContinue ? "Continue" : "Close SerialScope", ButtonKind.Normal) { MinimumSize = new Size(130, 30) };
            closeButton.Location = new Point(536 - 130, 368);
            foreach (var b in new[] { reportButton, copyButton, closeButton }) b.ApplyTheme(t);

            reportButton.Click += delegate
            {
                try { Process.Start(CrashHandler.IssueUrl(ex, report)); } catch (Exception) { }
            };
            copyButton.Click += delegate
            {
                try { Clipboard.SetText(report); copyButton.Text = "Copied"; } catch (Exception) { }
            };
            closeButton.DialogResult = canContinue ? DialogResult.Retry : DialogResult.Abort;
            AcceptButton = closeButton;
            CancelButton = closeButton;

            Controls.AddRange(new Control[] { heading, info, frame, reportButton, copyButton, closeButton });
            Shown += delegate
            {
                details.Select(0, 0);   // don't start with all the text selected
                closeButton.Focus();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, true);
        }
    }
}
