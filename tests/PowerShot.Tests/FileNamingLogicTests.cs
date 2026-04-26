using System;
using Xunit;
using PowerShot.Utils;

namespace PowerShot.Tests
{
    public class FileNamingLogicTests
    {
        [Fact]
        public void GenerateFileName_EmptyPrefixAndOption_ReturnsTimestampFormat()
        {
            var timestamp = new DateTime(2023, 10, 27, 14, 30, 0);

            var result = FileNamingLogic.GenerateFileName("", "", "001", "jpg", timestamp);

            Assert.Equal("SS_20231027_143000.jpg", result);
        }

        [Fact]
        public void GenerateFileName_PrefixOnly_ReturnsPrefixAndSequence()
        {
            var result = FileNamingLogic.GenerateFileName("Capture", "", "001", "png");

            Assert.Equal("Capture_001.png", result);
        }

        [Fact]
        public void GenerateFileName_PrefixAndOption_ReturnsCombinedName()
        {
            var result = FileNamingLogic.GenerateFileName("Prefix", "Option", "042", "JPG");

            Assert.Equal("Prefix_Option_042.jpg", result);
        }

        [Fact]
        public void GenerateFileName_FormatIsLowercased()
        {
            var result = FileNamingLogic.GenerateFileName("test", "", "1", "PNG");

            Assert.Equal("test_1.png", result);
        }

        [Fact]
        public void ValidateName_ValidName_ReturnsNull()
        {
            var result = FileNamingLogic.ValidateName("Valid_File_Name");

            Assert.Null(result);
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("/")]
        [InlineData(":")]
        [InlineData("*")]
        [InlineData("?")]
        [InlineData("\"")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData("|")]
        public void ValidateName_ForbiddenChars_ReturnsErrorMessage(string invalidChar)
        {
            var result = FileNamingLogic.ValidateName("file" + invalidChar + "name");

            Assert.NotNull(result);
            Assert.Contains("ファイル名に使用できない文字が含まれています", result);
            Assert.Contains(invalidChar, result);
        }

        [Fact]
        public void ValidateName_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(FileNamingLogic.ValidateName(null));
            Assert.Null(FileNamingLogic.ValidateName(""));
        }
    }
}
