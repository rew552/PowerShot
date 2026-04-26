using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        string testDir = "test_data_dir";
        if (!Directory.Exists(testDir))
        {
            Directory.CreateDirectory(testDir);
            for (int i = 0; i < 50000; i++)
            {
                File.Create(Path.Combine(testDir, $"file_{i}.txt")).Dispose();
            }
        }

        var dirInfo = new DirectoryInfo(testDir);

        // Warmup
        var warmup1 = dirInfo.GetFiles().OrderBy(f => f.Name).ToList();
        var warmup2 = dirInfo.EnumerateFiles().OrderBy(f => f.Name).ToList();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long mem1 = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var list1 = dirInfo.GetFiles().OrderBy(f => f.Name).ToList();
        sw.Stop();
        long mem2 = GC.GetTotalMemory(false);
        Console.WriteLine($"GetFiles: {sw.ElapsedMilliseconds}ms, Memory diff: {mem2 - mem1} bytes");

        list1 = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long mem3 = GC.GetTotalMemory(true);
        sw.Restart();
        var list2 = dirInfo.EnumerateFiles().OrderBy(f => f.Name).ToList();
        sw.Stop();
        long mem4 = GC.GetTotalMemory(false);
        Console.WriteLine($"EnumerateFiles: {sw.ElapsedMilliseconds}ms, Memory diff: {mem4 - mem3} bytes");

        Directory.Delete(testDir, true);
    }
}
