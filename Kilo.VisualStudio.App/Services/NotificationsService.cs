using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class NotificationsService
    {
        private readonly List<NotificationItem> _notificationHistory = new List<NotificationItem>();
        private const int MaxHistoryItems = 50;
        private bool _notificationsEnabled = true;
        private bool _soundEnabled = false;

        public event EventHandler<NotificationItem>? NotificationRaised;
        public event EventHandler<string>? SoundRequested;

        public bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set => _notificationsEnabled = value;
        }

        public bool SoundEnabled
        {
            get => _soundEnabled;
            set => _soundEnabled = value;
        }

        public void ShowInfo(string title, string message)
        {
            RaiseNotification(NotificationLevel.Info, title, message);
        }

        public void ShowSuccess(string title, string message)
        {
            RaiseNotification(NotificationLevel.Success, title, message);
        }

        public void ShowWarning(string title, string message)
        {
            RaiseNotification(NotificationLevel.Warning, title, message);
        }

        public void ShowError(string title, string message)
        {
            RaiseNotification(NotificationLevel.Error, title, message);
        }

        public void ShowToolExecuted(string toolName, string description)
        {
            RaiseNotification(NotificationLevel.Info, $"Tool: {toolName}", description);
        }

        public void ShowToolApproved(string toolName)
        {
            RaiseNotification(NotificationLevel.Info, "Tool Approved", $"{toolName} approved");
        }

        public void ShowToolDenied(string toolName)
        {
            RaiseNotification(NotificationLevel.Warning, "Tool Denied", $"{toolName} denied");
        }

        public void ShowSessionStarted()
        {
            RaiseNotification(NotificationLevel.Success, "Session Started", "Kilo session started");
        }

        public void ShowSessionEnded()
        {
            RaiseNotification(NotificationLevel.Info, "Session Ended", "Kilo session completed");
        }

        public void ShowConnectionStateChanged(string state, string message)
        {
            RaiseNotification(NotificationLevel.Info, $"Connection: {state}", message);
        }

        public IReadOnlyList<NotificationItem> GetRecentNotifications(int count = 10)
        {
            var result = new List<NotificationItem>();
            for (int i = 0; i < Math.Min(count, _notificationHistory.Count); i++)
            {
                result.Add(_notificationHistory[i]);
            }
            return result;
        }

        public void ClearHistory()
        {
            _notificationHistory.Clear();
        }

        private void RaiseNotification(NotificationLevel level, string title, string message)
        {
            if (!_notificationsEnabled)
                return;

            var notification = new NotificationItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Level = level,
                Title = title,
                Message = message,
                TimestampUtc = DateTimeOffset.UtcNow
            };

            _notificationHistory.Insert(0, notification);

            while (_notificationHistory.Count > MaxHistoryItems)
            {
                _notificationHistory.RemoveAt(_notificationHistory.Count - 1);
            }

            NotificationRaised?.Invoke(this, notification);

            if (_soundEnabled)
            {
                var soundKey = level switch
                {
                    NotificationLevel.Success => "success",
                    NotificationLevel.Warning => "warning",
                    NotificationLevel.Error => "error",
                    _ => "info"
                };
                SoundRequested?.Invoke(this, soundKey);
            }
        }
    }

    public enum NotificationLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationItem
    {
        public string Id { get; set; } = string.Empty;
        public NotificationLevel Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; set; }
    }
}