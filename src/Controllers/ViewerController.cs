using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.Serialization.Json;

namespace PowerShot
{
    public static class ViewerController
    {
        public static void Run(string projectDir)
        {
            try
            {
                string srcDir = Path.Combine(projectDir, "src");
                string settingsPath = Path.Combine(srcDir, "settings.json");
                
                string saveFolder = @".\Screenshots";
                
                if (File.Exists(settingsPath))
                {
                    string content = File.ReadAllText(settingsPath);
                    var match = Regex.Match(content, @"""SaveFolder""\s*:\s*""([^""]*)""");
                    if (match.Success)
                    {
                        saveFolder = match.Groups[1].Value.Replace(@"\\", @"\");
                    }
                }
                
                if (!Path.IsPathRooted(saveFolder))
                {
                    saveFolder = Path.GetFullPath(Path.Combine(srcDir, saveFolder));
                }

                if (!Directory.Exists(saveFolder))
                {
                    Directory.CreateDirectory(saveFolder);
                }
                
                FolderNode rootNode = ScanDirectory(new DirectoryInfo(saveFolder));
                
                string json = "";
                using (MemoryStream ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(FolderNode));
                    serializer.WriteObject(ms, rootNode);
                    json = Encoding.UTF8.GetString(ms.ToArray());
                }
                
                string templatePath = Path.Combine(srcDir, "Views", "viewer_template.html");
                if (!File.Exists(templatePath))
                {
                    Console.WriteLine("Template not found: " + templatePath);
                    return;
                }
                
                string html = File.ReadAllText(templatePath);
                html = html.Replace("\"[[FOLDER_DATA]]\"", json);
                
                string outPath = Path.Combine(srcDir, ".powershot_viewer.html");
                if (File.Exists(outPath))
                {
                    File.SetAttributes(outPath, FileAttributes.Normal);
                    File.Delete(outPath);
                }
                
                File.WriteAllText(outPath, html, Encoding.UTF8);
                File.SetAttributes(outPath, FileAttributes.Hidden);
                
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error generating viewer: " + ex.Message);
            }
        }
        
        private static FolderNode ScanDirectory(DirectoryInfo dir)
        {
            FolderNode node = new FolderNode
            {
                Name = dir.Name,
                Path = "file:///" + dir.FullName.Replace("\\", "/")
            };
            
            try
            {
                foreach (var childDir in dir.GetDirectories())
                {
                    node.Children.Add(ScanDirectory(childDir));
                }
                
                foreach (var file in dir.GetFiles("*.*"))
                {
                    string ext = file.Extension.ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                    {
                        node.Files.Add(new FileItem
                        {
                            Name = file.Name,
                            Path = "file:///" + file.FullName.Replace("\\", "/"),
                            Date = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            Size = file.Length
                        });
                    }
                }
            }
            catch { }
            
            return node;
        }
    }
}

