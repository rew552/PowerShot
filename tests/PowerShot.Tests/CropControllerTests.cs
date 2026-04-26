using System.Drawing;
using Xunit;
using PowerShot.Controllers;

namespace PowerShot.Tests
{
    public class CropControllerTests
    {
        [Fact]
        public void ClampRect_WithinBounds_ReturnsSameRect()
        {
            var result = CropController.ClampRect(10, 20, 100, 50, 500, 300);
            
            Assert.Equal(10, result.X);
            Assert.Equal(20, result.Y);
            Assert.Equal(100, result.Width);
            Assert.Equal(50, result.Height);
        }

        [Fact]
        public void ClampRect_NegativeCoordinates_ClampsToZero()
        {
            var result = CropController.ClampRect(-10, -20, 100, 50, 500, 300);
            
            Assert.Equal(0, result.X);
            Assert.Equal(0, result.Y);
        }

        [Fact]
        public void ClampRect_TooLarge_ClampsToSourceSize()
        {
            var result = CropController.ClampRect(0, 0, 1000, 1000, 500, 300);
            
            Assert.Equal(500, result.Width);
            Assert.Equal(300, result.Height);
        }

        [Fact]
        public void ClampRect_TooSmall_ClampsToMinSize()
        {
            // MinSize is 10.0 in CropController
            var result = CropController.ClampRect(0, 0, 5, 5, 500, 300);
            
            Assert.Equal(10, result.Width);
            Assert.Equal(10, result.Height);
        }

        [Fact]
        public void ClampRect_PositionOutOfBounds_ClampsToSafeMax()
        {
            var result = CropController.ClampRect(500, 300, 100, 100, 500, 300);
            
            // Should be clamped to srcW - min, srcH - min
            Assert.Equal(490, result.X);
            Assert.Equal(290, result.Y);
            Assert.Equal(10, result.Width);
            Assert.Equal(10, result.Height);
        }
    }
}
