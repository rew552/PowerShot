using PowerShot;

namespace FileManagerTests
{
    public class ValidateNameTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ValidateName_ReturnsNull_ForEmptyOrNull(string name)
        {
            var result = FileManager.ValidateName(name);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("valid_name")]
        [InlineData("filename.txt")]
        [InlineData("file name with spaces")]
        [InlineData("12345")]
        public void ValidateName_ReturnsNull_ForValidNames(string name)
        {
            var result = FileManager.ValidateName(name);
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
        [InlineData("name\\with\\backslash")]
        [InlineData("name/with/slash")]
        public void ValidateName_ReturnsErrorMessage_ForForbiddenChars(string name)
        {
            var result = FileManager.ValidateName(name);
            Assert.NotNull(result);
            Assert.Contains("ファイル名に使用できない文字が含まれています", result);
        }

        [Fact]
        public void ValidateName_ReturnsErrorMessage_ForInvalidPathChars()
        {
            // \u0000 is usually an invalid character in filenames
            string invalidChar = "\0";
            var result = FileManager.ValidateName("invalid" + invalidChar);

            // Note: Path.GetInvalidFileNameChars() might vary by platform,
            // but control characters are generally invalid on most.
            Assert.NotNull(result);
            Assert.Contains("ファイル名に使用できない文字が含まれています", result);
        }
    }
}
