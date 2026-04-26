using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace PowerShot.Utils
{
    internal static class XamlLoader
    {
        /// <summary>Loads a Window from src/Views/{viewName}.xaml. Returns null if missing.</summary>
        public static Window LoadWindow(string scriptDir, string viewName)
        {
            string xamlPath = Path.Combine(scriptDir, "Views", viewName + ".xaml");
            if (!File.Exists(xamlPath)) return null;

            using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
            {
                return (Window)XamlReader.Load(fs);
            }
        }
    }
}
