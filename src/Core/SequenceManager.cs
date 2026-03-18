using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PowerShot.Core
{
    // ============================================================
    // Sequence Manager — intelligent auto-incrementing file numbering
    // ============================================================
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

        /// <summary>
        /// Retrieves the next sequence number by examining an existing list of filenames instead of scanning the disk.
        /// Useful for MVVM pattern where file list is already cached.
        /// </summary>
        public static int GetNextSequenceFromList(IEnumerable<string> fileNames, string prefix, string optionName)
        {
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
            foreach (var fileName in fileNames)
            {
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

            return maxSeq + 1;
        }
    }
}
