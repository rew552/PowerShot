using System;
using Xunit;
using PowerShot;

namespace PowerShot.Tests
{
    public class FileNamingLogicTests
    {
        [Fact]
        public void GenerateFileName_EmptyPrefixAndOption_ReturnsTimestampFormat()
        {
            // Arrange
            var timestamp = new DateTime(2023, 10, 27, 14, 30, 0);

            // Act
            var result = FileNamingLogic.GenerateFileName("", "", "001", "jpg", timestamp);

            // Assert
            Assert.Equal("SS_20231027-143000.jpg", result);
        }

        [Fact]
        public void GenerateFileName_PrefixOnly_ReturnsPrefixAndSequence()
        {
            // Act
            var result = FileNamingLogic.GenerateFileName("Capture", "", "001", "png");

            // Assert
            Assert.Equal("Capture_001.png", result);
        }

        [Fact]
        public void GenerateFileName_PrefixAndOption_ReturnsCombinedName()
        {
            // Act
            var result = FileNamingLogic.GenerateFileName("Prefix", "Option", "042", "JPG");

            // Assert
            Assert.Equal("Prefix_Option_042.jpg", result);
        }

        [Fact]
        public void GenerateFileName_FormatIsLowercased()
        {
            // Act
            var result = FileNamingLogic.GenerateFileName("test", "", "1", "PNG");

            // Assert
            Assert.Equal("test_1.png", result);
        }

        [Fact]
        public void ValidateName_ValidName_ReturnsNull()
        {
            // Act
            var result = FileNamingLogic.ValidateName("Valid_File_Name");

            // Assert
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
            // Act
            var result = FileNamingLogic.ValidateName("file" + invalidChar + "name");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("ファイル名に使用できない文字が含まれています", result);
            Assert.Contains(invalidChar, result);
        }

        [Fact]
        public void ValidateName_NullOrEmpty_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(FileNamingLogic.ValidateName(null));
            Assert.Null(FileNamingLogic.ValidateName(""));
        }
    }
}
