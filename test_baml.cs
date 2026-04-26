using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

class Program
{
    static void Main()
    {
        // Try creating a safe XAML reader configuration
        // In WPF, there is no built-in way to sandbox XamlReader.Load without creating an AppDomain which is obsolete.
        // However, we can use an XmlReader with secure settings and restrict types by using XamlSchemaContext or XamlXmlReader.
        // But since this is .NET Framework/Core WPF, maybe we can write a wrapper that checks elements.
        string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Button Content=""Test""/></Window>";

        using (var sr = new StringReader(xaml))
        using (var xr = XmlReader.Create(sr))
        {
            try {
                // To securely load XAML, we should parse it manually or prevent specific types like ObjectDataProvider.
                // A simpler way: we can read the XAML string, check for forbidden strings like "ObjectDataProvider" and "EventSetter".
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }
    }
}
