using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.App.Services;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Extension.Logging;
using Kilo.VisualStudio.Extension.Models;
using Kilo.VisualStudio.Extension.Services;
using Kilo.VisualStudio.Extension.UI;
using Kilo.VisualStudio.Integration;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Task = System.Threading.Tasks.Task;

namespace Kilo.VisualStudio.Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(KiloAssistantToolWindowPane))]
    [ProvideToolWindow(typeof(KiloDiffViewerWindowPane))]
    [ProvideToolWindow(typeof(KiloSessionHistoryWindowPane))]
    [ProvideToolWindow(typeof(KiloSettingsWindowPane))]
    [ProvideToolWindow(typeof(KiloAutomationToolWindowPane))]
    [ProvideToolWindow(typeof(KiloAgentManagerWindowPane))]
    [ProvideToolWindow(typeof(KiloSubAgentViewerWindowPane))]
    [Guid(PackageGuids.PackageGuidString)]
    public sealed class KiloPackage : AsyncPackage
    {
        /// <summary>
        /// Custom UI context GUID for rule-based autoload (VS2022 17.10+ compatible).
        /// </summary>
        public const string KiloUIContextGuidString = "d2e8c4f1-9a3b-4e7d-b5c6-8f1a2d3e4c5b";
        private ExtensionSettings _extensionSettings = ExtensionSettings.Load();
        private readonly KiloLogger _logger = new KiloLogger();
        private static readonly object DiagnosticLogLock = new object();
        private static readonly string DiagnosticLogPath = Path.Combine(Path.GetTempPath(), "KiloVS", "kilo-extension.log");
        private static KiloPackage? _instance;
        private static AutocompleteService? _autocompleteServiceInstance;
        private static AutomationService? _automationServiceInstance;
        private static AgentModeService? _agentModeServiceInstance;
        private static readonly Guid OutputPaneGuid = new Guid("b3c8f1a2-7d4e-4f6b-8a9c-0d1e2f3a4b5c");
        private static readonly System.Net.Http.HttpClient SharedBackendHttpClient = new System.Net.Http.HttpClient();
        private IVsOutputWindowPane? _outputPane;
        public static TelemetryService? TelemetryServiceInstance { get; private set; }
        public static SkillsSystemService? SkillsSystemServiceInstance { get; private set; }
        public static PerformanceService? PerformanceServiceInstance { get; private set; }
        public static CollaborationService? CollaborationServiceInstance { get; private set; }
        public static RoslynAnalyzerService? RoslynAnalyzerServiceInstance { get; private set; }

        // Service layer – either mock or real depending on settings.
        private KiloConnectionService? _connectionService;
        private MockKiloSessionHostAdapter? _mockAdapter;
        private AssistantService? _assistantService;
        private AutocompleteService? _autocompleteService;
        private AutomationService? _automationService;
        private AgentModeService? _agentModeService;
        private McpHubService? _mcpHubService;
        private VSAutomationExecutor? _vsAutomationExecutor;

        public static KiloPackage? Instance => _instance;
        public static AutocompleteService? AutocompleteServiceInstance => _autocompleteServiceInstance;
        public static AutomationService? AutomationServiceInstance => _automationServiceInstance;
        public static AgentModeService? AgentModeServiceInstance => _agentModeServiceInstance;

        private static void LogExtensionEvent(string method, string message = "", Exception? exception = null)
        {
            try
            {
                var folder = Path.GetDirectoryName(DiagnosticLogPath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var timestamp = DateTimeOffset.Now.ToString("o");
                var threadId = Environment.CurrentManagedThreadId;
                var exceptionText = exception?.ToString() ?? "-";
                var line = $"{timestamp} | thread={threadId} | method={method} | message={message} | exception={exceptionText}{Environment.NewLine}";

                lock (DiagnosticLogLock)
                {
                    File.AppendAllText(DiagnosticLogPath, line);
                }

                // Also write to VS Output Window pane
                var shortLine = exception != null
                    ? $"[Kilo] {method}: {message} — {exception.Message}\n"
                    : $"[Kilo] {method}: {message}\n";
                _instance?._outputPane?.OutputStringThreadSafe(shortLine);
            }
            catch
            {
                // Logging must never break extension behavior.
            }
        }

        /// <summary>
        /// Gets the full content of the active document file from any context.
        /// </summary>
        public static string GetActiveFileContent()
        {
            return Instance?.GetActiveFileContentInternal() ?? string.Empty;
        }

        private string GetActiveFileContentInternal()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var filePath = GetActiveFilePath();
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    return System.IO.File.ReadAllText(filePath);
                }
            }
            catch { }
            return string.Empty;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            LogExtensionEvent(nameof(InitializeAsync), "start");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _logger.Info("Kilo Extension initializing…");

            // Get DTE service async (VSSDK analyzer friendly)
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;

            if (dte != null)
            {
                // Wire up debugging events for mode switching
                dte.Events.DebuggerEvents.OnEnterBreakMode += OnDebuggerEnterBreakMode;
                dte.Events.DebuggerEvents.OnEnterRunMode += OnDebuggerEnterRunMode;
                // dte.Events.DebuggerEvents.OnExceptionThrown += OnDebuggerExceptionThrown;
            }

            _instance = this;

            // Initialize VS Output Window pane for integrated logging
            try
            {
                if (await GetServiceAsync(typeof(SVsOutputWindow)) is IVsOutputWindow outputWindow)
                {
                    var paneGuid = OutputPaneGuid;
                    outputWindow.CreatePane(ref paneGuid, "Kilo", 1, 1);
                    outputWindow.GetPane(ref paneGuid, out _outputPane);
                    _outputPane?.OutputStringThreadSafe("Kilo Extension initializing…\n");
                }
            }
            catch
            {
                // Output pane is non-critical
            }

            var workspaceRoot = GetWorkspaceDirectory();
            TelemetryServiceInstance = new TelemetryService(workspaceRoot);
            SkillsSystemServiceInstance = new SkillsSystemService(workspaceRoot);
            PerformanceServiceInstance = new PerformanceService();
            CollaborationServiceInstance = new CollaborationService(workspaceRoot);
            RoslynAnalyzerServiceInstance = new RoslynAnalyzerService(workspaceRoot);

            if (_extensionSettings.EnableTelemetry)
            {
                TelemetryServiceInstance.SetUserConsent(true);
                TelemetryServiceInstance.SetTelemetryEnabled(true);
            }

            if (_extensionSettings.UseMockBackend)
            {
                // ── Mock path: no CLI process needed. ──────────────────────────
                _mockAdapter = new MockKiloSessionHostAdapter();
                _assistantService = new AssistantService(_mockAdapter, () => new KiloServerEndpoint(), _agentModeService);
                _mockAdapter.SessionEventReceived += OnSessionEvent;
                _autocompleteService = new AutocompleteService(GetWorkspaceDirectory());
                _automationService = new AutomationService(GetWorkspaceDirectory());
                _autocompleteServiceInstance = _autocompleteService;
                _automationServiceInstance = _automationService;
                _vsAutomationExecutor = new VSAutomationExecutor(dte, _automationService);
                _agentModeService = new AgentModeService(GetWorkspaceDirectory(), modeName => {
                    _extensionSettings.Profile = modeName;
                    _extensionSettings.Save();
                });
                _agentModeService.SetCurrentModeFromSettings(_extensionSettings.Profile);
                _mcpHubService = new McpHubService(GetWorkspaceDirectory());
                _agentModeServiceInstance = _agentModeService;
            }
            else
            {
                // ── Real path: spawn the Kilo CLI. ─────────────────────────────
                var extensionDir = GetExtensionDirectory();
                _connectionService = new KiloConnectionService(extensionDir);
                _connectionService.StateChanged += OnConnectionStateChanged;
                _connectionService.EventReceived += OnSessionEvent;

                // The AssistantService needs the adapter, but we can't access _connectionService.Adapter
                // before connecting (it throws). Use a lazy wrapper that resolves at request time.
                var realAdapter = new LazyConnectionAdapter(_connectionService);
                _assistantService = new AssistantService(realAdapter,
                    () => _connectionService.ServerInstance?.ToEndpoint() ?? new KiloServerEndpoint(), _agentModeService);

                // Use a shared HttpClient to avoid TCP port exhaustion
                var backendClient = new KiloBackendClient(SharedBackendHttpClient, false)
                {
                    ApiKey = _extensionSettings.KiloApiKey,
                    BackendUrl = _extensionSettings.BackendUrl
                };
                _autocompleteService = new AutocompleteService(GetWorkspaceDirectory(), backendClient);
                _automationService = new AutomationService(GetWorkspaceDirectory(), backendClient, _vsAutomationExecutor);
                _autocompleteServiceInstance = _autocompleteService;
                _automationServiceInstance = _automationService;
                _vsAutomationExecutor = new VSAutomationExecutor(dte, _automationService);
                _agentModeService = new AgentModeService(GetWorkspaceDirectory(), modeName => {
                    _extensionSettings.Profile = modeName;
                    _extensionSettings.Save();
                });
                _agentModeService.SetCurrentModeFromSettings(_extensionSettings.Profile);
                _mcpHubService = new McpHubService(GetWorkspaceDirectory());
                _agentModeServiceInstance = _agentModeService;

                // Start connection eagerly so the UI shows its state quickly.
                _ = _connectionService
                        .EnsureConnectedAsync(GetWorkspaceDirectory(), cancellationToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.Error("Kilo connection failed on startup.", t.Exception?.InnerException);
                                LogExtensionEvent(nameof(InitializeAsync), "connection startup failed", t.Exception?.InnerException);
                            }
                        }, TaskScheduler.Default);
            }

            await RegisterCommands();
            _logger.Info("Kilo Extension initialized.");
            LogExtensionEvent(nameof(InitializeAsync), "end");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_mockAdapter != null)
                    _mockAdapter.SessionEventReceived -= OnSessionEvent;
                if (_connectionService != null)
                {
                    _connectionService.StateChanged -= OnConnectionStateChanged;
                    _connectionService.EventReceived -= OnSessionEvent;
                    _connectionService.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        // ── Event handlers ──────────────────────────────────────────────────────────

        private void OnConnectionStateChanged(object? sender, KiloConnectionState state)
        {
            LogExtensionEvent(nameof(OnConnectionStateChanged), $"state={state}");
            _logger.Info($"Kilo connection state → {state}");
            BroadcastToToolWindow(control => control.UpdateConnectionState(state));

            // Update VS status bar with connection state
            var statusText = state switch
            {
                KiloConnectionState.Connected => "Kilo: Connected",
                KiloConnectionState.Connecting => "Kilo: Connecting…",
                KiloConnectionState.Disconnected => "Kilo: Disconnected",
                KiloConnectionState.Error => "Kilo: Connection Error",
                _ => "Kilo: Unknown"
            };
            _ = UpdateStatusBarAsync(statusText);

            if (TelemetryServiceInstance != null && _extensionSettings.EnableTelemetry)
            {
                _ = TelemetryServiceInstance.LogFeatureUsageAsync($"connection_{state.ToString().ToLowerInvariant()}");
            }
        }

        private void OnSessionEvent(object? sender, KiloSessionEvent e)
        {
            // Forward SSE events to the visible tool window.
            BroadcastToToolWindow(control => control.HandleSessionEvent(e));

            if (TelemetryServiceInstance != null && _extensionSettings.EnableTelemetry)
            {
                _ = TelemetryServiceInstance.LogEventAsync("session_event", new Dictionary<string, string>
                {
                    { "kind", e.Kind.ToString() },
                    { "sessionId", e.SessionId ?? string.Empty }
                });
            }
        }

        private void BroadcastToToolWindow(Action<KiloAssistantToolWindowControl> action)
        {
            // Use JoinableTaskFactory instead of Application.Current?.Dispatcher
            // which can be null in VS2022 extension contexts.
            _ = this.JoinableTaskFactory.RunAsync(async () =>
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
                if (window?.Content is KiloAssistantToolWindowControl control)
                {
                    action(control);
                }
            });
        }

        // ── Control event handlers ──────────────────────────────────────────────────

        private void WireControlEvents(KiloAssistantToolWindowControl control)
        {
            control.ReconnectRequested -= OnReconnectRequested;
            control.ReconnectRequested += OnReconnectRequested;
            control.ApplyDiffsRequested -= OnApplyDiffsRequested;
            control.ApplyDiffsRequested += OnApplyDiffsRequested;
            control.OpenFileRequested -= OnOpenFileRequested;
            control.OpenFileRequested += OnOpenFileRequested;
            control.DeleteSessionRequested -= OnDeleteSessionRequested;
            control.DeleteSessionRequested += OnDeleteSessionRequested;
        }

        private void OnReconnectRequested(object? sender, EventArgs e)
        {
            if (_connectionService == null) return;
            _ = _connectionService
                    .EnsureConnectedAsync(GetWorkspaceDirectorySafe())
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.Error("Kilo reconnect failed.", t.Exception?.InnerException);
                            LogExtensionEvent(nameof(OnReconnectRequested), "reconnect failed", t.Exception?.InnerException);
                        }
                    }, TaskScheduler.Default);
        }

        private void OnApplyDiffsRequested(object? sender, IReadOnlyList<FileDiffViewModel> fileDiffs)
        {
            if (fileDiffs == null || fileDiffs.Count == 0) return;

            var workspaceRoot = GetWorkspaceDirectorySafe();
            var kiloDiffs = new List<KiloFileDiff>();
            foreach (var vm in fileDiffs)
            {
                kiloDiffs.Add(new KiloFileDiff
                {
                    FilePath = vm.FilePath,
                    Before = vm.Before,
                    After = vm.After,
                    Additions = vm.Additions,
                    Deletions = vm.Deletions,
                    Status = vm.Status
                });
            }

            var host = new VsDiffEditorHost(this);
            var applyService = new DiffApplyService(host);
            var results = applyService.ApplyDiffs(kiloDiffs, workspaceRoot);

            var failures = new System.Text.StringBuilder();
            foreach (var r in results)
            {
                if (!r.IsSuccess && !r.IsSkipped)
                    failures.AppendLine($"• {r.FilePath}: {r.Message}");
            }

            if (failures.Length > 0)
            {
                VsShellUtilities.ShowMessageBox(this,
                    $"Some patches could not be applied:\n\n{failures}",
                    "Kilo — Patch Apply",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void OnDeleteSessionRequested(object? sender, string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || _assistantService == null || _connectionService == null) return;
            var workspaceRoot = GetWorkspaceDirectorySafe();
            _ = _connectionService.Adapter
                    .DeleteSessionAsync(sessionId, workspaceRoot, CancellationToken.None);
        }

        private void OnOpenFileRequested(object? sender, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                var host = new VsDiffEditorHost(this);
                host.OpenDocument(filePath);
            }
            catch (Exception ex)
            {
                _logger?.Warning("Unable to open requested file from diff view: " + ex.Message);
                LogExtensionEvent(nameof(OnOpenFileRequested), "open file failed", ex);
            }
        }

        private string GetWorkspaceDirectorySafe()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return GetWorkspaceDirectory();
            }
            catch
            {
                return Environment.CurrentDirectory;
            }
        }

        // ── Commands ────────────────────────────────────────────────────────────────

        private async Task RegisterCommands()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            LogExtensionEvent(nameof(RegisterCommands), "start");

            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                // Use the dedicated command set GUID (not the package GUID)
                var cmdSetGuid = new Guid(PackageGuids.CommandSetGuidString);

                // ── Main menu commands ──────────────────────────────────────
                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenToolWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenAssistantToolWindow)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskSelection(),
                    new CommandID(cmdSetGuid, PackageCommandSet.AskSelection)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskFile(),
                    new CommandID(cmdSetGuid, PackageCommandSet.AskFile)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenDiffViewerWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenDiffViewer)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenSessionHistoryWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenSessionHistory)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenSettingsWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenSettings)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = CycleAgentMode(),
                    new CommandID(cmdSetGuid, PackageCommandSet.CycleAgentMode)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = NewSession(),
                    new CommandID(cmdSetGuid, PackageCommandSet.NewSession)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenAutomationWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenAutomationToolWindow)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = ShowAgentManagerWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenAgentManager)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = ShowSubAgentViewer(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenSubAgentViewer)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => OpenExtensionLogFile(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenExtensionLog)));

                // ── Editor context menu commands ────────────────────────────
                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskSelection(),
                    new CommandID(cmdSetGuid, PackageCommandSet.AskSelectionContext)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskFile(),
                    new CommandID(cmdSetGuid, PackageCommandSet.AskFileContext)));

                // ── Solution Explorer context menu command ──────────────────
                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskFile(),
                    new CommandID(cmdSetGuid, PackageCommandSet.AskFileSolutionExplorer)));

                // ── Tools menu fallback command ─────────────────────────────
                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenToolWindow(),
                    new CommandID(cmdSetGuid, PackageCommandSet.OpenAssistantTools)));

                LogExtensionEvent(nameof(RegisterCommands), "success");
            }

            // ── Status bar ─────────────────────────────────────────────────
            await UpdateStatusBarAsync("Kilo: Ready");
        }

        private async Task UpdateStatusBarAsync(string text)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                if (await GetServiceAsync(typeof(SVsStatusbar)) is IVsStatusbar statusBar)
                {
                    statusBar.SetText(text);
                }
            }
            catch
            {
                // Status bar not critical
            }
        }

        private void OpenExtensionLogFile()
        {
            LogExtensionEvent(nameof(OpenExtensionLogFile), "entered");
            try
            {
                var folder = Path.GetDirectoryName(DiagnosticLogPath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (!File.Exists(DiagnosticLogPath))
                {
                    File.AppendAllText(DiagnosticLogPath, string.Empty);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = DiagnosticLogPath,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                LogExtensionEvent(nameof(OpenExtensionLogFile), "open log failed", ex);
                _logger.Warning("Failed to open Kilo extension log: " + ex.Message);
            }
        }

        private async Task OpenToolWindow()
        {
            LogExtensionEvent(nameof(OpenToolWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo tool window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());

            if (window is KiloAssistantToolWindowPane pane &&
                pane.Content is KiloAssistantToolWindowControl control)
            {
                if (_assistantService != null && _mcpHubService != null)
                    control.Initialize(
                        _assistantService,
                        _extensionSettings,
                        _mcpHubService,
                        GetWorkspaceDirectorySafe(),
                        TelemetryServiceInstance!,
                        PerformanceServiceInstance!,
                        CollaborationServiceInstance!,
                        RoslynAnalyzerServiceInstance!,
                        SkillsSystemServiceInstance!);

                WireControlEvents(control);

                var state = _connectionService?.State ?? KiloConnectionState.Disconnected;
                if (_extensionSettings.UseMockBackend)
                    state = KiloConnectionState.Connected;
                control.UpdateConnectionState(state);
                control.SetContext(GetActiveFilePath(), GetActiveSelection(), GetActiveLanguageId());
            }
        }

        private async Task AskSelection()
        {
            LogExtensionEvent(nameof(AskSelection), "entered");
            await OpenToolWindow();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                var selectedText = GetActiveSelection();
                var filePath = GetActiveFilePath();
                control.SetContext(filePath, selectedText, GetActiveLanguageId());
                control.SetPrompt(string.IsNullOrEmpty(selectedText)
                    ? "Please describe what you want to know about the current code."
                    : $"Explain or improve the selected code:\n{selectedText}");
            }
        }

        private async Task AskFile()
        {
            LogExtensionEvent(nameof(AskFile), "entered");
            await OpenToolWindow();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                var filePath = GetActiveFilePath();
                control.SetContext(filePath, GetActiveSelection(), GetActiveLanguageId());
                control.SetPrompt($"Analyze this file and explain what it does: {filePath}");
            }
        }

        private async Task OpenDiffViewerWindow()
        {
            LogExtensionEvent(nameof(OpenDiffViewerWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloDiffViewerWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Diff Viewer window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task OpenSessionHistoryWindow()
        {
            LogExtensionEvent(nameof(OpenSessionHistoryWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloSessionHistoryWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Session History window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task OpenSettingsWindow()
        {
            LogExtensionEvent(nameof(OpenSettingsWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloSettingsWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Settings window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task OpenAutomationWindow()
        {
            LogExtensionEvent(nameof(OpenAutomationWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloAutomationToolWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Automation window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task ShowAgentManagerWindow()
        {
            LogExtensionEvent(nameof(ShowAgentManagerWindow), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloAgentManagerWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Agent Manager window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task ShowSubAgentViewer()
        {
            LogExtensionEvent(nameof(ShowSubAgentViewer), "entered");
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloSubAgentViewerWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Sub-Agent Viewer window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task CycleAgentMode()
        {
            LogExtensionEvent(nameof(CycleAgentMode), "entered");
            await OpenToolWindow();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                control.CycleAgentMode();
            }
        }

        private async Task NewSession()
        {
            LogExtensionEvent(nameof(NewSession), "entered");
            await OpenToolWindow();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                control.CreateNewSession();
            }
        }

        // ── Debugging event handlers for mode switching ───────────────────────────

        private void OnDebuggerEnterBreakMode(EnvDTE.dbgEventReason reason, ref EnvDTE.dbgExecutionAction action)
        {
            // Auto-switch to Debugger mode when hitting a breakpoint
            _agentModeService?.AutoSwitchModeBasedOnContext("breakpoint");
        }

        private void OnDebuggerEnterRunMode(EnvDTE.dbgEventReason reason)
        {
            // Could potentially switch back to previous mode when resuming, but for now keep current mode
        }

        private void OnDebuggerExceptionThrown(string exceptionType, string name, int code, string description)
        {
            // Auto-switch to Debugger mode when an exception is thrown
            _agentModeService?.AutoSwitchModeBasedOnContext("exception");
        }

        // ── Context helpers ─────────────────────────────────────────────────────────

        private string GetWorkspaceDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (GetService(typeof(SDTE)) is EnvDTE.DTE dte &&
                    dte.Solution?.FullName is string solutionPath &&
                    !string.IsNullOrWhiteSpace(solutionPath))
                {
                    return System.IO.Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory;
                }
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        private string GetActiveFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (GetService(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection selection)
                {
                    selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out var obj);
                    if (obj is IVsWindowFrame frame)
                    {
                        frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var value);
                        return value as string ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Gets the full content of the active document file.
        /// </summary>
        public string GetActiveFileContentSync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var filePath = GetActiveFilePath();
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    return System.IO.File.ReadAllText(filePath);
                }
            }
            catch { }
            return string.Empty;
        }

        private string GetActiveLanguageId()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var filePath = GetActiveFilePath();
                if (!string.IsNullOrEmpty(filePath))
                {
                    return System.IO.Path.GetExtension(filePath).ToLowerInvariant() switch
                    {
                        ".cs" or ".csx" => "csharp",
                        ".fs" or ".fsx" => "fsharp",
                        ".vb" => "vb",
                        ".js" or ".jsx" => "javascript",
                        ".ts" or ".tsx" => "typescript",
                        ".py" or ".pyw" => "python",
                        ".json" => "json",
                        ".xml" or ".xaml" => "xml",
                        ".html" or ".htm" => "html",
                        ".css" or ".scss" or ".less" => "css",
                        ".md" => "markdown",
                        ".sql" => "sql",
                        ".cpp" or ".cxx" or ".cc" or ".c" => "cpp",
                        ".h" or ".hpp" => "cpp",
                        ".go" => "go",
                        ".rs" => "rust",
                        ".java" => "java",
                        ".kt" => "kotlin",
                        ".rb" => "ruby",
                        ".sh" or ".bash" => "shell",
                        ".ps1" or ".psm1" => "powershell",
                        ".yaml" or ".yml" => "yaml",
                        _ => "unknown"
                    };
                }
            }
            catch { }
            return "unknown";
        }

        private string GetActiveSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (GetService(typeof(SDTE)) is EnvDTE.DTE dte &&
                    dte.ActiveDocument?.Selection is EnvDTE.TextSelection sel)
                {
                    return sel.Text ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        public void ReplaceActiveSelection(string newText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (GetService(typeof(SDTE)) is EnvDTE.DTE dte &&
                    dte.ActiveDocument?.Selection is EnvDTE.TextSelection sel)
                {
                    sel.Delete();
                    sel.Insert(newText);
                }
            }
            catch
            {
                // ignore
            }
        }

        private string GetExtensionDirectory()
        {
            // Walk up from the executing assembly location to find the extension root.
            var assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            return assemblyDir;
        }
    }
}
