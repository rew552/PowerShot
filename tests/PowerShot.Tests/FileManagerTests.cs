using System;
using System.Drawing;
using System.IO;
using Xunit;
using PowerShot.Utils;

namespace PowerShot.Tests
{
    public class FileManagerTests : IDisposable
    {
        private readonly string _testDir;

        public FileManagerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PowerShot_FileManagerTests_" + Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact]
        public void SaveImage_CreatesDirectoryIfMissing()
        {
            using (var bmp = new Bitmap(10, 10))
            {
                string result = FileManager.SaveImage(bmp, _testDir, "test.png", "png", 80);
                
                Assert.Null(result); // success
                Assert.True(Directory.Exists(_testDir));
                Assert.True(File.Exists(Path.Combine(_testDir, "test.png")));
            }
        }

        [Fact]
        public void SaveImage_SavesAsJpgWithQuality()
        {
            using (var bmp = new Bitmap(100, 100))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Red);
                }

                string result = FileManager.SaveImage(bmp, _testDir, "test.jpg", "jpg", 10);
                Assert.Null(result);
                
                FileInfo fi = new FileInfo(Path.Combine(_testDir, "test.jpg"));
                long sizeLow = fi.Length;

                string result2 = FileManager.SaveImage(bmp, _testDir, "test_high.jpg", "jpg", 100);
                Assert.Null(result2);
                
                FileInfo fiHigh = new FileInfo(Path.Combine(_testDir, "test_high.jpg"));
                long sizeHigh = fiHigh.Length;

                // Quality 100 should generally be larger than quality 10
                Assert.True(sizeHigh > sizeLow);
            }
        }

        [Fact]
        public void SaveImage_InvalidPath_ReturnsErrorMessage()
        {
            using (var bmp = new Bitmap(10, 10))
            {
                // Use an invalid path (e.g., a path that is actually a file)
                string filePath = Path.Combine(_testDir, "not_a_dir");
                Directory.CreateDirectory(_testDir);
                File.WriteAllText(filePath, "content");

                string result = FileManager.SaveImage(bmp, filePath, "test.png", "png", 80);
                
                Assert.NotNull(result);
                Assert.Contains("失敗しました", result);
            }
        }
    }
}
