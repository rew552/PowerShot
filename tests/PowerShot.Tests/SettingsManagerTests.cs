using System;
using System.IO;
using System.Runtime.Serialization.Json;
using Xunit;
using PowerShot.Models;

namespace PowerShot.Tests
{
    public class SettingsManagerTests : IDisposable
    {
        private readonly string _testFilePath;

        public SettingsManagerTests()
        {
            _testFilePath = Path.GetTempFileName();
            File.Delete(_testFilePath);
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        [Fact]
        public void Load_MissingFile_CreatesDefaultAndReturns()
        {
            var settings = SettingsManager.Load(_testFilePath);

            Assert.NotNull(settings);
            Assert.Equal(80, settings.JpegQuality);
            Assert.Equal(@".\Screenshots", settings.SaveFolder);
            Assert.True(File.Exists(_testFilePath));
        }

        [Fact]
        public void Load_ValidFile_LoadsSettings()
        {
            var settings = AppSettings.Default();
            settings.JpegQuality = 90;
            settings.SaveFolder = "CustomFolder";

            using (var fs = new FileStream(_testFilePath, FileMode.Create, FileAccess.Write))
            {
                new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(fs, settings);
            }

            var loaded = SettingsManager.Load(_testFilePath);

            Assert.Equal(90, loaded.JpegQuality);
            Assert.Equal("CustomFolder", loaded.SaveFolder);
        }

        [Fact]
        public void Load_InvalidJson_ReturnsDefaultSettings()
        {
            File.WriteAllText(_testFilePath, "invalid json");

            var loaded = SettingsManager.Load(_testFilePath);

            Assert.Equal(80, loaded.JpegQuality);
            Assert.Equal(@".\Screenshots", loaded.SaveFolder);
        }

        [Theory]
        [InlineData(0, 80)]
        [InlineData(-10, 80)]
        [InlineData(101, 80)]
        [InlineData(150, 80)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        public void Load_JpegQualityBounds_CorrectsInvalidValues(int savedQuality, int expectedQuality)
        {
            var settings = AppSettings.Default();
            settings.JpegQuality = savedQuality;

            using (var fs = new FileStream(_testFilePath, FileMode.Create, FileAccess.Write))
            {
                new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(fs, settings);
            }

            var loaded = SettingsManager.Load(_testFilePath);

            Assert.Equal(expectedQuality, loaded.JpegQuality);
        }

        [Fact]
        public void Load_EmptySaveFolder_SetsToDefault()
        {
            var settings = AppSettings.Default();
            settings.SaveFolder = "";

            using (var fs = new FileStream(_testFilePath, FileMode.Create, FileAccess.Write))
            {
                new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(fs, settings);
            }

            var loaded = SettingsManager.Load(_testFilePath);

            Assert.Equal(@".\Screenshots", loaded.SaveFolder);
        }
    }
}
