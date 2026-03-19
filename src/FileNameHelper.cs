using System;
using System.IO;

namespace PowerShot
{
    public static class FileNameHelper
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
    }
}
