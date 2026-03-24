using System;
using System.Collections.Generic;

namespace Kilo.VisualStudio.App.Services
{
    public class PromptHistoryService
    {
        private readonly List<PromptHistoryItem> _history = new List<PromptHistoryItem>();
        private const int MaxHistoryItems = 100;

        public IReadOnlyList<PromptHistoryItem> History => _history.AsReadOnly();

        public void AddPrompt(string prompt, string sessionId)
        {
            var item = new PromptHistoryItem
            {
                Prompt = prompt,
                SessionId = sessionId,
                TimestampUtc = DateTimeOffset.UtcNow
            };

            _history.Insert(0, item);

            while (_history.Count > MaxHistoryItems)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        public IEnumerable<string> GetRecentPrompts(int count = 10)
        {
            var result = new List<string>();
            for (int i = 0; i < Math.Min(count, _history.Count); i++)
            {
                if (!string.IsNullOrWhiteSpace(_history[i].Prompt))
                {
                    result.Add(_history[i].Prompt);
                }
            }
            return result;
        }

        public IEnumerable<string> SearchPrompts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetRecentPrompts();

            query = query.ToLowerInvariant();
            var matches = _history.FindAll(x => x.Prompt.ToLowerInvariant().Contains(query));
            var results = new List<string>();
            foreach (var item in matches)
            {
                results.Add(item.Prompt);
            }
            return results;
        }

        public void ClearHistory()
        {
            _history.Clear();
        }

        public void LoadHistory(IReadOnlyList<PromptHistoryItem> items)
        {
            _history.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    _history.Add(item);
                }
            }
        }
    }

    public class PromptHistoryItem
    {
        public string Prompt { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}