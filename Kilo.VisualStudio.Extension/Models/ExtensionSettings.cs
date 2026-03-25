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
        public string Profile { get; set; } = "Default";
        public string Provider { get; set; } = "OpenAI";
        public string Model { get; set; } = "gpt-4o";

        // Per-profile overrides
        public System.Collections.Generic.Dictionary<string, string> ProfileProviderMapping { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, string> ProfileModelMapping { get; set; } = new();

        public bool AutoApproveTools { get; set; } = false;
        public bool AutoScrollResponses { get; set; } = true;
        public bool PlaySounds { get; set; } = false;
        public bool ShowNotifications { get; set; } = true;
        public bool IncludeActiveFile { get; set; } = true;
        public bool IncludeSelection { get; set; } = true;
        public bool IncludeTerminalOutput { get; set; } = false;
        public bool EnableSemanticSearch { get; set; } = false;
        public bool EnableInlineAutocomplete { get; set; } = true;
        public string AutocompleteTriggerCharacters { get; set; } = ".";
        public bool AutocompleteOnTyping { get; set; } = false;
        public int AutocompleteDelayMs { get; set; } = 300;
        public bool EnableTelemetry { get; set; } = false;
        public bool EnableSessionSharing { get; set; } = true;
        public string LastSessionId { get; set; } = string.Empty;
        public string LastProfile { get; set; } = "Default";

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
