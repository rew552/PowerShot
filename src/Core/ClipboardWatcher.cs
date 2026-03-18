using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using PowerShot.Models;
using PowerShot.ViewModels;

namespace PowerShot.Core
{
    // ============================================================
    // Clipboard Monitor — hooks WM_CLIPBOARDUPDATE via HwndSource
    // ============================================================
    public class ClipboardWatcher : IDisposable
    {
        private HwndSource _hwndSource;

        private string _lastImageHash;
        private bool _isWindowOpen;

        private string _rootBoundary;
        private string _scriptPath;
        private SessionState _session;

        public ClipboardWatcher(string scriptPath, string saveDir, SessionState session)
        {
            _scriptPath = scriptPath;
            _rootBoundary = saveDir;
            _session = session;
            _isWindowOpen = false;

            // Ensure Screenshots directory exists
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
        }

        public void Start()
        {
            // Create a hidden window for clipboard message hooking
            var parameters = new HwndSourceParameters("PowerShotClipboardHook")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0 // invisible
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            NativeMethods.AddClipboardFormatListener(_hwndSource.Handle);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                handled = true;
                ProcessClipboard();
            }
            return IntPtr.Zero;
        }

        private void ProcessClipboard()
        {
            if (_isWindowOpen) return;

            try
            {
                var dataObj = System.Windows.Clipboard.GetDataObject();
                if (dataObj == null) return;

                // --- Excel filter: if clipboard contains Excel-specific formats, ignore entirely ---
                var formats = dataObj.GetFormats();
                if (formats != null)
                {
                    foreach (var fmt in formats)
                    {
                        if (fmt.IndexOf("XML Spreadsheet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            fmt.Equals("HTML Format", StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Excel cell copy detected — ignore
                        }
                    }
                }

                // Check for image data
                if (!System.Windows.Clipboard.ContainsImage()) return;

                var bitmapSource = System.Windows.Clipboard.GetImage();
                if (bitmapSource == null) return;

                // Convert to System.Drawing.Bitmap for processing
                Bitmap bitmap = BitmapSourceToBitmap(bitmapSource);
                if (bitmap == null) return;

                // --- Small image filter ---
                if (bitmap.Width < 20 && bitmap.Height < 20)
                {
                    bitmap.Dispose();
                    return;
                }

                // --- Duplicate hash check ---
                string hash = ComputeImageHash(bitmap);
                if (hash == _lastImageHash)
                {
                    bitmap.Dispose();
                    return;
                }
                _lastImageHash = hash;

                // Fire event to show UI
                ShowMainWindow(bitmap);
            }
            catch { }
        }

        private void ShowMainWindow(Bitmap bitmap)
        {
            _isWindowOpen = true;

            try
            {
                string xamlPath = Path.Combine(_scriptPath, "MainWindow.xaml");
                if (!File.Exists(xamlPath))
                {
                    MessageBox.Show("MainWindow.xaml が見つかりません。",
                        "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Window mainWindow;
                using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
                {
                    mainWindow = (Window)XamlReader.Load(fs);
                }

                // MVVM Pattern
                var viewModel = new MainViewModel(mainWindow, bitmap, _scriptPath, _rootBoundary, _session);
                mainWindow.DataContext = viewModel;

                mainWindow.Closed += (s, e) =>
                {
                    _isWindowOpen = false;
                    // Clear clipboard after save to avoid re-triggering
                    if (viewModel.Saved)
                    {
                        try { System.Windows.Clipboard.Clear(); } catch { }
                        _lastImageHash = null;
                    }
                };

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _isWindowOpen = false;
                MessageBox.Show(
                    string.Format("UIの表示に失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ComputeImageHash(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            using (var sha256 = SHA256.Create())
            {
                bitmap.Save(ms, ImageFormat.Bmp);
                byte[] hashBytes = sha256.ComputeHash(ms.ToArray());
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private static Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            try
            {
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                int stride = width * ((source.Format.BitsPerPixel + 7) / 8);

                // Ensure Bgra32 format
                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = source;
                converted.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                converted.EndInit();

                stride = width * 4;
                byte[] pixels = new byte[height * stride];
                converted.CopyPixels(pixels, stride, 0);

                var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                bitmap.UnlockBits(bmpData);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_hwndSource != null)
            {
                NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }
    }
}
