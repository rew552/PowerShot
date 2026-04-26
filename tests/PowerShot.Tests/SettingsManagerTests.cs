using System;
using System.IO;
using System.Runtime.Serialization.Json;
using Xunit;
using PowerShot;

public class SettingsManagerTests : IDisposable
{
    private string testFilePath;

    public SettingsManagerTests()
    {
        testFilePath = Path.GetTempFileName();
        if (File.Exists(testFilePath)) {
            File.Delete(testFilePath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(testFilePath))
        {
            File.Delete(testFilePath);
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesDefaultAndReturns()
    {
        var settings = SettingsManager.Load(testFilePath);

        Assert.NotNull(settings);
        Assert.Equal(80, settings.JpegQuality);
        Assert.Equal(@".\Screenshots", settings.SaveFolder);
        Assert.True(File.Exists(testFilePath));
    }

    [Fact]
    public void Load_ValidFile_LoadsSettings()
    {
        var settings = AppSettings.Default();
        settings.JpegQuality = 90;
        settings.SaveFolder = "CustomFolder";

        using (var fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write))
        {
            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            serializer.WriteObject(fs, settings);
        }

        var loadedSettings = SettingsManager.Load(testFilePath);

        Assert.NotNull(loadedSettings);
        Assert.Equal(90, loadedSettings.JpegQuality);
        Assert.Equal("CustomFolder", loadedSettings.SaveFolder);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultSettings()
    {
        File.WriteAllText(testFilePath, "invalid json");

        var loadedSettings = SettingsManager.Load(testFilePath);

        Assert.NotNull(loadedSettings);
        Assert.Equal(80, loadedSettings.JpegQuality);
        Assert.Equal(@".\Screenshots", loadedSettings.SaveFolder);
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

        using (var fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write))
        {
            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            serializer.WriteObject(fs, settings);
        }

        var loadedSettings = SettingsManager.Load(testFilePath);

        Assert.Equal(expectedQuality, loadedSettings.JpegQuality);
    }

    [Fact]
    public void Load_EmptySaveFolder_SetsToDefault()
    {
        var settings = AppSettings.Default();
        settings.SaveFolder = "";

        using (var fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write))
        {
            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            serializer.WriteObject(fs, settings);
        }

        var loadedSettings = SettingsManager.Load(testFilePath);

        Assert.Equal(@".\Screenshots", loadedSettings.SaveFolder);
    }
}
