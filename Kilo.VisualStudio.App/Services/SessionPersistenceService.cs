using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public class SessionPersistenceService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _sessionsDirectory;
        private readonly string _historyDirectory;

        public SessionPersistenceService(string workspaceRoot)
        {
            _sessionsDirectory = Path.Combine(workspaceRoot, ".kilo", "sessions");
            _historyDirectory = Path.Combine(workspaceRoot, ".kilo", "history");
            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_sessionsDirectory))
                Directory.CreateDirectory(_sessionsDirectory);
            if (!Directory.Exists(_historyDirectory))
                Directory.CreateDirectory(_historyDirectory);
        }

        public Task SaveSessionAsync(KiloSessionSummary session, IReadOnlyList<KiloSessionMessage> messages)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{session.SessionId}.json");
            var data = new SessionData
            {
                Session = session,
                Messages = messages.ToList()
            };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(sessionFile, json);
            return Task.CompletedTask;
        }

        public Task<KiloSessionSummary?> LoadSessionAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            if (!File.Exists(sessionFile))
                return Task.FromResult<KiloSessionSummary?>(null);

            try
            {
                var json = File.ReadAllText(sessionFile);
                var data = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                return Task.FromResult(data?.Session);
            }
            catch
            {
                return Task.FromResult<KiloSessionSummary?>(null);
            }
        }

        public Task<IReadOnlyList<KiloSessionMessage>> LoadSessionMessagesAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            if (!File.Exists(sessionFile))
                return Task.FromResult<IReadOnlyList<KiloSessionMessage>>(new List<KiloSessionMessage>());

            try
            {
                var json = File.ReadAllText(sessionFile);
                var data = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                var messages = data?.Messages;
                return Task.FromResult<IReadOnlyList<KiloSessionMessage>>(messages ?? new List<KiloSessionMessage>());
            }
            catch
            {
                return Task.FromResult<IReadOnlyList<KiloSessionMessage>>(new List<KiloSessionMessage>());
            }
        }

        public Task SavePromptHistoryAsync(IReadOnlyList<string> prompts)
        {
            var historyFile = Path.Combine(_historyDirectory, "prompts.json");
            var json = JsonSerializer.Serialize(prompts, JsonOptions);
            File.WriteAllText(historyFile, json);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> LoadPromptHistoryAsync()
        {
            var historyFile = Path.Combine(_historyDirectory, "prompts.json");
            if (!File.Exists(historyFile))
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());

            try
            {
                var json = File.ReadAllText(historyFile);
                var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                return Task.FromResult<IReadOnlyList<string>>(list ?? new List<string>());
            }
            catch
            {
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }
        }

        public Task DeleteSessionAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            if (File.Exists(sessionFile))
                File.Delete(sessionFile);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KiloSessionSummary>> ListSavedSessionsAsync()
        {
            var sessions = new List<KiloSessionSummary>();
            if (!Directory.Exists(_sessionsDirectory))
                return Task.FromResult<IReadOnlyList<KiloSessionSummary>>(sessions);

            foreach (var file in Directory.GetFiles(_sessionsDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                    if (data?.Session != null)
                        sessions.Add(data.Session);
                }
                catch { }
            }

            return Task.FromResult<IReadOnlyList<KiloSessionSummary>>(sessions.OrderByDescending(s => s.UpdatedAtUtc).ToList());
        }

        private class SessionData
        {
            public KiloSessionSummary? Session { get; set; }
            public List<KiloSessionMessage>? Messages { get; set; }
        }
    }
}