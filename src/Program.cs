using System;
using System.IO;
using System.Windows;
using PowerShot.Core;
using PowerShot.Models;

namespace PowerShot
{
    // ============================================================
    // Program — Entry Point (called from PowerShell)
    // ============================================================
    public static class Program
    {
        private static SessionState _session = new SessionState();

        public static void Run(string scriptPath, string saveDir)
        {
            NativeMethods.SetProcessDPIAware();

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("  >_ PowerShot v2.0");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("  クリップボードの監視を開始しました。");
            Console.WriteLine("  スクリーンショットをコピーすると自動でUIが表示されます。");
            Console.WriteLine("  終了するにはこのウィンドウを閉じてください。");
            Console.WriteLine("-----------------------------------------");

            // Ensure save directory exists
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
                Console.WriteLine("  Screenshots フォルダを作成しました。");
            }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var watcher = new ClipboardWatcher(scriptPath, saveDir, _session);
            watcher.Start();

            // Handle console close
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
    }
}
