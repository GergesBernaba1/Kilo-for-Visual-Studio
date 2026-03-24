using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(KiloAssistantToolWindowPane))]
    [ProvideToolWindow(typeof(KiloDiffViewerWindowPane))]
    [ProvideToolWindow(typeof(KiloSessionHistoryWindowPane))]
    [ProvideToolWindow(typeof(KiloSettingsWindowPane))]
    [Guid(PackageGuids.PackageGuidString)]
    public sealed class KiloPackage : AsyncPackage
    {
        private ExtensionSettings _extensionSettings = ExtensionSettings.Load();
        private readonly KiloLogger _logger = new KiloLogger();

        // Service layer – either mock or real depending on settings.
        private KiloConnectionService? _connectionService;
        private MockKiloSessionHostAdapter? _mockAdapter;
        private AssistantService? _assistantService;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _logger.Info("Kilo Extension initializing…");

            if (_extensionSettings.UseMockBackend)
            {
                // ── Mock path: no CLI process needed. ──────────────────────────
                _mockAdapter = new MockKiloSessionHostAdapter();
                _assistantService = new AssistantService(_mockAdapter, () => new KiloServerEndpoint());
                _mockAdapter.SessionEventReceived += OnSessionEvent;
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
                    () => _connectionService.ServerInstance?.ToEndpoint() ?? new KiloServerEndpoint());

                // Start connection eagerly so the UI shows its state quickly.
                _ = _connectionService
                        .EnsureConnectedAsync(GetWorkspaceDirectory(), cancellationToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                _logger.Error("Kilo connection failed on startup.", t.Exception?.InnerException);
                        }, TaskScheduler.Default);
            }

            await RegisterCommandsAsync();
            _logger.Info("Kilo Extension initialized.");
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
            _logger.Info($"Kilo connection state → {state}");
            BroadcastToToolWindow(control => control.UpdateConnectionState(state));
        }

        private void OnSessionEvent(object? sender, KiloSessionEvent e)
        {
            // Forward SSE events to the visible tool window.
            BroadcastToToolWindow(control => control.HandleSessionEvent(e));
        }

        private void BroadcastToToolWindow(Action<KiloAssistantToolWindowControl> action)
        {
            // Must not block; UI dispatch happens on the captured dispatcher inside the control.
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)(() =>
                {
                    var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
                    if (window?.Content is KiloAssistantToolWindowControl control)
                    {
                        action(control);
                    }
                }));
        }

        // ── Control event handlers ──────────────────────────────────────────────────

        private void WireControlEvents(KiloAssistantToolWindowControl control)
        {
            control.ReconnectRequested -= OnReconnectRequested;
            control.ReconnectRequested += OnReconnectRequested;
            control.ApplyDiffsRequested -= OnApplyDiffsRequested;
            control.ApplyDiffsRequested += OnApplyDiffsRequested;
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
                            _logger.Error("Kilo reconnect failed.", t.Exception?.InnerException);
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

        private async Task RegisterCommandsAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                var packageGuid = new Guid(PackageGuids.PackageGuidString);

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenToolWindowAsync(),
                    new CommandID(packageGuid, PackageCommandSet.OpenAssistantToolWindow)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskSelectionAsync(),
                    new CommandID(packageGuid, PackageCommandSet.AskSelection)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = AskFileAsync(),
                    new CommandID(packageGuid, PackageCommandSet.AskFile)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenDiffViewerWindowAsync(),
                    new CommandID(packageGuid, PackageCommandSet.OpenDiffViewer)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenSessionHistoryWindowAsync(),
                    new CommandID(packageGuid, PackageCommandSet.OpenSessionHistory)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = OpenSettingsWindowAsync(),
                    new CommandID(packageGuid, PackageCommandSet.OpenSettings)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = CycleAgentModeAsync(),
                    new CommandID(packageGuid, PackageCommandSet.CycleAgentMode)));

                commandService.AddCommand(new MenuCommand(
                    (s, e) => _ = NewSessionAsync(),
                    new CommandID(packageGuid, PackageCommandSet.NewSession)));
            }
        }

        private async Task OpenToolWindowAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo tool window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());

            if (window is KiloAssistantToolWindowPane pane &&
                pane.Content is KiloAssistantToolWindowControl control)
            {
                if (_assistantService != null)
                    control.Initialize(_assistantService, _extensionSettings);

                WireControlEvents(control);

                var state = _connectionService?.State ?? KiloConnectionState.Disconnected;
                if (_extensionSettings.UseMockBackend)
                    state = KiloConnectionState.Connected;
                control.UpdateConnectionState(state);
                control.SetContext(GetActiveFilePath(), GetActiveSelection(), GetActiveLanguageId());
            }
        }

        private async Task AskSelectionAsync()
        {
            await OpenToolWindowAsync();

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

        private async Task AskFileAsync()
        {
            await OpenToolWindowAsync();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                var filePath = GetActiveFilePath();
                control.SetContext(filePath, GetActiveSelection(), GetActiveLanguageId());
                control.SetPrompt($"Analyze this file and explain what it does: {filePath}");
            }
        }

        private async Task OpenDiffViewerWindowAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloDiffViewerWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Diff Viewer window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task OpenSessionHistoryWindowAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloSessionHistoryWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Session History window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task OpenSettingsWindowAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();

            var window = FindToolWindow(typeof(KiloSettingsWindowPane), 0, true);
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create Kilo Settings window.");

            var frame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }

        private async Task CycleAgentModeAsync()
        {
            await OpenToolWindowAsync();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                control.CycleAgentMode();
            }
        }

        private async Task NewSessionAsync()
        {
            await OpenToolWindowAsync();

            var window = FindToolWindow(typeof(KiloAssistantToolWindowPane), 0, false);
            if (window?.Content is UI.KiloAssistantToolWindowControl control)
            {
                control.CreateNewSession();
            }
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

        private string GetExtensionDirectory()
        {
            // Walk up from the executing assembly location to find the extension root.
            var assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            return assemblyDir;
        }
    }
}
