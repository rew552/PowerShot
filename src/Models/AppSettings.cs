using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PowerShot
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public string SaveFolder { get; set; }

        [DataMember(Order = 4)] public int JpegQuality { get; set; }
        [DataMember(Order = 5)] public bool EmbedSysInfo { get; set; }
        [DataMember(Order = 6)] public string SysInfoPosition { get; set; }
        [DataMember(Order = 7)] public string OverlayText { get; set; }
        [DataMember(Order = 8)] public string OverlayTextPosition { get; set; }

        [DataMember(Order = 12)] public string HotkeyMonitorCapture { get; set; }
        [DataMember(Order = 13)] public bool CropEnabled { get; set; }
        [DataMember(Order = 14)] public int CropX { get; set; }
        [DataMember(Order = 15)] public int CropY { get; set; }
        [DataMember(Order = 16)] public int CropWidth { get; set; }
        [DataMember(Order = 17)] public int CropHeight { get; set; }
        [DataMember(Order = 18)] public bool OverlayEnabled { get; set; }

        public static AppSettings Default()
        {
            return new AppSettings
            {
                SaveFolder = @".\Screenshots",

                JpegQuality = 80,
                EmbedSysInfo = false,
                SysInfoPosition = "TopLeft",
                OverlayText = "",
                OverlayTextPosition = "TopLeft",

                HotkeyMonitorCapture = "Shift+PrintScreen",
                CropEnabled = false,
                CropX = 0,
                CropY = 0,
                CropWidth = 0,
                CropHeight = 0,
                OverlayEnabled = false
            };
        }
    }

    internal static class SettingsManager
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
                    if (settings.JpegQuality <= 0) settings.JpegQuality = 80;
                    if (string.IsNullOrEmpty(settings.SaveFolder)) settings.SaveFolder = @".\Screenshots";
                    return settings;
                }
            }
            catch
            {
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
            catch { }
        }
    }
}
