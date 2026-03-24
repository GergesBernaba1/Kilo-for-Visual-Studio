using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Integration
{
    public class MockKiloSessionHostAdapter : KiloSessionHostAdapterBase
    {
        private readonly Dictionary<string, KiloSessionSummary> _sessions = new Dictionary<string, KiloSessionSummary>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<KiloSessionMessage>> _messages = new Dictionary<string, List<KiloSessionMessage>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<KiloFileDiff>> _diffs = new Dictionary<string, List<KiloFileDiff>>(StringComparer.OrdinalIgnoreCase);
        private KiloConnectionState _connectionState = KiloConnectionState.Disconnected;

        public override KiloConnectionState ConnectionState => _connectionState;

        public override Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _connectionState = KiloConnectionState.Connected;
            PublishConnectionState(_connectionState, "Mock Kilo session adapter connected.");
            return Task.CompletedTask;
        }

        public override Task DisconnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _connectionState = KiloConnectionState.Disconnected;
            PublishConnectionState(_connectionState, "Mock Kilo session adapter disconnected.");
            return Task.CompletedTask;
        }

        public override Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = new KiloSessionSummary
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Title = "Mock Kilo Session",
                WorkspaceDirectory = workspaceDirectory ?? string.Empty,
                Status = KiloSessionStatus.Idle,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _sessions[session.SessionId] = session;
            _messages[session.SessionId] = new List<KiloSessionMessage>();
            _diffs[session.SessionId] = new List<KiloFileDiff>();

            Publish(new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.SessionCreated,
                EventType = "session.created",
                SessionId = session.SessionId,
                Session = session,
                TimestampUtc = DateTimeOffset.UtcNow
            });

            return Task.FromResult(session);
        }

        public override Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<KiloSessionSummary> sessions = _sessions.Values.Where(session =>
                string.Equals(session.WorkspaceDirectory, workspaceDirectory ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(sessions);
        }

        public override Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult<KiloSessionSummary?>(session);
        }

        public override Task DeleteSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sessions.Remove(sessionId);
            _messages.Remove(sessionId);
            _diffs.Remove(sessionId);

            Publish(new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.SessionDeleted,
                EventType = "session.deleted",
                SessionId = sessionId,
                TimestampUtc = DateTimeOffset.UtcNow
            });

            return Task.CompletedTask;
        }

        public override Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<KiloSessionMessage> messages = _messages.TryGetValue(sessionId, out var sessionMessages)
                ? sessionMessages.ToList()
                : new KiloSessionMessage[0];
            return Task.FromResult(messages);
        }

        public override Task SendPromptAsync(KiloChatRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_sessions.TryGetValue(request.SessionId, out var session))
            {
                throw new InvalidOperationException("Session not found.");
            }

            session.Status = KiloSessionStatus.Running;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;

            var userMessage = new KiloSessionMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                SessionId = request.SessionId,
                Role = "user",
                Content = request.Prompt,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _messages[request.SessionId].Add(userMessage);

            _ = Task.Run(async () =>
            {
                Publish(new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.TurnStarted,
                    EventType = "session.turn.open",
                    SessionId = request.SessionId,
                    TimestampUtc = DateTimeOffset.UtcNow
                });

                var responseText = BuildResponseText(request);
                var assistantMessageId = Guid.NewGuid().ToString("N");
                var assistantMessage = new KiloSessionMessage
                {
                    MessageId = assistantMessageId,
                    SessionId = request.SessionId,
                    Role = "assistant",
                    Content = responseText,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                _messages[request.SessionId].Add(assistantMessage);

                var chunks = Chunk(responseText, 48);
                foreach (var chunk in chunks)
                {
                    await Task.Delay(20, cancellationToken);
                    Publish(new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.TextDelta,
                        EventType = "message.part.delta",
                        SessionId = request.SessionId,
                        MessageId = assistantMessageId,
                        Delta = chunk,
                        TimestampUtc = DateTimeOffset.UtcNow
                    });
                }

                if (ShouldEmitCodeArtifacts(request))
                {
                    var suggestedCode = BuildSuggestedCode(request);
                    var fileDiffs = new List<KiloFileDiff>
                    {
                        new KiloFileDiff
                        {
                            FilePath = string.IsNullOrWhiteSpace(request.ActiveFilePath) ? "CurrentDocument.cs" : request.ActiveFilePath,
                            Before = string.IsNullOrWhiteSpace(request.SelectedText) ? "// existing implementation" : request.SelectedText,
                            After = suggestedCode,
                            Additions = CountLines(suggestedCode),
                            Deletions = CountLines(request.SelectedText),
                            Status = string.IsNullOrWhiteSpace(request.SelectedText) ? "added" : "modified"
                        }
                    };

                    _diffs[request.SessionId] = fileDiffs;
                    var patchDiff = BuildPatchDiff(fileDiffs);

                    Publish(new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.ToolExecutionUpdated,
                        EventType = "message.part.updated",
                        SessionId = request.SessionId,
                        MessageId = assistantMessageId,
                        ToolExecution = new KiloToolExecution
                        {
                            CallId = Guid.NewGuid().ToString("N"),
                            ToolName = string.IsNullOrWhiteSpace(request.SelectedText) ? "write" : "edit",
                            Title = "Apply generated patch",
                            Status = KiloToolExecutionStatus.Completed,
                            SuggestedCode = suggestedCode,
                            PatchDiff = patchDiff,
                            FileDiffs = fileDiffs
                        },
                        SuggestedCode = suggestedCode,
                        PatchDiff = patchDiff,
                        TimestampUtc = DateTimeOffset.UtcNow
                    });

                    Publish(new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.DiffUpdated,
                        EventType = "session.diff",
                        SessionId = request.SessionId,
                        FileDiffs = fileDiffs,
                        PatchDiff = patchDiff,
                        TimestampUtc = DateTimeOffset.UtcNow
                    });
                }

                session.Status = KiloSessionStatus.Completed;
                session.UpdatedAtUtc = DateTimeOffset.UtcNow;

                Publish(new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.TurnCompleted,
                    EventType = "session.turn.close",
                    SessionId = request.SessionId,
                    Message = responseText,
                    TimestampUtc = DateTimeOffset.UtcNow
                });
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public override Task AbortSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Status = KiloSessionStatus.Aborted;
                session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            Publish(new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.Error,
                EventType = "session.error",
                SessionId = sessionId,
                Error = "Session aborted by the user.",
                Message = "Session aborted by the user.",
                TimestampUtc = DateTimeOffset.UtcNow
            });

            return Task.CompletedTask;
        }

    public override Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<KiloFileDiff> diffs = _diffs.TryGetValue(sessionId, out var sessionDiffs)
            ? sessionDiffs.ToList()
            : new KiloFileDiff[0];
        // Filter by workspaceDirectory to match the behavior of the real implementation
        if (!string.IsNullOrWhiteSpace(workspaceDirectory))
        {
            diffs = diffs.Where(d => string.Equals(d.FilePath, workspaceDirectory, StringComparison.OrdinalIgnoreCase) || 
                                   d.FilePath.StartsWith(workspaceDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        .ToList();
        }
        return Task.FromResult(diffs);
    }

        public override Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> toolIds = new[] { "read", "edit", "write", "bash", "glob", "grep" };
            return Task.FromResult(toolIds);
        }

        public override Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private static bool ShouldEmitCodeArtifacts(KiloChatRequest request)
        {
            var prompt = request.Prompt?.ToLowerInvariant() ?? string.Empty;
            return prompt.Contains("refactor") || prompt.Contains("improve") || prompt.Contains("generate") || prompt.Contains("create");
        }

        private static string BuildResponseText(KiloChatRequest request)
        {
            var prompt = request.Prompt?.Trim() ?? string.Empty;
            var selectionLength = string.IsNullOrEmpty(request.SelectedText) ? 0 : request.SelectedText.Length;
            return "[MOCK SESSION] Kilo processed the request using the session-based protocol.\n\n"
                + "Prompt: " + prompt + "\n"
                + "Language: " + request.LanguageId + "\n"
                + "Active file: " + request.ActiveFilePath + "\n"
                + "Selected text length: " + selectionLength + " characters\n\n"
                + "This response streamed through the Visual Studio host adapter instead of the legacy single-request REST helper.";
        }

        private static string BuildSuggestedCode(KiloChatRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.SelectedText))
            {
                return "// Session-host generated proposal\n" + request.SelectedText + "\n// Additional resiliency and validation can be added here.";
            }

            return "public async Task<string> ExecuteAsync(CancellationToken cancellationToken)\n{\n    await Task.Delay(50, cancellationToken);\n    return \"Generated by the Kilo session host adapter\";\n}";
        }

        private static IReadOnlyList<string> Chunk(string value, int chunkSize)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return chunks;
            }

            for (var index = 0; index < value.Length; index += chunkSize)
            {
                var size = Math.Min(chunkSize, value.Length - index);
                chunks.Add(value.Substring(index, size));
            }

            return chunks;
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            return value.Replace("\r\n", "\n").Split('\n').Length;
        }
    }
}
