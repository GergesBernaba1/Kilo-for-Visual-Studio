using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class CollaborationService
    {
        private readonly string _workspaceRoot;
        private readonly string _shareFolder;

        public CollaborationService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
            _shareFolder = Path.Combine(_workspaceRoot, ".kilo", "shared_sessions");
            if (!Directory.Exists(_shareFolder))
                Directory.CreateDirectory(_shareFolder);
        }

        public string ShareSession(string sessionId, IEnumerable<string> conversationHistory)
        {
            var shareId = Guid.NewGuid().ToString("N");
            var payload = new SharedSessionPayload
            {
                ShareId = shareId,
                SessionId = sessionId ?? string.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ConversationHistory = conversationHistory == null ? new List<string>() : new List<string>(conversationHistory)
            };

            var filePath = Path.Combine(_shareFolder, $"{shareId}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return $"kilo-share://{shareId}";
        }

        public SharedSessionPayload? LoadSharedSession(string shareId)
        {
            if (string.IsNullOrWhiteSpace(shareId))
                return null;

            var filePath = Path.Combine(_shareFolder, shareId.EndsWith(".json") ? shareId : $"{shareId}.json");
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SharedSessionPayload>(json);
        }
    }

    public class SharedSessionPayload
    {
        public string ShareId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public List<string> ConversationHistory { get; set; } = new List<string>();
    }
}
