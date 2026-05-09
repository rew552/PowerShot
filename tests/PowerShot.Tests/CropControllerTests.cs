using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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

        [Fact]
        public void HitTest_ScaleConversion_VerifiesMargin()
        {
            bool testPassed = false;
            var t = new Thread(() =>
            {
                var canvas = new Canvas { Width = 1920, Height = 1080 };
                // Simulate rendering scale:
                canvas.Measure(new Size(480, 270));
                canvas.Arrange(new Rect(0, 0, 480, 270));
                
                var rect = new System.Windows.Shapes.Rectangle { Width = 500, Height = 500 };
                Canvas.SetLeft(rect, 100);
                Canvas.SetTop(rect, 100);
                rect.Visibility = Visibility.Visible;
                canvas.Children.Add(rect);

                var controller = new CropController(canvas, rect, null, null, null, null, null, null, null);
                
                var type = typeof(CropController);
                var hitTestMethod = type.GetMethod("HitTest", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Edge margin is 12 screen pixels. 
                // ActualWidth (480) / Width (1920) = 0.25 scale.
                // So effective margin on Canvas should be 12 / 0.25 = 48 logical pixels.
                // Left edge is at 100. So X = 100 - 3 = 97 is within margin (which is 48).
                var p = new System.Windows.Point(97, 150); // 3 logical pixels away
                
                var result = hitTestMethod.Invoke(controller, new object[] { p });
                // Should return ResizeLeft (which is enum value 9 or similar, let's just check it's not DrawNew or None)
                var resultStr = result.ToString();
                
                Assert.Equal("ResizeLeft", resultStr);
                testPassed = true;
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            
            Assert.True(testPassed);
        }
    }
}
