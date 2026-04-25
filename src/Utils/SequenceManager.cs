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
    internal static class SequenceManager
    {
        /// <summary>
        /// Scans folder for files matching prefix pattern and returns nextSequence number.
        /// </summary>
        public static int GetNextSequence(string directoryPath, string prefix, string optionName)
        {
            if (!Directory.Exists(directoryPath))
                return 1;

            if (string.IsNullOrWhiteSpace(prefix))
                return 1;

            string escapedPrefix = Regex.Escape(prefix);
            string pattern;

            if (!string.IsNullOrWhiteSpace(optionName))
            {
                string escapedOption = Regex.Escape(optionName);
                pattern = string.Format(@"^{0}_{1}_(\d+)\.(png|jpg)$", escapedPrefix, escapedOption);
            }
            else
            {
                pattern = string.Format(@"^{0}_(\d+)\.(png|jpg)$", escapedPrefix);
            }

            int maxSeq = 0;
            try
            {
                var files = Directory.GetFiles(directoryPath);
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        int seq;
                        if (int.TryParse(match.Groups[1].Value, out seq))
                        {
                            if (seq > maxSeq) maxSeq = seq;
                        }
                    }
                }
            }
            catch { }

            return maxSeq + 1;
        }
    }
}
