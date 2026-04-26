using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xaml;
using System.Xml;

namespace PowerShot
{
    using System.Reflection;

    internal class SafeXamlSchemaContext : XamlSchemaContext
    {
        public SafeXamlSchemaContext() : base(new[] { Assembly.GetExecutingAssembly(), typeof(Window).Assembly })
        {
        }

        protected override XamlType GetXamlType(string xamlNamespace, string name, params XamlType[] typeArguments)
        {
            if (name == "ObjectDataProvider" ||
                name == "AssemblyInstaller" ||
                name == "Process" ||
                name == "ExpandedWrapper" ||
                name == "ResourceDictionary")
            {
                return null;
            }
            return base.GetXamlType(xamlNamespace, name, typeArguments);
        }
    }

    internal static class XamlLoader
    {
        /// <summary>Loads a Window from src/Views/{viewName}.xaml. Returns null if missing or unsafe.</summary>
        public static Window LoadWindow(string scriptDir, string viewName)
        {
            string xamlPath = Path.Combine(scriptDir, "Views", viewName + ".xaml");
            if (!File.Exists(xamlPath)) return null;

            try
            {
                using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
                using (var xmlReader = XmlReader.Create(fs))
                {
                    var schemaContext = new SafeXamlSchemaContext();
                    var xamlReader = new XamlXmlReader(xmlReader, schemaContext);
                    return (Window)System.Windows.Markup.XamlReader.Load(xamlReader);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] XAMLの読み込みに失敗しました (" + viewName + "): " + ex.Message);
                return null;
            }
        }
    }
}
