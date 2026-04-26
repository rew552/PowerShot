using System;
using System.IO;
using System.Threading;
using System.Windows;

using PowerShot.Models;
using PowerShot.Utils;

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
                Console.WriteLine("  Screenshots フォルダを作成しました。");
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
            Console.WriteLine("\nPowerShotを終了しました。");
        }

        private static void ConfigureDpiAwareness()
        {
            // Per-Monitor V2: Windows 10 1703+ で WPF がモニターごとの DPI に自動追従する。
            // 旧 Windows では EntryPointNotFoundException が出るので System DPI Aware にフォールバック。
            try
            {
                if (!NativeMethods.SetProcessDpiAwarenessContext(
                        NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    NativeMethods.SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                NativeMethods.SetProcessDPIAware();
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

            Console.WriteLine("  Version: 3.2");
            Console.WriteLine("  ---------------------------------------------------------------------");
            Console.WriteLine("  クリップボードの監視を開始しました。");
            Console.WriteLine("  スクリーンショットを取得するとUIが起動し、プレビューおよび保存・編集が可能です。");
            Console.WriteLine("  終了するにはこのウィンドウを閉じてください。");
            Console.WriteLine("");
            Console.WriteLine("  [Tips] スクリーンショット:");
            Console.WriteLine("   ・[Win] + [Shift] + [S]   : 範囲を指定してキャプチャ");
            Console.WriteLine("   ・[Alt] + [PrintScreen]   : アクティブウィンドウをキャプチャ");
            Console.WriteLine("   ・[Shift] + [PrintScreen] : アクティブモニターをキャプチャ（独自実装）");
            Console.WriteLine("   ・[PrintScreen]           : 画面全体をキャプチャ");
            Console.WriteLine("     (※PrintScreenで範囲指定になる場合がある。その場合はCtrl + PrintScreen)");
            Console.WriteLine("  ---------------------------------------------------------------------");
            Console.WriteLine();
        }
    }
}


