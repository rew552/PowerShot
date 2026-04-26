using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PowerShot.Utils
{
    internal static class IconHelper
    {
        private static Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource GetIcon(string path, bool isDirectory)
        {
            string cacheKey = isDirectory ? "<DIR>" : Path.GetExtension(path).ToLowerInvariant();
            if (string.IsNullOrEmpty(cacheKey)) cacheKey = "<NOEXT>";

            if (_iconCache.ContainsKey(cacheKey))
                return _iconCache[cacheKey];

            var shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON | NativeMethods.SHGFI_USEFILEATTRIBUTES;
            uint fileAttr = isDirectory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY : NativeMethods.FILE_ATTRIBUTE_NORMAL;

            NativeMethods.SHGetFileInfo(
                path, fileAttr, ref shinfo,
                (uint)Marshal.SizeOf(typeof(NativeMethods.SHFILEINFO)), flags);

            ImageSource iconSource = null;
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    iconSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    iconSource.Freeze();
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }

            _iconCache[cacheKey] = iconSource;
            return iconSource;
        }
    }
}
