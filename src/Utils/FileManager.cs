﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PowerShot
{
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

            int index = name.IndexOfAny(ForbiddenChars);
            if (index >= 0)
            {
                return string.Format("ファイル名に使用できない文字が含まれています: '{0}'\n禁則文字: \\ / : * ? \" < > |", name[index]);
            }

            index = name.IndexOfAny(Path.GetInvalidFileNameChars());
            if (index >= 0)
            {
                return string.Format("ファイル名に使用できない文字が含まれています: (Code: {0})", (int)name[index]);
            }

            return null;
        }

        /// <summary>
        /// Generates the filename based on prefix, optionName, sequence, and format.
        /// </summary>
        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format, string timestampTemplate = "yyyyMMdd-HHmmss")
        {
            string ext = format.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(optionName))
            {
                string middle;
                if (timestampTemplate == "SEQ")
                {
                    middle = seqStr;
                }
                else if (timestampTemplate == "yyyyMMdd_SEQ")
                {
                    middle = DateTime.Now.ToString("yyyyMMdd") + "_" + seqStr;
                }
                else
                {
                    middle = DateTime.Now.ToString(timestampTemplate);
                }
                return string.Format("SS_{0}.{1}", middle, ext);
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
