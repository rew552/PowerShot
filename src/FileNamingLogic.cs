using System;
using System.IO;

namespace PowerShot
{
    public static class FileNamingLogic
    {
        public static readonly char[] ForbiddenChars = new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        public static string? ValidateName(string name)
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

        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format)
        {
            return GenerateFileName(prefix, optionName, seqStr, format, DateTime.Now);
        }

        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format, DateTime timestamp)
        {
            string ext = format.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(optionName))
            {
                return string.Format("SS_{0}.{1}", timestamp.ToString("yyyyMMdd_HHmmss"), ext);
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
