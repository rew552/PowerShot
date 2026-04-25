using System;
using Xunit;
using PowerShot;

namespace PowerShot.Tests
{
    public class GenerateFileNameTests
    {
        [Fact]
        public void GenerateFileName_EmptyPrefixAndOption_UsesDateAndFormat()
        {
            // Arrange
            string prefix = "";
            string optionName = "  "; // whitespace should count as empty
            string seqStr = "001";
            string format = "PNG";
            DateTime testDate = new DateTime(2023, 10, 25, 14, 30, 45);

            // Act
            string result = FileManager.GenerateFileName(prefix, optionName, seqStr, format, testDate);

            // Assert
            // "yyyyMMdd_HHmmss" -> "20231025_143045"
            // string.Format("SS_{0}.{1}", middle, ext) -> "SS_20231025_143045.png"
            Assert.Equal("SS_20231025_143045.png", result);
        }

        [Fact]
        public void GenerateFileName_NullPrefixAndOption_UsesDateAndFormat()
        {
            // Arrange
            string prefix = null;
            string optionName = null;
            string seqStr = "002";
            string format = "JPG";
            DateTime testDate = new DateTime(2024, 01, 05, 9, 15, 0);

            // Act
            string result = FileManager.GenerateFileName(prefix, optionName, seqStr, format, testDate);

            // Assert
            Assert.Equal("SS_20240105_091500.jpg", result);
        }

        [Fact]
        public void GenerateFileName_ValidPrefix_EmptyOption_IncludesPrefixAndSequence()
        {
            // Arrange
            string prefix = "MyPrefix";
            string optionName = "";
            string seqStr = "003";
            string format = "png";
            DateTime testDate = new DateTime(2023, 10, 25, 14, 30, 45); // Should not be used

            // Act
            string result = FileManager.GenerateFileName(prefix, optionName, seqStr, format, testDate);

            // Assert
            Assert.Equal("MyPrefix_003.png", result);
        }

        [Fact]
        public void GenerateFileName_EmptyPrefix_ValidOption_IncludesOptionAndSequence()
        {
            // Arrange
            string prefix = "";
            string optionName = "MyOption";
            string seqStr = "004";
            string format = "bmp";
            DateTime testDate = new DateTime(2023, 10, 25, 14, 30, 45); // Should not be used

            // Act
            string result = FileManager.GenerateFileName(prefix, optionName, seqStr, format, testDate);

            // Assert
            Assert.Equal("_MyOption_004.bmp", result);
        }

        [Fact]
        public void GenerateFileName_ValidPrefix_ValidOption_IncludesBothAndSequence()
        {
            // Arrange
            string prefix = "MyPrefix";
            string optionName = "MyOption";
            string seqStr = "005";
            string format = "JPEG";
            DateTime testDate = new DateTime(2023, 10, 25, 14, 30, 45); // Should not be used

            // Act
            string result = FileManager.GenerateFileName(prefix, optionName, seqStr, format, testDate);

            // Assert
            Assert.Equal("MyPrefix_MyOption_005.jpeg", result);
        }

        [Fact]
        public void GenerateFileName_PublicMethod_DoesNotThrow()
        {
            // Arrange
            string prefix = "Prefix";
            string optionName = "Option";
            string seqStr = "001";
            string format = "png";

            // Act
            var result = FileManager.GenerateFileName(prefix, optionName, seqStr, format);

            // Assert
            Assert.Equal("Prefix_Option_001.png", result);
        }
    }
}
