using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PowerShot.Core
{
    // ============================================================
    // File Manager — save logic, validation, naming
    // ============================================================
    internal static class FileManager
    {
        private static readonly char[] ForbiddenChars = new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        /// <summary>
        /// Validates that the given name does not contain forbidden characters.
        /// Returns null if valid, or an error message string if invalid.
        /// </summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (char c in ForbiddenChars)
            {
                if (name.IndexOf(c) >= 0)
                {
                    return string.Format("ファイル名に使用できない文字が含まれています: '{0}'\n禁則文字: \\ / : * ? \" < > |", c);
                }
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (name.IndexOf(c) >= 0)
                {
                    return string.Format("ファイル名に使用できない文字が含まれています: (Code: {0})", (int)c);
                }
            }

            return null;
        }

        /// <summary>
        /// Generates the filename based on prefix, optionName, sequence, and format.
        /// </summary>
        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format)
        {
            string ext = format.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(optionName))
            {
                return string.Format("SS_{0}.{1}", DateTime.Now.ToString("yyyyMMdd-HHmmss"), ext);
            }

            string baseName = prefix;
            if (!string.IsNullOrWhiteSpace(optionName))
            {
                baseName += "_" + optionName;
            }
            baseName += "_" + seqStr;
            return baseName + "." + ext;
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

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }
    }
}
