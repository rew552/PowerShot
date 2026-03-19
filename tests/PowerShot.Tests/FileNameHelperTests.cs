using Xunit;
using PowerShot;
using System;
using System.IO;

namespace PowerShot.Tests
{
    public class FileNameHelperTests
    {
        [Theory]
        [InlineData("test", "opt", "001", "jpg", "test_opt_001.jpg")]
        [InlineData("PREFIX", "", "123", "PNG", "PREFIX_123.png")]
        [InlineData("my-file", "v1", "99", "bmp", "my-file_v1_99.bmp")]
        public void GenerateFileName_WithPrefixAndSequence_ReturnsCorrectFormat(
            string prefix, string optionName, string seqStr, string format, string expected)
        {
            // Act
            string result = FileNameHelper.GenerateFileName(prefix, optionName, seqStr, format);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GenerateFileName_EmptyPrefixAndOption_ReturnsTimestampFormat()
        {
            // Arrange
            string prefix = "";
            string optionName = "";
            string seqStr = "ignored";
            string format = "jpg";

            // Act
            string result = FileNameHelper.GenerateFileName(prefix, optionName, seqStr, format);

            // Assert
            // Expected format: SS_yyyyMMdd-HHmmss.jpg
            Assert.StartsWith("SS_", result);
            Assert.EndsWith(".jpg", result);
            // "SS_" (3) + "yyyyMMdd-HHmmss" (15) + ".jpg" (4) = 22
            Assert.Equal(22, result.Length);
        }

        [Theory]
        [InlineData("valid_name")]
        [InlineData("file-123")]
        [InlineData("日本語の名前")]
        [InlineData("")]
        [InlineData(null)]
        public void ValidateName_ValidInputs_ReturnsNull(string name)
        {
            // Act
            string result = FileNameHelper.ValidateName(name);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("test/file")]
        [InlineData("test\\file")]
        [InlineData("name:invalid")]
        [InlineData("file*")]
        [InlineData("query?")]
        [InlineData("\"quoted\"")]
        [InlineData("<tag>")]
        [InlineData("pipe|")]
        public void ValidateName_ForbiddenChars_ReturnsErrorMessage(string name)
        {
            // Act
            string result = FileNameHelper.ValidateName(name);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("ファイル名に使用できない文字が含まれています", result);
        }
    }
}
