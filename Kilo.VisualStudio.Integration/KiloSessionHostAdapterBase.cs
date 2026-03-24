using System;
using System.Collections.Generic;
using System.Text;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.Integration
{
    public abstract class KiloSessionHostAdapterBase : IKiloSessionHostAdapter
    {
        public event EventHandler<KiloSessionEvent>? SessionEventReceived;

        public abstract KiloConnectionState ConnectionState { get; }

        public abstract System.Threading.Tasks.Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task DisconnectAsync(System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task DeleteSessionAsync(string sessionId, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task SendPromptAsync(KiloChatRequest request, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task AbortSessionAsync(string sessionId, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        public abstract System.Threading.Tasks.Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, System.Threading.CancellationToken cancellationToken);

        protected void Publish(KiloSessionEvent sessionEvent)
        {
            SessionEventReceived?.Invoke(this, sessionEvent);
        }

        protected void PublishConnectionState(KiloConnectionState connectionState, string message = "")
        {
            Publish(new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.ConnectionStateChanged,
                EventType = "connection.state",
                ConnectionState = connectionState,
                Message = message,
                TimestampUtc = DateTimeOffset.UtcNow
            });
        }

        protected static string BuildPatchDiff(IReadOnlyList<KiloFileDiff> fileDiffs)
        {
            if (fileDiffs == null || fileDiffs.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < fileDiffs.Count; index++)
            {
                var diff = fileDiffs[index];
                if (index > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("--- a/").AppendLine(diff.FilePath);
                builder.Append("+++ b/").AppendLine(diff.FilePath);
                builder.Append("@@ -").Append(diff.Deletions).Append(" +").Append(diff.Additions).AppendLine(" @@");

                if (!string.IsNullOrWhiteSpace(diff.Before))
                {
                    AppendWithPrefix(builder, diff.Before, '-');
                }

                if (!string.IsNullOrWhiteSpace(diff.After))
                {
                    AppendWithPrefix(builder, diff.After, '+');
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendWithPrefix(StringBuilder builder, string value, char prefix)
        {
            var normalized = value.Replace("\r\n", "\n").Split('\n');
            foreach (var line in normalized)
            {
                builder.Append(prefix).AppendLine(line);
            }
        }
    }
}
