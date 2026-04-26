using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Xunit;
using PowerShot;

namespace PowerShot.Tests
{
    public class ViewerControllerTests : IDisposable
    {
        private readonly string _testDir;

        public ViewerControllerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PowerShot_ViewerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        private FolderNode InvokeScanDirectory(DirectoryInfo dir)
        {
            var type = typeof(ViewerController);
            var method = type.GetMethod("ScanDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            return (FolderNode)method.Invoke(null, new object[] { dir });
        }

        [Fact]
        public void ScanDirectory_FiltersImageFiles()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.png"), "");
            File.WriteAllText(Path.Combine(_testDir, "test.jpg"), "");
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "");
            File.WriteAllText(Path.Combine(_testDir, "test.exe"), "");

            var result = InvokeScanDirectory(new DirectoryInfo(_testDir));

            Assert.Equal(2, result.Files.Count);
            Assert.Contains(result.Files, f => f.Name == "test.png");
            Assert.Contains(result.Files, f => f.Name == "test.jpg");
            Assert.DoesNotContain(result.Files, f => f.Name == "test.txt");
        }

        [Fact]
        public void ScanDirectory_RecursiveScanning()
        {
            string subDir = Path.Combine(_testDir, "Sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "sub.png"), "");
            File.WriteAllText(Path.Combine(_testDir, "root.png"), "");

            var result = InvokeScanDirectory(new DirectoryInfo(_testDir));

            Assert.Single(result.Files); // root.png
            Assert.Single(result.Children); // Sub folder
            Assert.Single(result.Children[0].Files); // sub.png
            Assert.Equal("Sub", result.Children[0].Name);
        }

        [Fact]
        public void ScanDirectory_FileMetadataSetCorrectly()
        {
            string filePath = Path.Combine(_testDir, "test.png");
            File.WriteAllText(filePath, "some data");
            
            var result = InvokeScanDirectory(new DirectoryInfo(_testDir));
            var file = result.Files[0];

            Assert.Equal("test.png", file.Name);
            Assert.True(file.Size > 0);
            Assert.NotEmpty(file.Date);
            Assert.StartsWith("file:///", file.Path);
        }
    }
}
