using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.Extension.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloSettingsControl : UserControl
    {
        private ExtensionSettings _settings = new ExtensionSettings();

        public KiloSettingsControl()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = ExtensionSettings.Load();
            ApiKeyBox.Password = _settings.KiloApiKey;
            BackendUrlBox.Text = _settings.BackendUrl;

            ProfileComboBox.SelectedItem = ProfileComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Content?.ToString(), _settings.Profile, StringComparison.OrdinalIgnoreCase));
            if (ProfileComboBox.SelectedItem == null) ProfileComboBox.SelectedIndex = 0;

            TelemetryConsentCheckBox.IsChecked = _settings.EnableTelemetry;
            SessionShareCheckBox.IsChecked = _settings.EnableSessionSharing;

            ApplyProfileProviderModel();
        }

        private void ApplyProfileProviderModel()
        {
            if (_settings == null) return;

            var profile = _settings.Profile ?? "Default";
            if (_settings.ProfileProviderMapping.TryGetValue(profile, out var mappedProvider))
            {
                _settings.Provider = mappedProvider;
            }

            if (_settings.ProfileModelMapping.TryGetValue(profile, out var mappedModel))
            {
                _settings.Model = mappedModel;
            }

            ProviderComboBox.SelectedItem = ProviderComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Content?.ToString(), _settings.Provider, StringComparison.OrdinalIgnoreCase));
            if (ProviderComboBox.SelectedItem == null) ProviderComboBox.SelectedIndex = 0;

            ModelComboBox.SelectedItem = ModelComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Content?.ToString(), _settings.Model, StringComparison.OrdinalIgnoreCase));
            if (ModelComboBox.SelectedItem == null) ModelComboBox.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.KiloApiKey = ApiKeyBox.Password;
            _settings.BackendUrl = BackendUrlBox.Text;
            _settings.Provider = ((ComboBoxItem?)ProviderComboBox.SelectedItem)?.Content?.ToString() ?? "OpenAI";
            _settings.Model = ((ComboBoxItem?)ModelComboBox.SelectedItem)?.Content?.ToString() ?? "gpt-4o";

            _settings.EnableTelemetry = TelemetryConsentCheckBox.IsChecked == true;
            _settings.EnableSessionSharing = SessionShareCheckBox.IsChecked == true;

            // persist per-profile choices
            var profile = _settings.Profile ?? "Default";
            _settings.ProfileProviderMapping[profile] = _settings.Provider;
            _settings.ProfileModelMapping[profile] = _settings.Model;

            _settings.Save();
            SaveRequested?.Invoke(_settings);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _settings = new ExtensionSettings();
            ApiKeyBox.Password = _settings.KiloApiKey;
            BackendUrlBox.Text = _settings.BackendUrl;
            ProviderComboBox.SelectedIndex = 0;
            ModelComboBox.SelectedIndex = 0;
            ProfileComboBox.SelectedIndex = 0;
            AutoApproveCheckBox.IsChecked = false;
            AutoScrollCheckBox.IsChecked = true;
            TelemetryConsentCheckBox.IsChecked = false;
            SessionShareCheckBox.IsChecked = true;
            SoundCheckBox.IsChecked = false;
            NotificationCheckBox.IsChecked = true;
            IncludeFileCheckBox.IsChecked = true;
            IncludeSelectionCheckBox.IsChecked = true;
            IncludeTerminalCheckBox.IsChecked = false;
            SemanticSearchCheckBox.IsChecked = false;

            _settings.EnableTelemetry = false;
            _settings.EnableSessionSharing = true;
            _settings.ProfileProviderMapping.Clear();
            _settings.ProfileModelMapping.Clear();
        }

        public event System.Action<ExtensionSettings>? SaveRequested;

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings == null) return;

            var profile = ((ComboBoxItem?)ProfileComboBox.SelectedItem)?.Content?.ToString() ?? "Default";
            _settings.Profile = profile;
            ApplyProfileProviderModel();
        }
    }
}