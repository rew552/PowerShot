using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

class Program
{
    static void Main()
    {
        string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                            <ObjectDataProvider />
                        </Window>";

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
