using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Extension.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloChatDocumentControl : UserControl
    {
        private AssistantService? _assistantService;
        private ExtensionSettings? _settings;
        private string _languageId = "unknown";
        private string _activeFilePath = string.Empty;
        private string _currentSessionId = string.Empty;
        private CancellationTokenSource? _activeCts;
        private readonly string[] _modes = { "Default", "Architect", "Coder", "Debugger" };
        private int _currentModeIndex;

        public KiloChatDocumentControl()
        {
            InitializeComponent();
        }

        public void Initialize(AssistantService assistantService, ExtensionSettings settings)
        {
            _assistantService = assistantService;
            _settings = settings;
            _currentModeIndex = Array.IndexOf(_modes, settings.Profile ?? "Default");
            if (_currentModeIndex < 0) _currentModeIndex = 0;
            UpdateModeLabel();
        }

        public void SetContext(string activeFilePath, string selectedText, string languageId)
        {
            _activeFilePath = activeFilePath ?? string.Empty;
            _languageId = string.IsNullOrWhiteSpace(languageId) ? "unknown" : languageId;
        }

        public void SetPrompt(string prompt)
        {
            PromptTextBox.Text = prompt ?? string.Empty;
        }

        private void UpdateModeLabel()
        {
            ModeLabel.Text = $"{_modes[_currentModeIndex]} Mode";
        }

        private void AddMessage(string role, string content)
        {
            var border = new Border
            {
                Background = role == "user" ? new SolidColorBrush(Color.FromRgb(45, 45, 48)) : new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var textBlock = new TextBlock
            {
                Text = content,
                Foreground = role == "user" ? new SolidColorBrush(Color.FromRgb(238, 238, 238)) : new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };

            border.Child = textBlock;
            ChatPanel.Children.Add(border);
            ChatScrollViewer.ScrollToEnd();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ChatPanel.Children.Clear();
            WelcomeMessage.Visibility = Visibility.Visible;
        }

        private async void PromptTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async System.Threading.Tasks.Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
                return;

            if (_assistantService == null || _settings == null)
            {
                AddMessage("system", "Error: Assistant service not initialized.");
                return;
            }

            var prompt = PromptTextBox.Text;
            AddMessage("user", prompt);
            WelcomeMessage.Visibility = Visibility.Collapsed;
            PromptTextBox.Clear();
            StatusText.Text = "Kilo is thinking...";
            SendButton.IsEnabled = false;

            _activeCts?.Cancel();
            _activeCts = new CancellationTokenSource();

            try
            {
                var provider = _settings?.Provider ?? "OpenAI";
                var model = _settings?.Model ?? "gpt-4o";
                if (_settings?.Profile == "Reviewer")
                {
                    provider = "Anthropic";
                    model = "claude-3-5-reasonable";
                }

                var request = new AssistantRequest
                {
                    ActiveFilePath = _activeFilePath,
                    ActiveFileContent = KiloPackage.GetActiveFileContent(),
                    LanguageId = _languageId,
                    SelectedText = string.Empty,
                    Prompt = prompt,
                    SessionId = _currentSessionId,
                    ProviderId = provider,
                    ModelId = $"{provider}:{model}"
                };

                var result = await _assistantService.AskAssistantAsync(request, _activeCts.Token);

                if (!result.IsSuccess)
                {
                    AddMessage("assistant", $"Error: {result.Error ?? result.Message}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(result.Message))
                    {
                        AddMessage("assistant", result.Message);
                    }

                    if (result.UsageCostUsd > 0)
                    {
                        StatusText.Text = $"Done (Provider={result.ProviderId ?? "unknown"}, Model={result.ModelId ?? "unknown"}, Cost=${result.UsageCostUsd:F4}, Tokens={result.UsageTokens ?? 0})";
                    }
                    else
                    {
                        StatusText.Text = $"Done (Provider={result.ProviderId ?? "unknown"}, Model={result.ModelId ?? "unknown"})";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddMessage("assistant", "[Cancelled]");
            }
            catch (Exception ex)
            {
                AddMessage("assistant", $"Exception: {ex.Message}");
            }
            finally
            {
                StatusText.Text = string.Empty;
                SendButton.IsEnabled = true;
            }
        }

        public void CycleMode()
        {
            _currentModeIndex = (_currentModeIndex + 1) % _modes.Length;
            if (_settings != null)
            {
                _settings.Profile = _modes[_currentModeIndex];
            }
            UpdateModeLabel();
        }

        public void NewSession()
        {
            _currentSessionId = string.Empty;
            SessionLabel.Text = "New session";
            ChatPanel.Children.Clear();
            WelcomeMessage.Visibility = Visibility.Visible;
        }
    }
}