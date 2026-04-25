using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace PowerShot
{
    public class ClipboardWatcher : IDisposable
    {
        private const int MonitorCaptureHotkeyId = 1;

        private HwndSource _hwndSource;
        private bool _hotkeyRegistered;

        private string _lastImageHash;
        private bool _isWindowOpen;

        private AppSettings _settings;
        private string _scriptPath;
        private SessionState _session;

        public ClipboardWatcher(string scriptPath, AppSettings settings, SessionState session)
        {
            _scriptPath = scriptPath;
            _settings = settings;
            _session = session;
            _isWindowOpen = false;

            string projectRoot = Path.GetDirectoryName(_scriptPath);
            string saveDir = Path.GetFullPath(Path.Combine(projectRoot, _settings.SaveFolder));
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

            // Hardcoded Shift+PrintScreen for active-monitor capture.
            // Failure here just means another app owns the combo — not fatal.
            _hotkeyRegistered = NativeMethods.RegisterHotKey(
                _hwndSource.Handle, MonitorCaptureHotkeyId,
                NativeMethods.MOD_SHIFT, NativeMethods.VK_SNAPSHOT);
            if (!_hotkeyRegistered)
            {
                Console.WriteLine("  [Warn] Shift+PrintScreen の登録に失敗しました (他アプリと競合の可能性)。");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                handled = true;
                ProcessClipboard();
            }
            else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == MonitorCaptureHotkeyId)
            {
                handled = true;
                CaptureActiveMonitor();
            }
            return IntPtr.Zero;
        }

        private void CaptureActiveMonitor()
        {
            if (_isWindowOpen) return;

            try
            {
                var cursor = System.Windows.Forms.Cursor.Position;
                var screen = System.Windows.Forms.Screen.FromPoint(cursor);
                var bounds = screen.Bounds;

                using (var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }

                    // Push to clipboard; this fires WM_CLIPBOARDUPDATE and the normal save flow takes over.
                    IntPtr hBitmap = bmp.GetHbitmap();
                    try
                    {
                        var src = Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        System.Windows.Clipboard.SetImage(src);
                    }
                    finally
                    {
                        NativeMethods.DeleteObject(hBitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] アクティブモニターのキャプチャに失敗: " + ex.Message);
            }
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
                        if (fmt.IndexOf("XML Spreadsheet", StringComparison.OrdinalIgnoreCase) >= 0)
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
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] クリップボード処理中にエラーが発生しました: " + ex.Message);
            }
        }

        private void ShowMainWindow(Bitmap bitmap)
        {
            _isWindowOpen = true;

            try
            {
                Window mainWindow = XamlLoader.LoadWindow(_scriptPath, "MainWindow");
                if (mainWindow == null)
                {
                    MessageBox.Show("MainWindow.xaml が見つかりません。",
                        "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var controller = new MainWindowController(mainWindow, bitmap, _scriptPath, _settings, _session);

                mainWindow.Closed += (s, e) =>
                {
                    _isWindowOpen = false;
                    // Clear clipboard after save to avoid re-triggering
                    if (controller.Saved)
                    {
                        try { System.Windows.Clipboard.Clear(); } catch (Exception ex) { Console.WriteLine("  [Error] クリップボードのクリアに失敗しました: " + ex.Message); }
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
                ms.Position = 0;
                byte[] hashBytes = sha256.ComputeHash(ms);
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
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] Bitmap への変換に失敗しました: " + ex.Message);
                return null;
            }
        }

        public void Dispose()
        {
            if (_hwndSource != null)
            {
                if (_hotkeyRegistered)
                {
                    NativeMethods.UnregisterHotKey(_hwndSource.Handle, MonitorCaptureHotkeyId);
                    _hotkeyRegistered = false;
                }
                NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }
    }
}

