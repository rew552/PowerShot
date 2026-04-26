using System;
using System.IO;
using System.Threading;
using System.Windows;

using PowerShot.Models;

namespace PowerShot.App
{
    public static class Program
    {
        private const string MutexName = "PowerShot_SingleInstance_v3";
        private static SessionState _session = new SessionState();

        public static void Run(string scriptPath)
        {
            bool createdNew;
            var mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "PowerShot はすでに起動しています。\nタスクバーまたはシステムトレイを確認してください。",
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Information);
                mutex.Dispose();
                return;
            }

            try
            {
                RunInternal(scriptPath);
            }
            finally
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }

        private static void RunInternal(string scriptPath)
        {
            ConfigureDpiAwareness();

            string settingsPath = Path.Combine(scriptPath, "settings.json");
            var settings = SettingsManager.Load(settingsPath);

            string projectRoot = Path.GetDirectoryName(scriptPath);
            string saveDir = Path.GetFullPath(Path.Combine(projectRoot, settings.SaveFolder));

            PrintWelcomeMessage();

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
                Console.WriteLine("  Screenshots directory created.");
            }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var watcher = new ClipboardWatcher(scriptPath, settings, _session);
            watcher.Start();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                watcher.Dispose();
                Environment.Exit(0);
            };

            app.Run();

            watcher.Dispose();
            Console.WriteLine("\nPowerShot terminated.");
        }

        private static void ConfigureDpiAwareness()
        {
            try
            {
                // Per-Monitor V2: Windows 10 1703+
                if (!PowerShot.Utils.NativeMethods.SetProcessDpiAwarenessContext(
                        PowerShot.Utils.NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    PowerShot.Utils.NativeMethods.SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                PowerShot.Utils.NativeMethods.SetProcessDPIAware();
            }
        }

        private static void PrintWelcomeMessage()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("           ____                          _____ __          __ ");
            Console.WriteLine("          / __ \\____ _      _____  _____/ ___// /_  ____  / /_");
            Console.WriteLine("         / /_/ / __ \\ | /| / / _ \\/ ___/\\__ \\/ __ \\/ __ \\/ __/");
            Console.WriteLine("        / ____/ /_/ / |/ |/ /  __/ /   ___/ / / / / /_/ / /_  ");
            Console.WriteLine("       /_/    \\____/|__/|__/\\___/_/   /____/_/ /_/\\____/\\__/  ");
            Console.WriteLine();
            Console.ResetColor();

            Console.WriteLine("  Version: 3.1");
            Console.WriteLine("  ---------------------------------------------------------------------");
            Console.WriteLine("  Clipboard monitoring started.");
            Console.WriteLine("  Capturing a screenshot will launch the UI for preview/save/edit.");
            Console.WriteLine("  Close this window to exit.");
            Console.WriteLine("");
            Console.WriteLine("  [Tips] Shortcuts:");
            Console.WriteLine("   - [Win] + [Shift] + [S]   : Capture region");
            Console.WriteLine("   - [Alt] + [PrintScreen]   : Capture active window");
            Console.WriteLine("   - [Shift] + [PrintScreen] : Capture active monitor");
            Console.WriteLine("   - [PrintScreen]           : Capture entire screen");
            Console.WriteLine("  ---------------------------------------------------------------------");
            Console.WriteLine();
        }
    }
}

