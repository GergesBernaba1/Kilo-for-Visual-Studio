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
            ProfileComboBox.SelectedIndex = 0;
            ModelComboBox.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.KiloApiKey = ApiKeyBox.Password;
            _settings.BackendUrl = BackendUrlBox.Text;
            _settings.Save();
            SaveRequested?.Invoke(_settings);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _settings = new ExtensionSettings();
            ApiKeyBox.Password = _settings.KiloApiKey;
            BackendUrlBox.Text = _settings.BackendUrl;
            ProfileComboBox.SelectedIndex = 0;
            ModelComboBox.SelectedIndex = 0;
            AutoApproveCheckBox.IsChecked = false;
            AutoScrollCheckBox.IsChecked = true;
            SoundCheckBox.IsChecked = false;
            NotificationCheckBox.IsChecked = true;
            IncludeFileCheckBox.IsChecked = true;
            IncludeSelectionCheckBox.IsChecked = true;
            IncludeTerminalCheckBox.IsChecked = false;
            SemanticSearchCheckBox.IsChecked = false;
        }

        public event System.Action<ExtensionSettings>? SaveRequested;
    }
}