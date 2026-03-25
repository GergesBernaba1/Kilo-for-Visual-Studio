using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Extension.Models;
using Microsoft.VisualStudio.Shell;

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
        public bool IsSelected { get; set; }
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

        private TelemetryService? _telemetryService;
        private PerformanceService? _performanceService;
        private CollaborationService? _collaborationService;
        private RoslynAnalyzerService? _roslynAnalyzerService;
        private SkillsSystemService? _skillsSystemService;

        private readonly ObservableCollection<SessionViewModel> _sessions = new ObservableCollection<SessionViewModel>();
        private readonly ObservableCollection<string> _conversationHistory = new ObservableCollection<string>();
        private readonly ObservableCollection<ToolCardViewModel> _toolCards = new ObservableCollection<ToolCardViewModel>();
        private readonly ObservableCollection<FileDiffViewModel> _fileDiffs = new ObservableCollection<FileDiffViewModel>();
        private readonly ObservableCollection<UsageHistoryItem> _usageHistory = new ObservableCollection<UsageHistoryItem>();

        public sealed class UsageHistoryItem
        {
            public DateTime Timestamp { get; set; }
            public string Provider { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public double Cost { get; set; }
            public int? Tokens { get; set; }

            public override string ToString() => $"{Timestamp:HH:mm:ss} | {Provider}/{Model} | Cost=${Cost:F4} | Tokens={Tokens ?? 0}";
        }

        public KiloAssistantToolWindowControl()
        {
            InitializeComponent();
            SessionComboBox.ItemsSource = _sessions;
            ConversationHistoryListBox.ItemsSource = _conversationHistory;
            UsageHistoryListBox.ItemsSource = _usageHistory;
            ToolCardsList.ItemsSource = _toolCards;
            DiffFilesList.ItemsSource = _fileDiffs;
            UpdateConnectionState(KiloConnectionState.Disconnected);
        }

        // ── Initialization ───────────────────────────────────────────────────────

        private McpHubService? _mcpHubService;

        public void Initialize(AssistantService assistantService, ExtensionSettings settings, McpHubService mcpHubService, string workspaceRoot,
            TelemetryService telemetryService, PerformanceService performanceService,
            CollaborationService collaborationService, RoslynAnalyzerService roslynAnalyzerService,
            SkillsSystemService skillsSystemService)
        {
            _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _mcpHubService = mcpHubService ?? throw new ArgumentNullException(nameof(mcpHubService));
            _telemetryService = telemetryService;
            _performanceService = performanceService;
            _collaborationService = collaborationService;
            _roslynAnalyzerService = roslynAnalyzerService;
            _skillsSystemService = skillsSystemService;

            if (_telemetryService != null)
            {
                _telemetryService.SetUserConsent(_settings.EnableTelemetry);
                _telemetryService.SetTelemetryEnabled(_settings.EnableTelemetry);
            }

            _workspaceRoot = workspaceRoot ?? Environment.CurrentDirectory;
            LoadSessionHistory();

            var credential = !string.IsNullOrEmpty(settings.BackendPassword)
                ? settings.BackendPassword : settings.KiloApiKey;
            ApiKeyText.Text = string.IsNullOrEmpty(credential) ? "(not set)" : "••••••••";
            BackendUrlText.Text = settings.BackendUrl;
            MockModeToggle.IsChecked = settings.UseMockBackend;

            // Initialize mode display
            var agentModeService = KiloPackage.AgentModeServiceInstance;
            if (agentModeService != null)
            {
                UpdateModeDisplay(agentModeService.CurrentModeDefinition);
                agentModeService.ModeChanged += OnAgentModeChanged;
            }

            if (_skillsSystemService != null)
            {
                SkillsListBox.ItemsSource = _skillsSystemService.Skills;
            }

            if (_mcpHubService != null)
            {
                _mcpHubService.ServersChanged += OnMcpServersChanged;
                _mcpHubService.HealthLogUpdated += OnMcpHealthLogUpdated;
                _mcpHubService.PoorHealthDetected += OnMcpPoorHealthDetected;
                _ = LoadMcpServersAsync();
            }

            // Wire live streaming events.
            _assistantService.TextDeltaReceived += OnTextDelta;
            _assistantService.ToolExecutionChanged += OnToolExecutionChanged;
            _assistantService.DiffUpdated += OnDiffUpdated;
        }

        private void OnAgentModeChanged(object? sender, AgentMode mode)
        {
            Dispatcher.Invoke(() =>
            {
                var agentModeService = KiloPackage.AgentModeServiceInstance;
                if (agentModeService != null)
                {
                    UpdateModeDisplay(agentModeService.CurrentModeDefinition);
                    SetStatus($"Mode switched to: {agentModeService.CurrentModeDefinition.Name}");

                    if (_settings != null)
                    {
                        _settings.Profile = agentModeService.CurrentModeDefinition.Name;
                        ApplyProfileProviderModel();
                        _settings.Save();
                    }
                }
            }, DispatcherPriority.Normal);
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
        }

        private void OnMcpServersChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => { _ = LoadMcpServersAsync(); }, DispatcherPriority.Normal);
        }

        private void OnMcpHealthLogUpdated(object? sender, McpHealthLogEntry logEntry)
        {
            Dispatcher.Invoke(() =>
            {
                if (McpHealthLogListBox != null)
                {
                    McpHealthLogListBox.Items.Insert(0, logEntry.ToString());
                }
            }, DispatcherPriority.Normal);
        }

        private void OnMcpPoorHealthDetected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatus("Poor MCP health detected; switching to Optimizer mode.");
                KiloPackage.AgentModeServiceInstance?.SetMode(AgentMode.Optimizer);
            }, DispatcherPriority.Normal);
        }

        private string _workspaceRoot = string.Empty;
        private string _sessionHistoryPath => Path.Combine(_workspaceRoot, ".kilo", "session_history.json");

        private void LoadSessionHistory()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_workspaceRoot))
                    return;

                if (!File.Exists(_sessionHistoryPath))
                    return;

                var json = File.ReadAllText(_sessionHistoryPath);
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                _conversationHistory.Clear();
                foreach (var item in list)
                    _conversationHistory.Insert(0, item);
            }
            catch { }
        }

        private void SaveSessionHistory()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_workspaceRoot))
                    return;

                var dir = Path.GetDirectoryName(_sessionHistoryPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_sessionHistoryPath, JsonSerializer.Serialize(_conversationHistory.ToList(), new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void AppendConversationHistory(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return;

            _conversationHistory.Insert(0, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {entry}");
            if (_conversationHistory.Count > 100)
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);

            SaveSessionHistory();
        }

        private void McpSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _ = LoadMcpServersAsync();
        }

        private async void McpRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mcpHubService == null)
                return;

            var remoteUrl = McpInventoryUrlBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                var loaded = await _mcpHubService.QueryRemoteCommunityServersAsync(remoteUrl);
                if (loaded.Any())
                {
                    SetStatus($"Loaded {loaded.Count} community MCP servers.");
                }
                else
                {
                    SetStatus("No servers loaded from remote inventory.");
                }
            }

            await LoadMcpServersAsync();
        }

        private void McpAutoModeButton_Click(object sender, RoutedEventArgs e)
        {
            KiloPackage.AgentModeServiceInstance?.SetMode(AgentMode.Optimizer);
        }

        private async Task LoadMcpServersAsync()
        {
            if (_mcpHubService == null)
                return;

            var available = await _mcpHubService.GetAvailableServersAsync();
            var known = _mcpHubService.Servers;

            var merged = known
                .Union(available, new McpServerConfigComparer())
                .ToList();

            var searchText = McpSearchBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var query = searchText;
                merged = merged.Where(s => s.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            McpServersListBox.ItemsSource = merged;

            if (McpHealthLogListBox != null)
            {
                McpHealthLogListBox.ItemsSource = _mcpHubService.HealthLog.Select(h => h.ToString()).ToList();
            }
        }

        private async void McpToggleServer_Click(object sender, RoutedEventArgs e)
        {
            if (_mcpHubService == null || McpServersListBox.SelectedItem is not McpServerConfig selected)
                return;

            _mcpHubService.EnableServer(selected.Id, !selected.Enabled);
            await LoadMcpServersAsync();
        }

        private async void McpRestartServer_Click(object sender, RoutedEventArgs e)
        {
            if (_mcpHubService == null || McpServersListBox.SelectedItem is not McpServerConfig selected)
                return;

            await _mcpHubService.RestartServerAsync(selected.Id);
            await LoadMcpServersAsync();
        }

        private async void McpInstallServer_Click(object sender, RoutedEventArgs e)
        {
            if (_mcpHubService == null || McpServersListBox.SelectedItem is not McpServerConfig selected)
                return;

            _mcpHubService.AddServer(new McpServerConfig
            {
                Id = selected.Id,
                Name = selected.Name,
                Description = selected.Description,
                Command = selected.Command,
                Args = selected.Args.ToList(),
                Enabled = true,
                Status = "Running",
                Score = selected.Score,
                Tags = selected.Tags.ToList(),
                DocumentationUrl = selected.DocumentationUrl
            });

            await LoadMcpServersAsync();
        }

        private void McpServersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (McpServersListBox.SelectedItem is McpServerConfig selected)
            {
                McpServerScoreText.Text = $"Score: {selected.Score:0.##}";
                McpServerTagsText.Text = selected.Tags.Any() ? $"Tags: {string.Join(", ", selected.Tags)}" : "Tags: n/a";
                McpServerDocsText.Text = string.IsNullOrWhiteSpace(selected.DocumentationUrl) ? "Docs: n/a" : $"Docs: {selected.DocumentationUrl}";
            }
        }

        private void McpDiffPatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (McpServersListBox.SelectedItem is McpServerConfig selected)
            {
                var url = selected.DocumentationUrl;
                if (!string.IsNullOrWhiteSpace(url))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                else
                    SetStatus("No documentation URL available for selected MCP server.");
            }
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
                        AppendConversationHistory("Turn completed");
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
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                AppendResponse(delta);
                if (!string.IsNullOrWhiteSpace(delta))
                    AppendConversationHistory($"Delta: {delta.Trim().Replace("\r\n", " ").Replace("\n", " ")}");
            }));
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
                    IsSelected = false,
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

            UpdateDiffActionButtons();
        }

        private void DiffItemSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateDiffActionButtons();
        }

        private void UpdateDiffActionButtons()
        {
            var hasSelected = _fileDiffs.Any(f => f.IsSelected);
            ApplySelectedDiffsButton.IsEnabled = hasSelected;
            PreviewSelectedDiffsButton.IsEnabled = hasSelected;
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

            _telemetryService?.LogFeatureUsageAsync("assistant_query");

            var perfScope = _performanceService?.StartMeasure("assistant_query");
            SetBusy(true, "Sending to Kilo…");

            _activeCts?.Cancel();
            _activeCts = new CancellationTokenSource();

            try
            {
                var provider = _settings?.Provider ?? "OpenAI";
                var model = _settings?.Model ?? "gpt-4o";
                if (_settings?.Profile == "Reviewer")
                {
                    // Reviewer mode favors a review-optimized model
                    provider = "Anthropic";
                    model = "claude-3-5-reasonable";
                }

                var request = new AssistantRequest
                {
                    ActiveFilePath = _activeFilePath,
                    LanguageId = _languageId,
                    SelectedText = SelectedTextArea.Text,
                    Prompt = PromptTextBox.Text,
                    SessionId = _currentSessionId,
                    ProviderId = provider,
                    ModelId = $"{provider}:{model}"
                };

                var result = await _assistantService.AskAssistantAsync(request, _activeCts.Token);

                if (!result.IsSuccess)
                {
                    ResponseTextBox.AppendText($"\n⚠ {result.Error ?? result.Message}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(result.SuggestedCode))
                    {
                        SuggestedCodeText.Text = result.SuggestedCode;
                        SuggestedCodeExpander.IsExpanded = true;
                    }

                    var providerId = result.ProviderId ?? _settings?.Provider ?? "unknown";
                    var modelId = result.ModelId ?? _settings?.Model ?? "unknown";
                    var cost = result.UsageCostUsd;
                    var tokens = result.UsageTokens ?? 0;

                    if (cost > 0)
                    {
                        SetStatus($"Done (Provider={providerId}, Model={modelId}, Cost=${cost:F4}, Tokens={tokens})");
                    }
                    else
                    {
                        SetStatus($"Done (Provider={providerId}, Model={modelId})");
                    }

                    _usageHistory.Insert(0, new UsageHistoryItem
                    {
                        Timestamp = DateTime.Now,
                        Provider = providerId,
                        Model = modelId,
                        Cost = cost,
                        Tokens = result.UsageTokens
                    });
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
                perfScope?.Dispose();

                try
                {
                    var memory = GC.GetTotalMemory(false);
                    _performanceService?.RecordMemory(memory);

                    ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
                    ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                    _performanceService?.RecordThreadPoolUsage(maxWorkerThreads - workerThreads, workerThreads);
                }
                catch { }

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

        private void RunSkillButton_Click(object sender, RoutedEventArgs e)
        {
            if (SkillsListBox.SelectedItem is Kilo.VisualStudio.App.Services.SkillDefinition skill)
            {
                var inputText = string.IsNullOrWhiteSpace(SelectedTextArea.Text) ? "" : SelectedTextArea.Text;
                PromptTextBox.Text = skill.PromptTemplate.Replace("{input}", inputText);
                SetStatus($"Skill '{skill.Name}' loaded into prompt.");

                _telemetryService?.LogEventAsync("skill_run", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "skill", skill.Name }
                });
            }
            else
            {
                SetStatus("Select a skill first.");
            }
        }

        private async void ShareSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_collaborationService == null)
            {
                SetStatus("Collaboration not initialized.");
                return;
            }

            if (!_settings?.EnableSessionSharing ?? true)
            {
                SetStatus("Session sharing is disabled in settings.");
                return;
            }

            var link = _collaborationService.ShareSession(_currentSessionId, _conversationHistory);
            Clipboard.SetText(link);
            SetStatus("Session share link copied to clipboard.");

            _telemetryService?.LogEventAsync("share_session", new System.Collections.Generic.Dictionary<string, string>
            {
                { "shareLink", link }
            });
        }

        private async void AnalyzeFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_activeFilePath) || !File.Exists(_activeFilePath))
            {
                SetStatus("No active file selected for analysis.");
                return;
            }

            if (_roslynAnalyzerService == null)
            {
                SetStatus("Analyzer service currently unavailable.");
                return;
            }

            SetBusy(true, "Analyzing active file...");
            try
            {
                var report = await _roslynAnalyzerService.AnalyzeFileAsync(_activeFilePath);
                MessageBox.Show(report, "Roslyn Analyzer Report", MessageBoxButton.OK, MessageBoxImage.Information);
                _telemetryService?.LogFeatureUsageAsync("roslyn_analyze");
            }
            catch (Exception ex)
            {
                SetStatus($"Analysis failed: {ex.Message}");
                _telemetryService?.LogErrorAsync("RoslynAnalyze", ex.Message, ex.StackTrace);
            }
            finally
            {
                SetBusy(false, string.Empty);
            }
        }

        private void RefreshPerfButton_Click(object sender, RoutedEventArgs e)
        {
            if (_performanceService == null)
            {
                PerformanceStatsText.Text = "Perf service unavailable.";
                return;
            }

            PerformanceStatsText.Text = _performanceService.GetPerformanceReport();
            _telemetryService?.LogFeatureUsageAsync("perf_refresh");
        }

        private void ApplyAllDiffsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fileDiffs.Count == 0) return;
            ApplyDiffsRequested?.Invoke(this, _fileDiffs.ToList());
        }

        private void ApplySelectedDiffsButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _fileDiffs.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0) return;
            ApplyDiffsRequested?.Invoke(this, selected);
        }

        private void PreviewSelectedDiffsButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _fileDiffs.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0)
            {
                SetStatus("No selected files to preview.");
                return;
            }

            var report = string.Join("\n\n", selected.Select(f => $"{f.FilePath}: +{f.Additions} -{f.Deletions}\n{f.DiffText}"));
            MessageBox.Show(report, "Selected Diff Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyFileDiffButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FileDiffViewModel fileDiff)
            {
                ApplyDiffsRequested?.Invoke(this, new[] { fileDiff });
            }
        }

        private void OpenFileFromDiff_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FileDiffViewModel fileDiff)
            {
                OpenFileRequested?.Invoke(this, fileDiff.FilePath);
            }
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
        public event EventHandler<string>? OpenFileRequested;
        public event EventHandler<string>? RevertRequested;

        // ── Public methods for external callers ───────────────────────────────────

        public void CycleAgentMode()
        {
            var agentModeService = KiloPackage.AgentModeServiceInstance;
            if (agentModeService != null)
            {
                agentModeService.CycleMode();
                var currentModeDef = agentModeService.CurrentModeDefinition;
                if (_settings != null)
                {
                    _settings.Profile = currentModeDef.Name;
                }
                UpdateModeDisplay(currentModeDef);
                SetStatus($"Mode changed to: {currentModeDef.Name} - {currentModeDef.Description}");
            }
            else
            {
                // Fallback to old behavior
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
        }

        private void UpdateModeDisplay(AgentModeDefinition modeDef)
        {
            if (ModeIcon != null && ModeLabel != null && ModeToolsText != null)
            {
                ModeIcon.Text = modeDef.Icon;
                ModeLabel.Text = modeDef.Name;
                ModeIcon.ToolTip = modeDef.Description;
                ModeLabel.ToolTip = modeDef.Description;

                // Update mode tools display
                var toolsText = modeDef.AllowedTools.Length > 0
                    ? $"Allowed tools: {string.Join(", ", modeDef.AllowedTools)}"
                    : "All tools available";
                ModeToolsText.Text = toolsText;
            }
        }

        public void CreateNewSession()
        {
            NewSessionButton_Click(this, new RoutedEventArgs());
        }
    }
}
