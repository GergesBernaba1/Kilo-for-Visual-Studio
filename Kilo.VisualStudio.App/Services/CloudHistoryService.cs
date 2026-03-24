using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public class CloudHistoryService
    {
        private readonly string _cloudHistoryPath;
        private readonly string _workspaceRoot;
        private CloudHistorySyncStatus _syncStatus = CloudHistorySyncStatus.NotSynced;
        private DateTimeOffset? _lastSyncUtc;

        public event EventHandler<CloudHistorySyncStatus>? SyncStatusChanged;
        public event EventHandler<IReadOnlyList<CloudSessionSummary>>? CloudHistoryLoaded;

        public CloudHistorySyncStatus SyncStatus => _syncStatus;
        public DateTimeOffset? LastSyncUtc => _lastSyncUtc;

        public CloudHistoryService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _cloudHistoryPath = Path.Combine(workspaceRoot, ".kilo", "cloud_history.json");
        }

        public Task SaveToCloudAsync(IReadOnlyList<CloudSessionSummary> sessions)
        {
            try
            {
                UpdateStatus(CloudHistorySyncStatus.Syncing);

                var dir = Path.GetDirectoryName(_cloudHistoryPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(_cloudHistoryPath, json);

                _lastSyncUtc = DateTimeOffset.UtcNow;
                UpdateStatus(CloudHistorySyncStatus.Synced);
            }
            catch
            {
                UpdateStatus(CloudHistorySyncStatus.Error);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CloudSessionSummary>> LoadFromCloudAsync()
        {
            try
            {
                if (!File.Exists(_cloudHistoryPath))
                    return Task.FromResult<IReadOnlyList<CloudSessionSummary>>(new List<CloudSessionSummary>());

                var json = File.ReadAllText(_cloudHistoryPath);
                var sessions = JsonSerializer.Deserialize<List<CloudSessionSummary>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                UpdateStatus(CloudHistorySyncStatus.Synced);
                CloudHistoryLoaded?.Invoke(this, sessions ?? new List<CloudSessionSummary>());

                return Task.FromResult<IReadOnlyList<CloudSessionSummary>>(sessions ?? new List<CloudSessionSummary>());
            }
            catch
            {
                UpdateStatus(CloudHistorySyncStatus.Error);
                return Task.FromResult<IReadOnlyList<CloudSessionSummary>>(new List<CloudSessionSummary>());
            }
        }

        public Task SyncToCloudAsync(IReadOnlyList<CloudSessionSummary> localSessions, IReadOnlyList<CloudSessionSummary> cloudSessions)
        {
            UpdateStatus(CloudHistorySyncStatus.Syncing);

            try
            {
                var merged = MergeSessionHistory(localSessions, cloudSessions);
                var json = JsonSerializer.Serialize(merged);

                File.WriteAllText(_cloudHistoryPath, json);
                _lastSyncUtc = DateTimeOffset.UtcNow;
                UpdateStatus(CloudHistorySyncStatus.Synced);
            }
            catch
            {
                UpdateStatus(CloudHistorySyncStatus.Error);
            }

            return Task.CompletedTask;
        }

        public void MarkAsPendingUpload()
        {
            UpdateStatus(CloudHistorySyncStatus.PendingUpload);
        }

        public bool HasPendingUploads()
        {
            return _syncStatus == CloudHistorySyncStatus.PendingUpload;
        }

        private void UpdateStatus(CloudHistorySyncStatus status)
        {
            _syncStatus = status;
            SyncStatusChanged?.Invoke(this, status);
        }

        private List<CloudSessionSummary> MergeSessionHistory(
            IReadOnlyList<CloudSessionSummary> local,
            IReadOnlyList<CloudSessionSummary> cloud)
        {
            var merged = new Dictionary<string, CloudSessionSummary>();

            foreach (var session in cloud)
            {
                merged[session.SessionId] = session;
            }

            foreach (var session in local)
            {
                if (merged.TryGetValue(session.SessionId, out var existing))
                {
                    if (session.UpdatedAtUtc > existing.UpdatedAtUtc)
                    {
                        merged[session.SessionId] = session;
                    }
                }
                else
                {
                    merged[session.SessionId] = session;
                }
            }

            var result = new List<CloudSessionSummary>(merged.Values);
            result.Sort((a, b) => b.UpdatedAtUtc.CompareTo(a.UpdatedAtUtc));

            return result;
        }
    }

    public enum CloudHistorySyncStatus
    {
        NotSynced,
        PendingUpload,
        Syncing,
        Synced,
        Error
    }

    public class CloudSessionSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string WorkspaceDirectory { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public int MessageCount { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
    }
}