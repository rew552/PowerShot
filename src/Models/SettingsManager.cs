using System;
using System.IO;
using System.Runtime.Serialization.Json;
using PowerShot.Models;

namespace PowerShot.Models
{
    public static class SettingsManager
    {
        public static AppSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                var def = AppSettings.Default();
                Save(path, def);
                return def;
            }
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    var settings = (AppSettings)serializer.ReadObject(fs);
                    if (settings.JpegQuality <= 0 || settings.JpegQuality > 100) settings.JpegQuality = 80;
                    if (string.IsNullOrEmpty(settings.SaveFolder)) settings.SaveFolder = @".\Screenshots";
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] 設定の読み込みに失敗しました: " + ex.Message);
                return AppSettings.Default();
            }
        }

        public static void Save(string path, AppSettings settings)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(fs, settings);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Warn] 設定の保存に失敗しました: " + ex.Message);
            }
        }
    }
}
