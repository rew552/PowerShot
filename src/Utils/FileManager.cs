using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PowerShot.Utils
{
    internal static class FileManager
    {
        public static string ValidateName(string name)
        {
            return FileNamingLogic.ValidateName(name);
        }

        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format)
        {
            return FileNamingLogic.GenerateFileName(prefix, optionName, seqStr, format);
        }

        /// <summary>
        /// Saves a Bitmap to the specified directory with the given filename.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public static string SaveImage(Bitmap bitmap, string directory, string fileName, string format, long jpgQuality)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string fullPath = Path.Combine(directory, fileName);

                if (format.Equals("jpg", StringComparison.OrdinalIgnoreCase))
                {
                    var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    if (jpgEncoder != null)
                    {
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, jpgQuality);
                        bitmap.Save(fullPath, jpgEncoder, encoderParams);
                    }
                    else
                    {
                        bitmap.Save(fullPath, ImageFormat.Jpeg);
                    }
                }
                else
                {
                    bitmap.Save(fullPath, ImageFormat.Png);
                }

                return null; // success
            }
            catch (Exception ex)
            {
                return string.Format("ファイルの保存に失敗しました:\n{0}", ex.Message);
            }
        }

        private static ImageCodecInfo[] _codecs;

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            if (_codecs == null)
                _codecs = ImageCodecInfo.GetImageDecoders();

            foreach (var codec in _codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }
    }
}
