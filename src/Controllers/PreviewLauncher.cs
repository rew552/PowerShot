using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PowerShot.Utils;


namespace PowerShot.Controllers
{
    internal static class PreviewLauncher
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public static void Show(string scriptDir, Window owner, string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (Array.IndexOf(SupportedExtensions, ext) < 0) return;

            try
            {
                Window previewWindow = XamlLoader.LoadWindow(scriptDir, "PreviewWindow");
                if (previewWindow == null)
                {
                    MessageBox.Show("PreviewWindow.xaml が見つかりません。",
                        "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                previewWindow.Title = "PowerShot - " + Path.GetFileName(filePath);
                previewWindow.Owner = owner;

                var previewImage = (System.Windows.Controls.Image)previewWindow.FindName("PreviewImage");
                var previewTitle = (TextBlock)previewWindow.FindName("PreviewTitle");

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(filePath, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();

                previewImage.Source = bi;
                if (previewTitle != null)
                {
                    previewTitle.Text = Path.GetFileName(filePath);
                }

                // Window sizing based on image and WorkArea
                double imgW = bi.PixelWidth;
                double imgH = bi.PixelHeight;
                
                // Get DPI scale from the owner window
                PresentationSource source = PresentationSource.FromVisual(owner);
                double dpiX = 1.0;
                double dpiY = 1.0;
                if (source != null && source.CompositionTarget != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                double logicalImgW = imgW / dpiX;
                double logicalImgH = imgH / dpiY;

                // Add padding for window chrome (title, margins)
                logicalImgW += 16; 
                logicalImgH += 48;

                double maxW = SystemParameters.WorkArea.Width * 0.9;
                double maxH = SystemParameters.WorkArea.Height * 0.9;

                previewWindow.Width = Math.Min(logicalImgW, maxW);
                previewWindow.Height = Math.Min(logicalImgH, maxH);
                previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // Force layout update to properly measure ScrollViewer
                previewWindow.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                previewWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("プレビューの表示に失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
