using System;
using System.IO;
using System.Xaml;

class Program
{
    static void Main()
    {
        string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
            <Window.Resources>
                <ObjectDataProvider x:Key=""obj"" ObjectType=""{x:Type x:String}"" MethodName=""Trim""/>
            </Window.Resources>
            <Button Content=""Test""/>
        </Window>";

        try {
            var reader = new XamlXmlReader(new StringReader(xaml));
            var context = new XamlSchemaContext();

            // Can we restrict types here?
            Console.WriteLine(context.GetType().Name);
        } catch(Exception e) {
            Console.WriteLine("Caught: " + e.Message);
        }
    }
}
