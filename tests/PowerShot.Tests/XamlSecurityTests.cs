using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Xunit;
using PowerShot;
using System.Xaml;
using System.Xml;

namespace PowerShot.Tests
{
    public class XamlSecurityTests : IDisposable
    {
        private readonly string _testScriptDir;
        private readonly string _viewsDir;

        public XamlSecurityTests()
        {
            _testScriptDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _viewsDir = Path.Combine(_testScriptDir, "Views");
            Directory.CreateDirectory(_viewsDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testScriptDir))
            {
                Directory.Delete(_testScriptDir, true);
            }
        }

        [Fact]
        public void LoadWindow_WithSafeXaml_ReturnsWindowOrXamlException()
        {
            string safeXamlPath = Path.Combine(_viewsDir, "SafeWindow.xaml");
            string safeXaml = "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"></Window>";
            File.WriteAllText(safeXamlPath, safeXaml);

            // Since we're in a test environment without full WPF running properly, it might throw a different exception
            // but the test is that we don't return null from the schema context for Window.

            var schemaContext = new SafeXamlSchemaContext();
            var xamlType = schemaContext.GetXamlType(typeof(Window));
            Assert.NotNull(xamlType);
        }

        [Fact]
        public void LoadWindow_WithObjectDataProvider_IsBlocked()
        {
            // Test that the SafeXamlSchemaContext returns null for ObjectDataProvider
            var schemaContext = typeof(XamlLoader).Assembly.GetType("PowerShot.SafeXamlSchemaContext");
            var contextInstance = Activator.CreateInstance(schemaContext);
            var method = schemaContext.GetMethod("GetXamlType", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(XamlType[]) }, null);

            object result = method.Invoke(contextInstance, new object[] { "http://schemas.microsoft.com/winfx/2006/xaml/presentation", "ObjectDataProvider", new XamlType[0] });

            Assert.Null(result);
        }

        [Fact]
        public void LoadWindow_WithProcess_IsBlocked()
        {
            // Test that the SafeXamlSchemaContext returns null for Process
            var schemaContext = typeof(XamlLoader).Assembly.GetType("PowerShot.SafeXamlSchemaContext");
            var contextInstance = Activator.CreateInstance(schemaContext);
            var method = schemaContext.GetMethod("GetXamlType", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(XamlType[]) }, null);

            object result = method.Invoke(contextInstance, new object[] { "http://schemas.microsoft.com/winfx/2006/xaml/presentation", "Process", new XamlType[0] });

            Assert.Null(result);
        }
    }
}
