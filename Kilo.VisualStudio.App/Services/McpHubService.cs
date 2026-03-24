using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class McpHubService
    {
        private readonly string _workspaceRoot;
        private readonly string _mcpConfigPath;
        private List<McpServerConfig> _servers = new List<McpServerConfig>();

        public event EventHandler? ServersChanged;

        public IReadOnlyList<McpServerConfig> Servers => _servers;

        public McpHubService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _mcpConfigPath = Path.Combine(workspaceRoot, ".kilo", "mcp_servers.json");
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
                }
            };

            return Task.FromResult(available);
        }

        public void AddServer(McpServerConfig server)
        {
            server.Id = Guid.NewGuid().ToString("N");
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
                    break;
                }
            }
            SaveServers();
            ServersChanged?.Invoke(this, EventArgs.Empty);
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
    }
}