using System;
using System.IO;
using System.Text.Json;

namespace Kilo.VisualStudio.Extension.Models
{
    public class ExtensionSettings
    {
        public string KiloApiKey { get; set; } = string.Empty;
        public string BackendPassword { get; set; } = string.Empty;
        public bool UseMockBackend { get; set; } = true;
        public string BackendUrl { get; set; } = "http://127.0.0.1:4096";

        private static string SettingsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kilo.VisualStudio");
        private static string SettingsFile => Path.Combine(SettingsFolder, "settings.json");

        public static ExtensionSettings Load()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                if (!File.Exists(SettingsFile))
                {
                    var defaults = new ExtensionSettings();
                    defaults.Save();
                    return defaults;
                }

                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<ExtensionSettings>(json) ?? new ExtensionSettings();
            }
            catch
            {
                return new ExtensionSettings();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // ignore failures for MVP
            }
        }
    }
}
