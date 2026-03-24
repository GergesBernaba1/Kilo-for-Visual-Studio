using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Contracts.Services
{
    public interface IKiloSessionHostAdapter
    {
        event EventHandler<KiloSessionEvent>? SessionEventReceived;

        KiloConnectionState ConnectionState { get; }

        Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, CancellationToken cancellationToken);

        Task DisconnectAsync(CancellationToken cancellationToken);

        Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, CancellationToken cancellationToken);

        Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, CancellationToken cancellationToken);

        Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken);

        Task DeleteSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken);

        Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken);

        Task SendPromptAsync(KiloChatRequest request, CancellationToken cancellationToken);

        Task AbortSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken);

        Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, CancellationToken cancellationToken);

        Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, CancellationToken cancellationToken);
    }
}
