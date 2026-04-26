using System;
using System.IO;
using Xunit;
using PowerShot;

namespace PowerShot.Tests
{
    public class SequenceManagerTests : IDisposable
    {
        private readonly string _testDirectory;

        public SequenceManagerTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PowerShot_SequenceManagerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        private void CreateDummyFile(string fileName)
        {
            File.WriteAllText(Path.Combine(_testDirectory, fileName), "dummy");
        }

        [Fact]
        public void GetNextSequence_DirectoryDoesNotExist_Returns1()
        {
            string nonExistentDir = Path.Combine(_testDirectory, "NonExistent");
            int result = SequenceManager.GetNextSequence(nonExistentDir, "Prefix", "Option");
            Assert.Equal(1, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetNextSequence_PrefixIsNullOrWhiteSpace_Returns1(string prefix)
        {
            int result = SequenceManager.GetNextSequence(_testDirectory, prefix, "Option");
            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNextSequence_NoFilesInDirectory_Returns1()
        {
            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Option");
            Assert.Equal(1, result);
        }

        [Fact]
        public void GetNextSequence_ValidFilesWithPrefixOnly_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_1.png");
            CreateDummyFile("Prefix_2.jpg");
            CreateDummyFile("Prefix_5.png");

            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", null);
            Assert.Equal(6, result);
        }

        [Fact]
        public void GetNextSequence_ValidFilesWithPrefixAndOption_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_Opt_1.png");
            CreateDummyFile("Prefix_Opt_3.jpg");
            CreateDummyFile("Prefix_Opt_4.png");

            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Opt");
            Assert.Equal(5, result);
        }

        [Fact]
        public void GetNextSequence_IgnoresNonMatchingFiles()
        {
            CreateDummyFile("Prefix_Opt_1.png");
            CreateDummyFile("Prefix_Opt_2.txt"); // Wrong extension
            CreateDummyFile("Prefix_OtherOpt_3.png"); // Wrong option
            CreateDummyFile("OtherPrefix_Opt_4.png"); // Wrong prefix
            CreateDummyFile("Prefix_Opt_ABC.png"); // Not a number

            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Opt");
            Assert.Equal(2, result);
        }

        [Fact]
        public void GetNextSequence_CaseInsensitiveExtension_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_1.PNG");
            CreateDummyFile("Prefix_2.JPG");
            CreateDummyFile("Prefix_3.pNg");

            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", "");
            Assert.Equal(4, result);
        }

        [Fact]
        public void GetNextSequence_HandlesGapsInSequence()
        {
            CreateDummyFile("Prefix_10.png");
            CreateDummyFile("Prefix_20.jpg");

            int result = SequenceManager.GetNextSequence(_testDirectory, "Prefix", "");
            Assert.Equal(21, result);
        }
    }
}