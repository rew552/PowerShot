using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

class Program
{
    static void Main()
    {
        string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
            <Button Content=""Test""/>
        </Window>";

        try {
            var xmlDoc = new XmlDocument();
            // XmlReaderSettings to avoid XXE
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (var sr = new StringReader(xaml))
            using (var reader = XmlReader.Create(sr, settings))
            {
                xmlDoc.Load(reader);
            }

            var forbidden = new[] { "ObjectDataProvider", "EventSetter", "ResourceDictionary" };
            var elements = xmlDoc.GetElementsByTagName("*");
            foreach (XmlNode el in elements) {
                foreach (var f in forbidden) {
                    if (el.LocalName == f) {
                        throw new Exception("Forbidden XAML element: " + el.Name);
                    }
                }
            }

            // Re-serialize and load
            // Wait, we can just load the xmlDoc or re-read from string if we just want to validate.
            // But actually we can pass the string/stream again since we validated it.
            Console.WriteLine("Parsed safely");
        } catch(Exception e) {
            Console.WriteLine("Caught: " + e.Message);
        }
    }
}
