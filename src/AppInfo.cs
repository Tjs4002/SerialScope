using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

[assembly: AssemblyTitle("SerialScope")]
[assembly: AssemblyDescription("A clean, lightweight serial monitor and plotter for Windows.")]
[assembly: AssemblyCompany("Tjs4002")]
[assembly: AssemblyProduct("SerialScope")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Tjs4002")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2.0")]
[assembly: ComVisible(false)]

namespace SerialScope
{
    internal static class AppInfo
    {
        public const string Name = "SerialScope";
        public const string Version = "1.2.0";
        public const string Author = "Tjs4002";
        public const string GitHubUser = "Tjs4002";
        public const string ProfileUrl = "https://github.com/Tjs4002";
        public const string RepoUrl = "https://github.com/Tjs4002/SerialScope";
        public const string License = "MIT License";

        // True when running as the Microsoft Store (MSIX) package. The Store updates the app itself,
        // and the install folder is read-only, so update checks and portable mode are left out there.
        public static readonly bool IsStoreApp = DetectPackage();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        private static bool DetectPackage()
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != 15700;   // APPMODEL_ERROR_NO_PACKAGE
            }
            catch (EntryPointNotFoundException)
            {
                return false;   // Windows 7: no packaged apps
            }
        }
    }
}
