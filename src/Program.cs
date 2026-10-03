using System;
using System.Windows.Forms;

namespace SerialScope
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CrashHandler.Install();

            CommandLine options = CommandLine.Parse(args);
            if (options.ShowHelp || options.Errors.Count > 0)
            {
                string text = options.Errors.Count > 0
                    ? string.Join("\n", options.Errors.ToArray()) + "\n\n" + CommandLine.Help
                    : CommandLine.Help;
                MessageBox.Show(text, AppInfo.Name, MessageBoxButtons.OK,
                    options.Errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                if (options.ShowHelp) return;
            }

            Application.Run(new MainForm(options));
        }
    }
}
