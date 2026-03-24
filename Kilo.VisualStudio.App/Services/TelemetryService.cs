using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class TelemetryService
    {
        private readonly string _workspaceRoot;
        private readonly string _telemetryPath;
        private bool _userConsent = false;
        private bool _telemetryEnabled = false;

        public event EventHandler<TelemetryEvent>? EventLogged;

        public bool IsEnabled => _telemetryEnabled && _userConsent;
        public bool HasConsent => _userConsent;

        public TelemetryService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _telemetryPath = Path.Combine(workspaceRoot, ".kilo", "telemetry.json");
            LoadSettings();
        }

        public void SetUserConsent(bool consent)
        {
            _userConsent = consent;
            _telemetryEnabled = consent;
            SaveSettings();
        }

        public void SetTelemetryEnabled(bool enabled)
        {
            if (_userConsent)
            {
                _telemetryEnabled = enabled;
                SaveSettings();
            }
        }

        public Task LogEventAsync(string eventName, Dictionary<string, string>? properties = null)
        {
            if (!IsEnabled)
                return Task.CompletedTask;

            var telemetryEvent = new TelemetryEvent
            {
                EventName = eventName,
                TimestampUtc = DateTimeOffset.UtcNow,
                Properties = properties ?? new Dictionary<string, string>()
            };

            EventLogged?.Invoke(this, telemetryEvent);
            return Task.CompletedTask;
        }

        public Task LogFeatureUsageAsync(string featureName)
        {
            return LogEventAsync("feature_usage", new Dictionary<string, string>
            {
                { "feature", featureName }
            });
        }

        public Task LogErrorAsync(string errorType, string message, string? stackTrace = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "error_type", errorType },
                { "message", message }
            };
            if (!string.IsNullOrEmpty(stackTrace))
            {
                properties["stack_trace"] = stackTrace;
            }

            return LogEventAsync("error", properties);
        }

        public Task LogPerformanceAsync(string operation, long durationMs)
        {
            return LogEventAsync("performance", new Dictionary<string, string>
            {
                { "operation", operation },
                { "duration_ms", durationMs.ToString() }
            });
        }

        public void ClearAllData()
        {
            try
            {
                if (File.Exists(_telemetryPath))
                {
                    File.Delete(_telemetryPath);
                }
            }
            catch { }
        }

        public string GetPrivacyPolicy()
        {
            return @"Kilo for Visual Studio Privacy Policy

DATA COLLECTION:
- Usage telemetry: Feature usage, performance metrics, error reports
- No personal data is collected without explicit consent

DATA STORED LOCALLY:
- All telemetry data is stored locally in the .kilo folder
- You can delete all data at any time

YOUR RIGHTS:
- You can opt-out of telemetry at any time
- You can request deletion of all telemetry data
- No data is shared with third parties

CONTACT:
For privacy concerns, contact support@kilo.ai";
        }

        private void LoadSettings()
        {
            try
            {
                var settingsPath = Path.Combine(_workspaceRoot, ".kilo", "telemetry_settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<TelemetrySettings>(json);
                    if (settings != null)
                    {
                        _userConsent = settings.UserConsent;
                        _telemetryEnabled = settings.TelemetryEnabled;
                    }
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.Combine(_workspaceRoot, ".kilo");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var settings = new TelemetrySettings
                {
                    UserConsent = _userConsent,
                    TelemetryEnabled = _telemetryEnabled
                };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(dir, "telemetry_settings.json"), json);
            }
            catch { }
        }
    }

    public class TelemetryEvent
    {
        public string EventName { get; set; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    public class TelemetrySettings
    {
        public bool UserConsent { get; set; }
        public bool TelemetryEnabled { get; set; }
    }
}