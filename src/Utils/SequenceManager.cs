using System;
using System.IO;
using System.Text.RegularExpressions;

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
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int seq))
                    {
                        maxSeq = Math.Max(maxSeq, seq);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] 次の連番の取得に失敗しました: " + ex.Message);
            }

            return maxSeq + 1;
        }
    }
}
