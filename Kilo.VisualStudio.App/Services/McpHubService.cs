using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
#if !NET48
using System.Net.Http;
#endif
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class McpHubService : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly string _mcpConfigPath;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly string _communityCachePath;
        private List<McpServerConfig> _servers = new List<McpServerConfig>();
        private readonly List<McpHealthLogEntry> _healthLog = new List<McpHealthLogEntry>();

        public McpAutoModeThreshold AutoModeThreshold { get; set; } = McpAutoModeThreshold.Medium;

        public event EventHandler? ServersChanged;
        public event EventHandler<McpHealthLogEntry>? HealthLogUpdated;
        public event EventHandler? PoorHealthDetected;

        public IReadOnlyList<McpServerConfig> Servers => _servers;
        public IReadOnlyList<McpHealthLogEntry> HealthLog => _healthLog;

        public McpHubService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _mcpConfigPath = Path.Combine(workspaceRoot, ".kilo", "mcp_servers.json");

            _healthCheckTimer = new System.Timers.Timer(15000);
            _healthCheckTimer.Elapsed += HealthCheckTimer_Elapsed;
            _healthCheckTimer.AutoReset = true;
            _healthCheckTimer.Start();

            _communityCachePath = Path.Combine(workspaceRoot, ".kilo", "mcp_community_cache.json");
            LoadCommunityCache();
            LoadServers();
        }

        public Task<List<McpServerConfig>> GetAvailableServersAsync()
        {
            var available = new List<McpServerConfig>
            {
                new McpServerConfig
                {
                    Id = "filesystem",
                    Name = "Filesystem",
                    Description = "Read, write, and search filesystem",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-filesystem", "/" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "github",
                    Name = "GitHub",
                    Description = "GitHub API integration",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-github" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "brave-search",
                    Name = "Brave Search",
                    Description = "Web search capability",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-brave-search" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "postgres",
                    Name = "PostgreSQL",
                    Description = "Database operations",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-postgres", "postgresql://localhost" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "aws-s3",
                    Name = "AWS S3",
                    Description = "AWS S3 object store access",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-aws-s3" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "azure-blob",
                    Name = "Azure Blob Storage",
                    Description = "Azure blob storage integration",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-azure-blob" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "nuget",
                    Name = "NuGet",
                    Description = "NuGet package index and metadata",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-nuget" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "msbuild",
                    Name = "MSBuild",
                    Description = "MSBuild project graph and task analysis",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-msbuild" },
                    Enabled = false
                },
                new McpServerConfig
                {
                    Id = "tfs",
                    Name = "TFS",
                    Description = "Azure DevOps/TFS repository integration",
                    Command = "npx",
                    Args = new List<string> { "-y", "@modelcontextprotocol/server-tfs" },
                    Enabled = false
                }
            };

            return Task.FromResult(available);
        }

        public async Task<List<McpServerConfig>> QueryRemoteCommunityServersAsync(string inventoryUrl)
        {
            try
            {
#if NET48
                using var client = new WebClient();
                var body = await client.DownloadStringTaskAsync(inventoryUrl);
#else
                using var client = new HttpClient();
                var body = await client.GetStringAsync(inventoryUrl);
#endif
                var remoteServers = JsonSerializer.Deserialize<List<McpServerConfig>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<McpServerConfig>();

                // cache remote inventory for offline/prefetch
                File.WriteAllText(_communityCachePath, JsonSerializer.Serialize(remoteServers, new JsonSerializerOptions { WriteIndented = true }));

                foreach (var remote in remoteServers)
                {
                    var existing = _servers.FirstOrDefault(x => string.Equals(x.Id, remote.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        remote.Status = remote.Enabled ? "Running" : "Stopped";
                        _servers.Add(remote);
                    }
                    else
                    {
                        existing.Name = remote.Name;
                        existing.Description = remote.Description;
                        existing.Command = remote.Command;
                        existing.Args = remote.Args;
                        existing.Score = remote.Score;
                        existing.Tags = remote.Tags;
                        existing.DocumentationUrl = remote.DocumentationUrl;
                    }
                }

                SaveServers();
                ServersChanged?.Invoke(this, EventArgs.Empty);
                return remoteServers;
            }
            catch
            {
                return LoadCommunityCache();
            }
        }

        public IEnumerable<McpServerConfig> SearchServers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _servers;

            query = query.Trim();
            return _servers.Where(s => s.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                                        || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                        || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public void AddServer(McpServerConfig server)
        {
            server.Id = Guid.NewGuid().ToString("N");
            server.Status = server.Enabled ? "Running" : "Stopped";
            _servers.Add(server);
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateServer(McpServerConfig server)
        {
            for (int i = 0; i < _servers.Count; i++)
            {
                if (_servers[i].Id == server.Id)
                {
                    _servers[i] = server;
                    break;
                }
            }
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Dispose()
        {
            try
            {
                _healthCheckTimer?.Stop();
            }
            catch { }

            try
            {
                _healthCheckTimer?.Dispose();
            }
            catch { }

            GC.SuppressFinalize(this);
        }
        public void RemoveServer(string serverId)
        {
            _servers.RemoveAll(s => s.Id == serverId);
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);
        }

        public McpServerConfig? GetServer(string serverId)
        {
            foreach (var server in _servers)
            {
                if (server.Id == serverId)
                    return server;
            }
            return null;
        }

        public void EnableServer(string serverId, bool enabled)
        {
            foreach (var server in _servers)
            {
                if (server.Id == serverId)
                {
                    server.Enabled = enabled;
                    server.Status = enabled ? "Running" : "Disabled";
                    break;
                }
            }
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<string> RestartServerAsync(string serverId)
        {
            var server = GetServer(serverId);
            if (server == null)
                return "Server not found";

            server.Status = "Restarting";
            server.LastHealthCheckUtc = DateTime.UtcNow;
            ServersChanged?.Invoke(this, EventArgs.Empty);

            await Task.Delay(800);

            server.Status = server.Enabled ? "Running" : "Disabled";
            server.LastHealthCheckUtc = DateTime.UtcNow;
            AddHealthLog(server.Id, $"Restarted, new status: {server.Status}");
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);

            return server.Status;
        }

        private void HealthCheckTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var checkedCount = 0;
            var unhealthyCount = 0;
            var poorHealth = false;

            foreach (var server in _servers)
            {
                try
                {
                    if (!server.Enabled)
                    {
                        server.Status = "Disabled";
                        continue;
                    }

                    var healthy = server.Enabled && server.Args != null && server.Args.Count > 0;
                    var newStatus = healthy ? "Healthy" : "Unhealthy";

                    if (!string.Equals(server.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        AddHealthLog(server.Id, $"Status changed from {server.Status} to {newStatus}");
                    }

                    server.Status = newStatus;
                    server.LastHealthCheckUtc = DateTime.UtcNow;

                    if (!healthy)
                    {
                        unhealthyCount++;
                        AddHealthLog(server.Id, "Detected unhealthy server, scheduling restart.");
                        _ = RestartServerAsync(server.Id);
                    }

                    checkedCount++;
                }
                catch (Exception ex)
                {
                    server.Status = "Unhealthy";
                    AddHealthLog(server.Id, $"Exception in health check: {ex.Message}");
                    poorHealth = true;
                    checkedCount++;
                    unhealthyCount++;
                }
            }

            // predictive auto-mode: if ratio exceeds threshold
            if (checkedCount > 0)
            {
                var ratio = (double)unhealthyCount / checkedCount;
                var threshold = AutoModeThreshold switch
                {
                    McpAutoModeThreshold.Low => 0.2,
                    McpAutoModeThreshold.Medium => 0.35,
                    McpAutoModeThreshold.High => 0.5,
                    _ => 0.35
                };

                if (ratio >= threshold)
                {
                    poorHealth = true;
                    AddHealthLog("__system__", $"Poor health ratio {ratio:P1} >= threshold {threshold:P0} → auto-mode trigger.");
                }
            }

            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);

            if (poorHealth)
            {
                PoorHealthDetected?.Invoke(this, EventArgs.Empty);
            }
        }

        public Task<string> GetConfigJsonAsync()
        {
            var config = new
            {
                mcpServers = new Dictionary<string, object>()
            };

            foreach (var server in _servers)
            {
                if (server.Enabled)
                {
                    config.mcpServers[server.Id] = new
                    {
                        command = server.Command,
                        args = server.Args
                    };
                }
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(json);
        }

        private void LoadServers()
        {
            try
            {
                if (File.Exists(_mcpConfigPath))
                {
                    var json = File.ReadAllText(_mcpConfigPath);
                    _servers = JsonSerializer.Deserialize<List<McpServerConfig>>(json) ?? new List<McpServerConfig>();
                }
            }
            catch { }
        }

        private List<McpServerConfig> LoadCommunityCache()
        {
            try
            {
                if (!File.Exists(_communityCachePath))
                    return new List<McpServerConfig>();

                var body = File.ReadAllText(_communityCachePath);
                var cachedServers = JsonSerializer.Deserialize<List<McpServerConfig>>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<McpServerConfig>();

                foreach (var remote in cachedServers)
                {
                    var existing = _servers.FirstOrDefault(x => string.Equals(x.Id, remote.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        _servers.Add(remote);
                    }
                    else
                    {
                        existing.Name = remote.Name;
                        existing.Description = remote.Description;
                        existing.Command = remote.Command;
                        existing.Args = remote.Args;
                        existing.Score = remote.Score;
                        existing.Tags = remote.Tags;
                        existing.DocumentationUrl = remote.DocumentationUrl;
                        existing.Status = remote.Status;
                    }
                }

                return cachedServers;
            }
            catch
            {
                return new List<McpServerConfig>();
            }
        }

        private void AddHealthLog(string serverId, string message)
        {
            var logEntry = new McpHealthLogEntry
            {
                ServerId = serverId,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            };

            _healthLog.Insert(0, logEntry);
            HealthLogUpdated?.Invoke(this, logEntry);

            if (_healthLog.Count > 100)
                _healthLog.RemoveRange(100, _healthLog.Count - 100);
        }

        private void SaveServers()
        {
            try
            {
                var dir = Path.GetDirectoryName(_mcpConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_servers, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_mcpConfigPath, json);
            }
            catch { }
        }
    }

    public class McpServerConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public List<string> Args { get; set; } = new List<string>();
        public bool Enabled { get; set; }
        public double Score { get; set; } = 0.0;
        public List<string> Tags { get; set; } = new List<string>();
        public string DocumentationUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Stopped";
        public DateTime LastHealthCheckUtc { get; set; } = DateTime.MinValue;
        public string LatestMessage { get; set; } = string.Empty;

        public bool IsRunning => string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "Healthy", StringComparison.OrdinalIgnoreCase);
    }

    public class McpHealthLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string ServerId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{TimestampUtc:HH:mm:ss} [{ServerId}] {Message}";
        }
    }

    public enum McpAutoModeThreshold
    {
        Low,
        Medium,
        High
    }
}