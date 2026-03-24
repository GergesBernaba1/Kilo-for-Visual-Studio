using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Extension.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    // ── View-models for the live lists ────────────────────────────────────────────

    public sealed class SessionViewModel : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private KiloSessionStatus _status;

        public string SessionId { get; set; } = string.Empty;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(DisplayTitle)); }
        }

        public KiloSessionStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? $"Session {SessionId.Substring(0, Math.Min(8, SessionId.Length))}" : Title;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => DisplayTitle;
    }

    public sealed class ToolCardViewModel : INotifyPropertyChanged
    {
        private KiloToolExecutionStatus _status;
        private string _title = string.Empty;

        public string CallId { get; set; } = string.Empty;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public KiloToolExecutionStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusBrush)); }
        }

        public string StatusText => _status switch
        {
            KiloToolExecutionStatus.Pending => "Pending…",
            KiloToolExecutionStatus.Running => "Running…",
            KiloToolExecutionStatus.Completed => "Completed",
            KiloToolExecutionStatus.Failed => "Failed",
            KiloToolExecutionStatus.Cancelled => "Cancelled",
            _ => string.Empty
        };

        public Brush StatusBrush => _status switch
        {
            KiloToolExecutionStatus.Pending => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            KiloToolExecutionStatus.Running => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            KiloToolExecutionStatus.Completed => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            KiloToolExecutionStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class FileDiffViewModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public string Before { get; set; } = string.Empty;
        public string After { get; set; } = string.Empty;

        public string StatusIcon => Status?.ToLowerInvariant() switch
        {
            "added" => "+",
            "deleted" => "−",
            _ => "M"
        };

        public Brush StatusColour => Status?.ToLowerInvariant() switch
        {
            "added" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            "deleted" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(234, 179, 8))
        };

        public string AdditionsSummary => Additions > 0 ? $"+{Additions}" : string.Empty;
        public string DeletionsSummary => Deletions > 0 ? $"-{Deletions}" : string.Empty;

        public string DiffText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Before) && string.IsNullOrWhiteSpace(After))
                    return "(diff not available)";

                var lines = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(Before))
                {
                    foreach (var l in Before.Replace("\r\n", "\n").Split('\n'))
                        lines.Append('-').AppendLine(l);
                }
                if (!string.IsNullOrWhiteSpace(After))
                {
                    foreach (var l in After.Replace("\r\n", "\n").Split('\n'))
                        lines.Append('+').AppendLine(l);
                }
                return lines.ToString().TrimEnd();
            }
        }
    }

    // ── Control ───────────────────────────────────────────────────────────────────

    public partial class KiloAssistantToolWindowControl : UserControl
    {
        private AssistantService? _assistantService;
        private ExtensionSettings? _settings;
        private string _languageId = "unknown";
        private string _activeFilePath = string.Empty;
        private string _currentSessionId = string.Empty;
        private CancellationTokenSource? _activeCts;

        private readonly ObservableCollection<SessionViewModel> _sessions = new ObservableCollection<SessionViewModel>();
        private readonly ObservableCollection<ToolCardViewModel> _toolCards = new ObservableCollection<ToolCardViewModel>();
        private readonly ObservableCollection<FileDiffViewModel> _fileDiffs = new ObservableCollection<FileDiffViewModel>();

        public KiloAssistantToolWindowControl()
        {
            InitializeComponent();
            SessionComboBox.ItemsSource = _sessions;
            ToolCardsList.ItemsSource = _toolCards;
            DiffFilesList.ItemsSource = _fileDiffs;
            UpdateConnectionState(KiloConnectionState.Disconnected);
        }

        // ── Initialization ───────────────────────────────────────────────────────

        public void Initialize(AssistantService assistantService, ExtensionSettings settings)
        {
            _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var credential = !string.IsNullOrEmpty(settings.BackendPassword)
                ? settings.BackendPassword : settings.KiloApiKey;
            ApiKeyText.Text = string.IsNullOrEmpty(credential) ? "(not set)" : "••••••••";
            BackendUrlText.Text = settings.BackendUrl;
            MockModeToggle.IsChecked = settings.UseMockBackend;

            // Wire live streaming events.
            _assistantService.TextDeltaReceived += OnTextDelta;
            _assistantService.ToolExecutionChanged += OnToolExecutionChanged;
            _assistantService.DiffUpdated += OnDiffUpdated;
        }

        public void SetContext(string activeFilePath, string selectedText, string languageId)
        {
            _activeFilePath = activeFilePath ?? string.Empty;
            ActiveFilePathText.Text = string.IsNullOrWhiteSpace(activeFilePath) ? "(no active file)" : activeFilePath;
            _languageId = string.IsNullOrWhiteSpace(languageId) ? "unknown" : languageId;

            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                SelectedTextArea.Text = selectedText;
                SelectedTextArea.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedTextArea.Text = string.Empty;
                SelectedTextArea.Visibility = Visibility.Collapsed;
            }
        }

        public void SetPrompt(string prompt) =>
            PromptTextBox.Text = prompt ?? string.Empty;

        // ── Connection state ─────────────────────────────────────────────────────

        public void UpdateConnectionState(KiloConnectionState state)
        {
            Dispatcher.Invoke(() =>
            {
                switch (state)
                {
                    case KiloConnectionState.Connected:
                        ConnectionDot.Fill = (SolidColorBrush)Resources["ConnectedBrush"];
                        ConnectionLabel.Text = "Connected";
                        ConnectionLabel.Foreground = (SolidColorBrush)Resources["ConnectedBrush"];
                        AskButton.IsEnabled = true;
                        ReconnectButton.Visibility = Visibility.Collapsed;
                        SetStatus(string.Empty);
                        break;
                    case KiloConnectionState.Connecting:
                        ConnectionDot.Fill = (SolidColorBrush)Resources["ConnectingBrush"];
                        ConnectionLabel.Text = "Connecting…";
                        ConnectionLabel.Foreground = (SolidColorBrush)Resources["ConnectingBrush"];
                        AskButton.IsEnabled = false;
                        ReconnectButton.Visibility = Visibility.Collapsed;
                        SetStatus("Starting Kilo server…");
                        break;
                    case KiloConnectionState.Error:
                        ConnectionDot.Fill = (SolidColorBrush)Resources["ErrorBrush"];
                        ConnectionLabel.Text = "Error";
                        ConnectionLabel.Foreground = (SolidColorBrush)Resources["ErrorBrush"];
                        AskButton.IsEnabled = false;
                        ReconnectButton.Visibility = Visibility.Visible;
                        break;
                    default:
                        ConnectionDot.Fill = (SolidColorBrush)Resources["DisconnectedBrush"];
                        ConnectionLabel.Text = "Disconnected";
                        ConnectionLabel.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                        AskButton.IsEnabled = false;
                        ReconnectButton.Visibility = Visibility.Visible;
                        break;
                }
            }, DispatcherPriority.Normal);
        }

        // ── Session event handler (called from KiloPackage) ──────────────────────

        public void HandleSessionEvent(KiloSessionEvent e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                switch (e.Kind)
                {
                    case KiloSessionEventKind.ConnectionStateChanged:
                        UpdateConnectionState(e.ConnectionState);
                        break;
                    case KiloSessionEventKind.SessionCreated:
                        if (e.Session != null) AddOrUpdateSession(e.Session);
                        break;
                    case KiloSessionEventKind.SessionUpdated:
                        if (e.Session != null) AddOrUpdateSession(e.Session);
                        break;
                    case KiloSessionEventKind.SessionDeleted:
                        RemoveSession(e.SessionId);
                        break;
                    case KiloSessionEventKind.TurnStarted:
                        SetBusy(true, "Kilo is thinking…");
                        break;
                    case KiloSessionEventKind.TurnCompleted:
                        SetBusy(false, "Done.");
                        ShowDiffSectionIfNeeded();
                        break;
                    case KiloSessionEventKind.Error:
                        SetBusy(false, $"Error: {e.Error}");
                        AppendResponse($"\n⚠ {e.Error}");
                        break;
                    case KiloSessionEventKind.TextDelta:
                        if (!string.IsNullOrEmpty(e.Delta) && e.SessionId == _currentSessionId)
                            AppendResponse(e.Delta);
                        break;
                    case KiloSessionEventKind.ToolExecutionUpdated:
                        if (e.ToolExecution != null)
                            UpsertToolCard(e.ToolExecution);
                        break;
                    case KiloSessionEventKind.DiffUpdated:
                        if (e.FileDiffs != null && e.FileDiffs.Count > 0)
                            RefreshDiffList(e.FileDiffs);
                        break;
                }
            }));
        }

        // ── Streaming callbacks (wired from Initialize) ──────────────────────────

        private void OnTextDelta(object? sender, string delta)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => AppendResponse(delta)));
        }

        private void OnToolExecutionChanged(object? sender, KiloToolExecution tool)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => UpsertToolCard(tool)));
        }

        private void OnDiffUpdated(object? sender, IReadOnlyList<KiloFileDiff> diffs)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => RefreshDiffList(diffs)));
        }

        // ── Session history helpers ──────────────────────────────────────────────

        private void AddOrUpdateSession(KiloSessionSummary session)
        {
            var existing = _sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
            if (existing != null)
            {
                existing.Title = session.Title;
                existing.Status = session.Status;
            }
            else
            {
                var vm = new SessionViewModel
                {
                    SessionId = session.SessionId,
                    Title = session.Title,
                    Status = session.Status
                };
                _sessions.Insert(0, vm);
                if (SessionComboBox.SelectedItem == null)
                    SessionComboBox.SelectedItem = vm;
            }
        }

        private void RemoveSession(string sessionId)
        {
            var vm = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (vm != null) _sessions.Remove(vm);
        }

        // ── Tool card helpers ────────────────────────────────────────────────────

        private void UpsertToolCard(KiloToolExecution tool)
        {
            var existing = _toolCards.FirstOrDefault(c => c.CallId == tool.CallId);
            if (existing != null)
            {
                existing.Title = string.IsNullOrWhiteSpace(tool.Title) ? tool.ToolName : tool.Title;
                existing.Status = tool.Status;
            }
            else
            {
                _toolCards.Add(new ToolCardViewModel
                {
                    CallId = tool.CallId,
                    Title = string.IsNullOrWhiteSpace(tool.Title) ? tool.ToolName : tool.Title,
                    Status = tool.Status
                });
            }

            ToolCardsPanel.Visibility = _toolCards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Show suggested code if present.
            if (!string.IsNullOrWhiteSpace(tool.SuggestedCode))
            {
                SuggestedCodeText.Text = tool.SuggestedCode;
                SuggestedCodeExpander.IsExpanded = true;
            }
        }

        // ── Diff helpers ─────────────────────────────────────────────────────────

        private void RefreshDiffList(IReadOnlyList<KiloFileDiff> diffs)
        {
            _fileDiffs.Clear();
            foreach (var d in diffs)
            {
                _fileDiffs.Add(new FileDiffViewModel
                {
                    FilePath = d.FilePath,
                    Status = d.Status,
                    Additions = d.Additions,
                    Deletions = d.Deletions,
                    Before = d.Before,
                    After = d.After
                });
            }

            ShowDiffSectionIfNeeded();
            ApplyAllDiffsButton.IsEnabled = _fileDiffs.Count > 0;
            RevertChangesButton.IsEnabled = _fileDiffs.Count > 0;
        }

        private void ShowDiffSectionIfNeeded()
        {
            if (_fileDiffs.Count > 0)
                DiffExpander.IsExpanded = true;
        }

        // ── UI state helpers ─────────────────────────────────────────────────────

        private void AppendResponse(string text)
        {
            ResponseTextBox.AppendText(text);
            ResponseTextBox.ScrollToEnd();
        }

        private void SetBusy(bool busy, string statusText)
        {
            AskButton.IsEnabled = !busy;
            AbortButton.IsEnabled = busy;
            AbortButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            SetStatus(statusText);
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text ?? string.Empty;
        }

        // ── Button handlers ──────────────────────────────────────────────────────

        private async void AskButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                ResponseTextBox.Text = "Please enter a prompt.";
                return;
            }

            if (_assistantService == null)
            {
                ResponseTextBox.Text = "Error: Assistant service not initialized.";
                return;
            }

            // Reset state.
            ResponseTextBox.Clear();
            _toolCards.Clear();
            ToolCardsPanel.Visibility = Visibility.Collapsed;
            SuggestedCodeExpander.IsExpanded = false;
            DiffExpander.IsExpanded = false;
            _fileDiffs.Clear();
            ApplyAllDiffsButton.IsEnabled = false;
            RevertChangesButton.IsEnabled = false;

            SetBusy(true, "Sending to Kilo…");

            _activeCts?.Cancel();
            _activeCts = new CancellationTokenSource();

            try
            {
                var request = new AssistantRequest
                {
                    ActiveFilePath = _activeFilePath,
                    LanguageId = _languageId,
                    SelectedText = SelectedTextArea.Text,
                    Prompt = PromptTextBox.Text,
                    SessionId = _currentSessionId
                };

                var result = await _assistantService.AskAssistantAsync(request, _activeCts.Token);

                if (!result.IsSuccess)
                {
                    ResponseTextBox.AppendText($"\n⚠ {result.Error ?? result.Message}");
                }
                else if (!string.IsNullOrWhiteSpace(result.SuggestedCode))
                {
                    SuggestedCodeText.Text = result.SuggestedCode;
                    SuggestedCodeExpander.IsExpanded = true;
                }
            }
            catch (OperationCanceledException)
            {
                AppendResponse("\n[Cancelled]");
            }
            catch (Exception ex)
            {
                AppendResponse($"\nException: {ex.Message}");
            }
            finally
            {
                SetBusy(false, string.Empty);
            }
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            _activeCts?.Cancel();
            SetStatus("Aborting…");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PromptTextBox.Clear();
            ResponseTextBox.Clear();
            SuggestedCodeText.Text = string.Empty;
            SuggestedCodeExpander.IsExpanded = false;
            _toolCards.Clear();
            ToolCardsPanel.Visibility = Visibility.Collapsed;
            _fileDiffs.Clear();
            DiffExpander.IsExpanded = false;
            ApplyAllDiffsButton.IsEnabled = false;
            RevertChangesButton.IsEnabled = false;
            SetStatus(string.Empty);
        }

        private void ReconnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise a dedicated command handled by KiloPackage to re-trigger EnsureConnectedAsync.
            SetStatus("Reconnecting…");
            UpdateConnectionState(KiloConnectionState.Connecting);
            ReconnectRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void NewSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_assistantService == null) return;
            // Create a new session and activate it.
            try
            {
                _currentSessionId = string.Empty;
                ResponseTextBox.Clear();
                _toolCards.Clear();
                ToolCardsPanel.Visibility = Visibility.Collapsed;
                _fileDiffs.Clear();
                DiffExpander.IsExpanded = false;
                SetStatus("Creating new session…");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to create session: {ex.Message}");
            }
        }

        private async void DeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionComboBox.SelectedItem is not SessionViewModel vm) return;
            if (_assistantService == null) return;

            var result = MessageBox.Show(
                $"Delete session '{vm.DisplayTitle}'?",
                "Kilo Assistant",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                DeleteSessionRequested?.Invoke(this, vm.SessionId);
                _sessions.Remove(vm);
                if (vm.SessionId == _currentSessionId)
                    _currentSessionId = string.Empty;
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to delete session: {ex.Message}");
            }
        }

        private void SessionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionComboBox.SelectedItem is SessionViewModel vm)
            {
                _currentSessionId = vm.SessionId;
                ResponseTextBox.Clear();
                _toolCards.Clear();
                ToolCardsPanel.Visibility = Visibility.Collapsed;
                _fileDiffs.Clear();
                DiffExpander.IsExpanded = false;
                SessionSelected?.Invoke(this, vm.SessionId);
            }
        }

        private void ApplyAllDiffsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fileDiffs.Count == 0) return;
            ApplyDiffsRequested?.Invoke(this, _fileDiffs.ToList());
        }

        private void RevertChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSessionId)) return;
            RevertRequested?.Invoke(this, _currentSessionId);
        }

        private void MockModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Settings change is surfaced back to the package via event; actual reload
            // happens on next extension startup.
        }

        // ── Events raised for KiloPackage ────────────────────────────────────────

        public event EventHandler? ReconnectRequested;
        public event EventHandler<string>? SessionSelected;
        public event EventHandler<string>? DeleteSessionRequested;
        public event EventHandler<IReadOnlyList<FileDiffViewModel>>? ApplyDiffsRequested;
        public event EventHandler<string>? RevertRequested;

        // ── Public methods for external callers ───────────────────────────────────

        public void CycleAgentMode()
        {
            var modes = new[] { "Default", "Architect", "Coder", "Debugger" };
            var currentMode = _settings?.Profile ?? "Default";
            var currentIndex = Array.IndexOf(modes, currentMode);
            var newIndex = (currentIndex + 1) % modes.Length;
            if (_settings != null)
            {
                _settings.Profile = modes[newIndex];
                SetStatus($"Mode changed to: {modes[newIndex]}");
            }
        }

        public void CreateNewSession()
        {
            NewSessionButton_Click(this, new RoutedEventArgs());
        }
    }
}
