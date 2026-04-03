using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.Integration
{
    /// <summary>
    /// Mirrors the VS Code KiloConnectionService. Owns a single <see cref="KiloServerManager"/>
    /// and a single <see cref="KiloProtocolSessionHostAdapter"/>. Multiple callers (tool windows,
    /// commands, etc.) subscribe to the shared event bus for SSE events and connection-state
    /// changes rather than each creating their own HTTP streams.
    ///
    /// Lifecycle:
    ///   1. Caller calls <see cref="EnsureConnectedAsync"/> (idempotent, coalesces concurrent calls).
    ///   2. Service spawns the CLI via <see cref="KiloServerManager"/> if not already running.
    ///   3. Service connects the <see cref="KiloProtocolSessionHostAdapter"/> to the discovered port.
    ///   4. Connection-state transitions are broadcast to all <see cref="StateChanged"/> subscribers.
    ///   5. All SSE events are broadcast to all <see cref="EventReceived"/> subscribers.
    ///   6. If the server process dies the service transitions to Disconnected and attempts reconnect.
    ///   7. Callers call <see cref="Dispose"/> to kill the process and release resources.
    /// </summary>
    public sealed class KiloConnectionService : IDisposable
    {
        private const int ReconnectDelayMs = 1_500;

        private readonly KiloServerManager _serverManager;
        private readonly KiloProtocolSessionHostAdapter _adapter;
        private readonly object _connectGate = new object();
        private readonly string _extensionDirectory;

        // Shared HttpClient to avoid TCP port exhaustion from per-instance creation.
        private static readonly HttpClient SharedHttpClient = new HttpClient();

        private Task? _connectTask;
        private KiloConnectionState _state = KiloConnectionState.Disconnected;
        private bool _disposed;
        private string _workspaceDirectory = string.Empty;

        // ── Public events ──────────────────────────────────────────────────────────

        /// <summary>Fires whenever the connection state changes.</summary>
        public event EventHandler<KiloConnectionState>? StateChanged;

        /// <summary>Fires for every SSE event received from the Kilo server.</summary>
        public event EventHandler<KiloSessionEvent>? EventReceived;

        // ── Constructor ────────────────────────────────────────────────────────────

        public KiloConnectionService(string extensionDirectory)
        {
            _extensionDirectory = extensionDirectory ?? throw new ArgumentNullException(nameof(extensionDirectory));
            _serverManager = new KiloServerManager(_extensionDirectory);
            _adapter = new KiloProtocolSessionHostAdapter(SharedHttpClient);

            // Bubble all SSE events from the adapter to our subscribers.
            _adapter.SessionEventReceived += OnAdapterEvent;

            // React to server process death.
            _serverManager.ServerDied += OnServerDied;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public KiloConnectionState State => _state;

        public KiloServerInstance? ServerInstance => _serverManager.CurrentInstance;

        /// <summary>
        /// Gets the shared adapter so callers can invoke session methods (create, chat, etc.).
        /// Throws if not connected.
        /// </summary>
        public IKiloSessionHostAdapter Adapter
        {
            get
            {
                if (_state != KiloConnectionState.Connected)
                    throw new InvalidOperationException($"Kilo is not connected (state={_state}). Call EnsureConnectedAsync first.");
                return _adapter;
            }
        }

        /// <summary>
        /// Idempotent: starts the CLI and opens the SSE stream if not already connected.
        /// Concurrent callers share the same in-flight connect task.
        /// </summary>
        public Task EnsureConnectedAsync(string workspaceDirectory, CancellationToken cancellationToken = default)
        {
            lock (_connectGate)
            {
                if (_state == KiloConnectionState.Connected) return Task.CompletedTask;
                if (_connectTask != null) return _connectTask;

                _workspaceDirectory = workspaceDirectory ?? string.Empty;
                _connectTask = ConnectCoreAsync(_workspaceDirectory, cancellationToken)
                    .ContinueWith(t => { lock (_connectGate) { _connectTask = null; } }, TaskScheduler.Default);

                return _connectTask;
            }
        }

        public void Dispose()
        {
            lock (_connectGate)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _adapter.SessionEventReceived -= OnAdapterEvent;
            _serverManager.ServerDied -= OnServerDied;

            // Use a timeout-bounded wait to avoid freezing VS on extension unload.
            // Do NOT use .GetAwaiter().GetResult() — it can deadlock if a sync context is active.
            try
            {
                var disconnectTask = _adapter.DisconnectAsync(CancellationToken.None);
                if (!disconnectTask.Wait(TimeSpan.FromSeconds(3)))
                {
                    System.Diagnostics.Debug.WriteLine("Kilo: disconnect timed out after 3s during Dispose.");
                }
            }
            catch (AggregateException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Kilo: error during disconnect in Dispose: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Kilo: error during disconnect in Dispose: {ex.Message}");
            }

            _serverManager.Dispose();
            // Do NOT dispose SharedHttpClient — it is static and shared across instances.
        }

        // ── Internal ───────────────────────────────────────────────────────────────

        private async Task ConnectCoreAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            SetState(KiloConnectionState.Connecting);
            try
            {
                // 1. Ensure the CLI server is running.
                var server = await _serverManager.GetServerAsync(cancellationToken);

                // 2. Connect the protocol adapter to the discovered endpoint.
                await _adapter.ConnectAsync(server.ToEndpoint(), workspaceDirectory, cancellationToken);

                SetState(KiloConnectionState.Connected);
            }
            catch (OperationCanceledException)
            {
                SetState(KiloConnectionState.Disconnected);
                throw;
            }
            catch (Exception ex)
            {
                SetState(KiloConnectionState.Error);

                EventReceived?.Invoke(this, new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.Error,
                    EventType = "connection.error",
                    Error = ex.Message,
                    Message = ex is KiloServerStartupException sse ? sse.UserMessage : ex.Message,
                    TimestampUtc = DateTimeOffset.UtcNow
                });

                throw;
            }
        }

        private void SetState(KiloConnectionState newState)
        {
            if (_state == newState) return;
            _state = newState;
            StateChanged?.Invoke(this, newState);
        }

        private void OnAdapterEvent(object? sender, KiloSessionEvent e)
        {
            // Mirror connection-state transitions fired from the adapter's own SSE loop.
            if (e.Kind == KiloSessionEventKind.ConnectionStateChanged)
            {
                SetState(e.ConnectionState);
            }

            EventReceived?.Invoke(this, e);
        }

        private void OnServerDied(object? sender, string reason)
        {
            SetState(KiloConnectionState.Disconnected);

            EventReceived?.Invoke(this, new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.Error,
                EventType = "server.died",
                Error = reason,
                Message = $"The Kilo server process stopped: {reason}",
                TimestampUtc = DateTimeOffset.UtcNow
            });

            // Fire-and-forget reconnect after a short delay.
            // Use async continuation to avoid blocking a thread pool thread.
            _ = Task.Delay(ReconnectDelayMs)
                    .ContinueWith(async _ =>
                    {
                        if (!_disposed && _state == KiloConnectionState.Disconnected)
                        {
                            try
                            {
                                await EnsureConnectedAsync(_workspaceDirectory);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Kilo: reconnect failed: {ex.Message}");
                                SetState(KiloConnectionState.Error);
                            }
                        }
                    }, TaskScheduler.Default).Unwrap();
        }
    }
}
