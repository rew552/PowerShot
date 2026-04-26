using System;
using System.IO;
using Xunit;
using PowerShot.Utils;

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
                Directory.Delete(_testDirectory, true);
        }

        private void CreateDummyFile(string fileName)
        {
            File.WriteAllText(Path.Combine(_testDirectory, fileName), "dummy");
        }

        [Fact]
        public void GetNextSequence_DirectoryDoesNotExist_Returns1()
        {
            string nonExistentDir = Path.Combine(_testDirectory, "NonExistent");
            Assert.Equal(1, SequenceManager.GetNextSequence(nonExistentDir, "Prefix", "Option"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetNextSequence_PrefixIsNullOrWhiteSpace_Returns1(string prefix)
        {
            Assert.Equal(1, SequenceManager.GetNextSequence(_testDirectory, prefix, "Option"));
        }

        [Fact]
        public void GetNextSequence_NoFilesInDirectory_Returns1()
        {
            Assert.Equal(1, SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Option"));
        }

        [Fact]
        public void GetNextSequence_ValidFilesWithPrefixOnly_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_1.png");
            CreateDummyFile("Prefix_2.jpg");
            CreateDummyFile("Prefix_5.png");

            Assert.Equal(6, SequenceManager.GetNextSequence(_testDirectory, "Prefix", null));
        }

        [Fact]
        public void GetNextSequence_ValidFilesWithPrefixAndOption_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_Opt_1.png");
            CreateDummyFile("Prefix_Opt_3.jpg");
            CreateDummyFile("Prefix_Opt_4.png");

            Assert.Equal(5, SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Opt"));
        }

        [Fact]
        public void GetNextSequence_IgnoresNonMatchingFiles()
        {
            CreateDummyFile("Prefix_Opt_1.png");
            CreateDummyFile("Prefix_Opt_2.txt");
            CreateDummyFile("Prefix_OtherOpt_3.png");
            CreateDummyFile("OtherPrefix_Opt_4.png");
            CreateDummyFile("Prefix_Opt_ABC.png");

            Assert.Equal(2, SequenceManager.GetNextSequence(_testDirectory, "Prefix", "Opt"));
        }

        [Fact]
        public void GetNextSequence_CaseInsensitiveExtension_ReturnsMaxPlusOne()
        {
            CreateDummyFile("Prefix_1.PNG");
            CreateDummyFile("Prefix_2.JPG");
            CreateDummyFile("Prefix_3.pNg");

            Assert.Equal(4, SequenceManager.GetNextSequence(_testDirectory, "Prefix", ""));
        }

        [Fact]
        public void GetNextSequence_HandlesGapsInSequence()
        {
            CreateDummyFile("Prefix_10.png");
            CreateDummyFile("Prefix_20.jpg");

            Assert.Equal(21, SequenceManager.GetNextSequence(_testDirectory, "Prefix", ""));
        }
    }
}
