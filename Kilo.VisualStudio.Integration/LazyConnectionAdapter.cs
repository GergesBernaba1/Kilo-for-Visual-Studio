using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.Integration
{
    /// <summary>
    /// Wraps <see cref="KiloConnectionService"/> and exposes it as an
    /// <see cref="IKiloSessionHostAdapter"/>. Defers adapter access until the first
    /// call so that <see cref="AssistantService"/> can be constructed before the
    /// Kilo CLI has finished starting up.
    /// </summary>
    public sealed class LazyConnectionAdapter : IKiloSessionHostAdapter
    {
        private readonly KiloConnectionService _service;

        public LazyConnectionAdapter(KiloConnectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            // Forward SSE events from the shared connection service.
            _service.EventReceived += OnServiceEvent;
        }

        // ── IKiloSessionHostAdapter ────────────────────────────────────────────────

        public event EventHandler<KiloSessionEvent>? SessionEventReceived;

        public KiloConnectionState ConnectionState => _service.State;

        public async Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, CancellationToken cancellationToken)
        {
            // The KiloConnectionService already owns server startup — just ensure it has connected.
            await _service.EnsureConnectedAsync(workspaceDirectory, cancellationToken);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            // Do not dispose the shared service on behalf of a single request.
            return Task.CompletedTask;
        }

        public Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.CreateSessionAsync(workspaceDirectory, cancellationToken);

        public Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.ListSessionsAsync(workspaceDirectory, cancellationToken);

        public Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.GetSessionAsync(sessionId, workspaceDirectory, cancellationToken);

        public Task DeleteSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.DeleteSessionAsync(sessionId, workspaceDirectory, cancellationToken);

        public Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.GetMessagesAsync(sessionId, workspaceDirectory, cancellationToken);

        public Task SendPromptAsync(KiloChatRequest request, CancellationToken cancellationToken)
            => Adapter.SendPromptAsync(request, cancellationToken);

        public Task AbortSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.AbortSessionAsync(sessionId, workspaceDirectory, cancellationToken);

        public Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.GetSessionDiffAsync(sessionId, workspaceDirectory, cancellationToken);

        public Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.GetRegisteredToolIdsAsync(workspaceDirectory, cancellationToken);

        public Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, CancellationToken cancellationToken)
            => Adapter.ReplyToToolPermissionAsync(reply, workspaceDirectory, cancellationToken);

        // ── Private ────────────────────────────────────────────────────────────────

        private IKiloSessionHostAdapter Adapter => _service.Adapter;

        private void OnServiceEvent(object? sender, KiloSessionEvent e)
        {
            SessionEventReceived?.Invoke(this, e);
        }
    }
}
