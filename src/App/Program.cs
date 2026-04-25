using System;
using System.IO;
using System.Windows;

namespace PowerShot
{
    public static class Program
    {
        private static SessionState _session = new SessionState();

        public static void Run(string scriptPath)
        {
            NativeMethods.SetProcessDPIAware();

            string settingsPath = Path.Combine(scriptPath, "settings.json");
            var settings = SettingsManager.Load(settingsPath);
            
            // Use the parent of scriptPath (project root) as the base for the SaveFolder
            string projectRoot = Path.GetDirectoryName(scriptPath);
            string saveDir = Path.GetFullPath(Path.Combine(projectRoot, settings.SaveFolder));

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("           ____                          _____ __          __ ");
            Console.WriteLine("          / __ \\____ _      _____  _____/ ___// /_  ____  / /_");
            Console.WriteLine("         / /_/ / __ \\ | /| / / _ \\/ ___/\\__ \\/ __ \\/ __ \\/ __/");
            Console.WriteLine("        / ____/ /_/ / |/ |/ /  __/ /   ___/ / / / / /_/ / /_  ");
            Console.WriteLine("       /_/    \\____/|__/|__/\\___/_/   /____/_/ /_/\\____/\\__/  ");
            Console.WriteLine();
            Console.ResetColor();

            Console.WriteLine("  Version: 3.0");
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

            // Ensure save directory exists
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
                Console.WriteLine("  Screenshots フォルダを作成しました。");
            }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var watcher = new ClipboardWatcher(scriptPath, settings, _session);
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

