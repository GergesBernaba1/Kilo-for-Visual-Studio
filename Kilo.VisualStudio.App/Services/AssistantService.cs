using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.App.Services
{
    /// <summary>
    /// Orchestrates a single Kilo assistant interaction via the session host adapter.
    /// Supports both the legacy single-request path (for backwards-compatibility) and the
    /// new session-based streaming path that mirrors the VS Code extension.
    ///
    /// The service is intentionally thin: it delegates all transport concerns to
    /// <see cref="IKiloSessionHostAdapter"/> and collects streamed SSE events into a structured
    /// <see cref="AssistantResponse"/>.
    /// </summary>
    public class AssistantService
    {
        // Default per-request timeout; callers can pass an explicit CancellationToken.
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(5);

        private readonly IKiloBackendClient? _backendClient;
        private readonly IKiloSessionHostAdapter? _sessionHostAdapter;
        private readonly Func<KiloServerEndpoint>? _endpointFactory;

        // ── Streaming callbacks (live UI) ──────────────────────────────────────────

        /// <summary>Fires each time a text delta arrives from the LLM.</summary>
        public event EventHandler<string>? TextDeltaReceived;

        /// <summary>Fires when a tool execution's status changes.</summary>
        public event EventHandler<KiloToolExecution>? ToolExecutionChanged;

        /// <summary>Fires when diff data for the current session changes.</summary>
        public event EventHandler<System.Collections.Generic.IReadOnlyList<KiloFileDiff>>? DiffUpdated;

        /// <summary>Fires on every raw SSE event (for forwarding to UI panels).</summary>
        public event EventHandler<KiloSessionEvent>? SessionEventReceived;

        // ── Constructors ───────────────────────────────────────────────────────────

        public AssistantService(IKiloBackendClient backendClient)
        {
            _backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        }

        public AssistantService(IKiloSessionHostAdapter sessionHostAdapter, Func<KiloServerEndpoint> endpointFactory)
        {
            _sessionHostAdapter = sessionHostAdapter ?? throw new ArgumentNullException(nameof(sessionHostAdapter));
            _endpointFactory = endpointFactory ?? throw new ArgumentNullException(nameof(endpointFactory));
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a prompt and awaits the full turn completion, collecting all streamed content
        /// along the way. Progress events are fired through <see cref="TextDeltaReceived"/>,
        /// <see cref="ToolExecutionChanged"/>, and <see cref="DiffUpdated"/> for live UI updates.
        /// </summary>
        public async Task<AssistantResponse> AskAssistantAsync(
            AssistantRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return new AssistantResponse
                {
                    IsSuccess = false,
                    Error = "Prompt cannot be empty",
                    Message = "Please provide a prompt for the assistant."
                };
            }

            if (_sessionHostAdapter != null && _endpointFactory != null)
            {
                return await AskViaSessionHostAsync(request, cancellationToken);
            }

            if (_backendClient == null)
                throw new InvalidOperationException("No assistant backend is configured.");

            return await _backendClient.SendRequestAsync(request);
        }

        /// <summary>
        /// Sends a prompt into an existing session (for session-reuse scenarios).
        /// </summary>
        public async Task<AssistantResponse> SendToSessionAsync(
            string sessionId,
            KiloChatRequest chatRequest,
            CancellationToken cancellationToken = default)
        {
            if (_sessionHostAdapter == null)
                throw new InvalidOperationException("Session host adapter is not configured.");

            var endpoint = _endpointFactory!();
            await _sessionHostAdapter.ConnectAsync(endpoint, chatRequest.WorkspaceDirectory, cancellationToken);
            return await SendAndCollectAsync(sessionId, chatRequest, cancellationToken);
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private async Task<AssistantResponse> AskViaSessionHostAsync(
            AssistantRequest request,
            CancellationToken callerToken)
        {
            var workspaceDirectory = ResolveWorkspaceDirectory(request.ActiveFilePath);
            var endpoint = _endpointFactory!();

            using var timeoutCts = new CancellationTokenSource(DefaultRequestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);

            await _sessionHostAdapter!.ConnectAsync(endpoint, workspaceDirectory, linkedCts.Token);

            var session = await _sessionHostAdapter.CreateSessionAsync(workspaceDirectory, linkedCts.Token);

            var chatRequest = new KiloChatRequest
            {
                SessionId = session.SessionId,
                WorkspaceDirectory = workspaceDirectory,
                ActiveFilePath = request.ActiveFilePath,
                SelectedText = request.SelectedText,
                LanguageId = request.LanguageId,
                Prompt = request.Prompt
            };

            return await SendAndCollectAsync(session.SessionId, chatRequest, linkedCts.Token);
        }

        private async Task<AssistantResponse> SendAndCollectAsync(
            string sessionId,
            KiloChatRequest chatRequest,
            CancellationToken cancellationToken)
        {
            var textBuilder = new StringBuilder();
            var suggestedCode = string.Empty;
            var patchDiff = string.Empty;
            var tcs = new TaskCompletionSource<AssistantResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            void HandleEvent(object? sender, KiloSessionEvent ev)
            {
                // Broadcast to any external subscribers first.
                SessionEventReceived?.Invoke(this, ev);

                // Only process events for our session.
                if (!string.IsNullOrEmpty(ev.SessionId) && ev.SessionId != sessionId)
                    return;

                switch (ev.Kind)
                {
                    case KiloSessionEventKind.TextDelta:
                        if (!string.IsNullOrEmpty(ev.Delta))
                        {
                            textBuilder.Append(ev.Delta);
                            TextDeltaReceived?.Invoke(this, ev.Delta);
                        }
                        break;

                    case KiloSessionEventKind.PartUpdated:
                    case KiloSessionEventKind.MessageUpdated:
                        if (!string.IsNullOrEmpty(ev.Message))
                        {
                            textBuilder.Clear();
                            textBuilder.Append(ev.Message);
                        }
                        break;

                    case KiloSessionEventKind.ToolExecutionUpdated:
                        if (ev.ToolExecution != null)
                        {
                            if (!string.IsNullOrEmpty(ev.ToolExecution.SuggestedCode))
                                suggestedCode = ev.ToolExecution.SuggestedCode;
                            if (!string.IsNullOrEmpty(ev.ToolExecution.PatchDiff))
                                patchDiff = ev.ToolExecution.PatchDiff;
                            ToolExecutionChanged?.Invoke(this, ev.ToolExecution);
                        }
                        break;

                    case KiloSessionEventKind.DiffUpdated:
                        if (!string.IsNullOrEmpty(ev.PatchDiff))
                            patchDiff = ev.PatchDiff;
                        if (ev.FileDiffs != null && ev.FileDiffs.Count > 0)
                            DiffUpdated?.Invoke(this, ev.FileDiffs);
                        break;

                    case KiloSessionEventKind.Error:
                        tcs.TrySetResult(new AssistantResponse
                        {
                            IsSuccess = false,
                            Error = string.IsNullOrWhiteSpace(ev.Error) ? "Session host reported an error." : ev.Error,
                            Message = textBuilder.Length > 0 ? textBuilder.ToString() : ev.Message
                        });
                        break;

                    case KiloSessionEventKind.TurnCompleted:
                        tcs.TrySetResult(new AssistantResponse
                        {
                            IsSuccess = true,
                            Message = textBuilder.Length > 0 ? textBuilder.ToString() : "Kilo completed the request.",
                            SuggestedCode = string.IsNullOrWhiteSpace(suggestedCode) ? null : suggestedCode,
                            PatchDiff = string.IsNullOrWhiteSpace(patchDiff) ? null : patchDiff
                        });
                        break;
                }
            }

            _sessionHostAdapter!.SessionEventReceived += HandleEvent;
            try
            {
                using (cancellationToken.Register(() =>
                    tcs.TrySetResult(new AssistantResponse
                    {
                        IsSuccess = false,
                        Error = "Request cancelled or timed out.",
                        Message = textBuilder.Length > 0 ? textBuilder.ToString() : "The Kilo session was cancelled."
                    })))
                {
                    await _sessionHostAdapter.SendPromptAsync(chatRequest, cancellationToken);
                    return await tcs.Task;
                }
            }
            finally
            {
                _sessionHostAdapter.SessionEventReceived -= HandleEvent;
            }
        }

        private static string ResolveWorkspaceDirectory(string activeFilePath)
        {
            if (string.IsNullOrWhiteSpace(activeFilePath))
                return Environment.CurrentDirectory;
            if (Directory.Exists(activeFilePath))
                return activeFilePath;
            var dir = Path.GetDirectoryName(activeFilePath);
            return string.IsNullOrWhiteSpace(dir) ? Environment.CurrentDirectory : dir;
        }
    }
}
