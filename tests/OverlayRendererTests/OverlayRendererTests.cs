using System;
using System.Drawing;
using System.Reflection;
using Xunit;

namespace PowerShot.Tests
{
    public class OverlayRendererTests
    {
        private const float Padding = 12f;

        private static PointF InvokeGetPosition(SizeF textSize, Rectangle bounds, string position)
        {
            var type = typeof(PowerShot.OverlayRenderer);
            var methodInfo = type.GetMethod("GetPosition", BindingFlags.Static | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                throw new InvalidOperationException("GetPosition method not found.");
            }
            return (PointF)methodInfo.Invoke(null, new object[] { textSize, bounds, position });
        }

        [Fact]
        public void GetPosition_TopLeft_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);
            string position = "TopLeft"; // Defaults to top-left

            var result = InvokeGetPosition(textSize, bounds, position);

            // x = bounds.X + Padding
            // y = bounds.Y + Padding
            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_TopRight_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);
            string position = "TopRight";

            var result = InvokeGetPosition(textSize, bounds, position);

            // rectW = textSize.Width + Padding * 2
            // x = bounds.Right - rectW - Padding
            // y = bounds.Y + Padding
            float rectW = textSize.Width + Padding * 2;
            Assert.Equal(bounds.Right - rectW - Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_BottomLeft_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);
            string position = "BottomLeft";

            var result = InvokeGetPosition(textSize, bounds, position);

            // rectH = textSize.Height + Padding * 2
            // x = bounds.X + Padding
            // y = bounds.Bottom - rectH - Padding
            float rectH = textSize.Height + Padding * 2;
            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Bottom - rectH - Padding, result.Y);
        }

        [Fact]
        public void GetPosition_BottomRight_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);
            string position = "BottomRight";

            var result = InvokeGetPosition(textSize, bounds, position);

            // rectW = textSize.Width + Padding * 2
            // rectH = textSize.Height + Padding * 2
            // x = bounds.Right - rectW - Padding
            // y = bounds.Bottom - rectH - Padding
            float rectW = textSize.Width + Padding * 2;
            float rectH = textSize.Height + Padding * 2;
            Assert.Equal(bounds.Right - rectW - Padding, result.X);
            Assert.Equal(bounds.Bottom - rectH - Padding, result.Y);
        }

        [Fact]
        public void GetPosition_InvalidPosition_DefaultsToTopLeft()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);
            string position = "Center"; // Invalid, should default to TopLeft logic

            var result = InvokeGetPosition(textSize, bounds, position);

            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_OutOfBounds_ClampsToTopLeftOfBounds()
        {
            // If the bounds are too small for the text, x and y might calculate as less than bounds.X or bounds.Y
            // The method currently clamps to:
            // if (x < bounds.X) x = bounds.X;
            // if (y < bounds.Y) y = bounds.Y;

            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 50, 50); // Very small bounds
            string position = "BottomRight";

            var result = InvokeGetPosition(textSize, bounds, position);

            // Since it clamps to the bounds:
            Assert.Equal(bounds.X, result.X);
            Assert.Equal(bounds.Y, result.Y);
        }
    }
}
