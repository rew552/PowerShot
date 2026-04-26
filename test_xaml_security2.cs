using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

class Program
{
    static void Main()
    {
        string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Button Content=""Test""/></Window>";

        using (var sr = new StringReader(xaml))
        using (var xr = XmlReader.Create(sr))
        {
            var reader = new XamlReader();
            var manager = new XamlDesignerSerializationManager(xr);
            manager.XamlWriterMode = XamlWriterMode.Expression;
            // Trying to see what APIs are available on XamlReader
            Console.WriteLine(typeof(XamlReader).GetMethods().Length);
        }
    }
}
