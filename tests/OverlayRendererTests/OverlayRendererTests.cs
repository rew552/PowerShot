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
            var type = typeof(PowerShot.Utils.OverlayRenderer);
            var methodInfo = type.GetMethod("GetPosition", BindingFlags.Static | BindingFlags.NonPublic);
            if (methodInfo == null)
                throw new InvalidOperationException("GetPosition method not found.");
            return (PointF)methodInfo.Invoke(null, new object[] { textSize, bounds, position });
        }

        [Fact]
        public void GetPosition_TopLeft_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);

            var result = InvokeGetPosition(textSize, bounds, "TopLeft");

            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_TopRight_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);

            var result = InvokeGetPosition(textSize, bounds, "TopRight");

            float rectW = textSize.Width + Padding * 2;
            Assert.Equal(bounds.Right - rectW - Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_BottomLeft_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);

            var result = InvokeGetPosition(textSize, bounds, "BottomLeft");

            float rectH = textSize.Height + Padding * 2;
            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Bottom - rectH - Padding, result.Y);
        }

        [Fact]
        public void GetPosition_BottomRight_ReturnsCorrectPosition()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 500, 300);

            var result = InvokeGetPosition(textSize, bounds, "BottomRight");

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

            var result = InvokeGetPosition(textSize, bounds, "Center");

            Assert.Equal(bounds.X + Padding, result.X);
            Assert.Equal(bounds.Y + Padding, result.Y);
        }

        [Fact]
        public void GetPosition_OutOfBounds_ClampsToTopLeftOfBounds()
        {
            var textSize = new SizeF(100, 50);
            var bounds = new Rectangle(10, 20, 50, 50);

            var result = InvokeGetPosition(textSize, bounds, "BottomRight");

            Assert.Equal(bounds.X, result.X);
            Assert.Equal(bounds.Y, result.Y);
        }
    }
}
