using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Integration
{
    /// <summary>
    /// Mirrors the VS Code ServerManager. Discovers the kilo CLI binary bundled with the
    /// extension (bin/kilo.exe on Windows), spawns "kilo serve --port 0", reads the listening
    /// port from stdout, and owns the process lifetime. A single shared instance is re-used
    /// for the entire extension session; callers that call <see cref="GetServerAsync"/> while
    /// startup is already in progress share the same task.
    /// </summary>
    public sealed class KiloServerManager : IDisposable
    {
        private const int StartupTimeoutSeconds = 30;
        private const int HealthPollIntervalMs = 10_000;
        private const string ListeningPattern = @"listening on https?://[\w.]+:(\d+)";

        private readonly string _extensionDirectory;
        private readonly object _gate = new object();

        private KiloServerInstance? _instance;
        private Task<KiloServerInstance>? _startupTask;
        private Timer? _healthTimer;
        private bool _disposed;

        public KiloServerManager(string extensionDirectory)
        {
            _extensionDirectory = extensionDirectory ?? throw new ArgumentNullException(nameof(extensionDirectory));
        }

        public KiloServerInstance? CurrentInstance
        {
            get { lock (_gate) { return _instance; } }
        }

        public event EventHandler<string>? ServerDied;

        /// <summary>
        /// Returns the running server, starting it if necessary. Concurrent callers share
        /// the same startup task so only one process is ever spawned.
        /// </summary>
        public Task<KiloServerInstance> GetServerAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_instance != null) return Task.FromResult(_instance);
                if (_startupTask != null) return _startupTask;

                _startupTask = StartServerCoreAsync(cancellationToken);
                _startupTask.ContinueWith(t =>
                {
                    lock (_gate)
                    {
                        _startupTask = null;
                        if (!t.IsFaulted && !t.IsCanceled)
                        {
                            _instance = t.Result;
                            ScheduleHealthPoll();
                        }
                    }
                }, TaskScheduler.Default);

                return _startupTask;
            }
        }

        /// <summary>
        /// Stops the running server process and clears the stored instance.
        /// </summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;

                _healthTimer?.Dispose();
                _healthTimer = null;

                KillInstance(_instance);
                _instance = null;
            }
        }

        // ── Internal ───────────────────────────────────────────────────────────────

        private async Task<KiloServerInstance> StartServerCoreAsync(CancellationToken cancellationToken)
        {
            var cliPath = ResolveCliBinaryPath();
            if (!File.Exists(cliPath))
            {
                throw new KiloServerStartupException(
                    $"Kilo CLI binary not found at: {cliPath}",
                    $"The Kilo CLI was expected at '{cliPath}'. "
                    + "Ensure the extension was installed correctly and the bin/ folder is present.");
            }

            var password = GeneratePassword();

            var startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = "serve --port 0",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Pass environment variables the VS Code extension passes.
            startInfo.Environment["KILO_SERVER_PASSWORD"] = password;
            startInfo.Environment["KILO_CLIENT"] = "visual-studio";
            startInfo.Environment["KILO_ENABLE_QUESTION_TOOL"] = "true";
            startInfo.Environment["KILOCODE_FEATURE"] = "vs-extension";

            var process = new Process { StartInfo = startInfo };
            process.EnableRaisingEvents = true;

            var tcs = new TaskCompletionSource<KiloServerInstance>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stderrLines = new System.Collections.Generic.List<string>();
            var resolved = false;
            var resolvedLock = new object();

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data == null) return;

                var match = Regex.Match(args.Data, ListeningPattern, RegexOptions.IgnoreCase);
                if (!match.Success) return;

                int port;
                if (!int.TryParse(match.Groups[1].Value, out port)) return;

                lock (resolvedLock)
                {
                    if (resolved) return;
                    resolved = true;
                }

                tcs.TrySetResult(new KiloServerInstance(port, password, process));
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    stderrLines.Add(args.Data);
                }
            };

            process.Exited += (_, __) =>
            {
                lock (resolvedLock)
                {
                    if (resolved) return;
                    resolved = true;
                }

                var details = string.Join(Environment.NewLine, stderrLines);
                tcs.TrySetException(new KiloServerStartupException(
                    "Kilo server process exited before reporting a port.",
                    string.IsNullOrWhiteSpace(details) ? "No output was captured from stderr." : details));

                lock (_gate)
                {
                    if (_instance?.Process == process)
                    {
                        _instance = null;
                    }
                }

                ServerDied?.Invoke(this, $"Process exited with code {process.ExitCode}");
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Register cancellation so we kill the process if cancelled.
            cancellationToken.Register(() =>
            {
                lock (resolvedLock)
                {
                    if (resolved) return;
                    resolved = true;
                }
                KillProcess(process);
                tcs.TrySetCanceled(cancellationToken);
            });

            // Startup timeout.
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(StartupTimeoutSeconds));
            timeoutCts.Token.Register(() =>
            {
                lock (resolvedLock)
                {
                    if (resolved) return;
                    resolved = true;
                }
                KillProcess(process);
                var details = string.Join(Environment.NewLine, stderrLines);
                tcs.TrySetException(new KiloServerStartupException(
                    $"Kilo server did not start within {StartupTimeoutSeconds} seconds.",
                    string.IsNullOrWhiteSpace(details) ? "Timeout reached with no port output." : details));
            });

            var instance = await tcs.Task;
            timeoutCts.Dispose();
            return instance;
        }

        private string ResolveCliBinaryPath()
        {
            // 1. Bundled binary alongside the extension DLLs.
            var binName = IsWindows() ? "kilo.exe" : "kilo";
            var bundled = Path.Combine(_extensionDirectory, "bin", binName);
            if (File.Exists(bundled)) return bundled;

            // 2. Global PATH lookup (for dev environments where kilo is installed globally).
            var pathBin = FindOnPath(binName);
            if (!string.IsNullOrWhiteSpace(pathBin)) return pathBin;

            // Return the expected bundled path so the error message is descriptive.
            return bundled;
        }

        private static string? FindOnPath(string exeName)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static bool IsWindows() =>
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);

        private static string GeneratePassword()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private void ScheduleHealthPoll()
        {
            _healthTimer?.Dispose();
            _healthTimer = new Timer(OnHealthPoll, null, HealthPollIntervalMs, HealthPollIntervalMs);
        }

        private void OnHealthPoll(object? state)
        {
            KiloServerInstance? current;
            lock (_gate) { current = _instance; }
            if (current == null) return;

            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var credentials = System.Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"kilo:{current.Password}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var response = client.GetAsync($"{current.BaseUrl}/global/health").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    HandleServerUnhealthy(current, $"Health check returned {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                HandleServerUnhealthy(current, ex.Message);
            }
        }

        private void HandleServerUnhealthy(KiloServerInstance instance, string reason)
        {
            lock (_gate)
            {
                if (_instance != instance) return;
                _instance = null;
                _healthTimer?.Dispose();
                _healthTimer = null;
            }
            ServerDied?.Invoke(this, reason);
        }

        private static void KillInstance(KiloServerInstance? instance)
        {
            if (instance == null) return;
            KillProcess(instance.Process);
        }

        private static void KillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Ignore — process already gone.
            }
        }
    }

    public sealed class KiloServerInstance
    {
        public int Port { get; }
        public string Password { get; }
        public string BaseUrl => $"http://127.0.0.1:{Port}";
        internal Process Process { get; }

        internal KiloServerInstance(int port, string password, Process process)
        {
            Port = port;
            Password = password ?? throw new ArgumentNullException(nameof(password));
            Process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public KiloServerEndpoint ToEndpoint() => new KiloServerEndpoint
        {
            BaseUrl = BaseUrl,
            Password = Password
        };
    }

    public sealed class KiloServerStartupException : Exception
    {
        public string UserMessage { get; }
        public string UserDetails { get; }

        public KiloServerStartupException(string userMessage, string userDetails)
            : base(userDetails)
        {
            UserMessage = userMessage;
            UserDetails = userDetails;
        }
    }
}
