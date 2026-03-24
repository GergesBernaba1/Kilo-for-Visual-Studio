using System;
using System.Collections.Generic;
using System.Linq;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.App.Services
{
    public class ContextItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public long SizeBytes { get; set; }
    }

    public class ContextAccumulationService
    {
        private readonly List<ContextItem> _contextItems = new List<ContextItem>();
        private const long MaxContextSizeBytes = 500_000;

        public IReadOnlyList<ContextItem> ContextItems => _contextItems.AsReadOnly();

        public void AddFileContext(string filePath, string content)
        {
            var item = new ContextItem
            {
                Type = "file",
                Content = content,
                Source = filePath,
                SizeBytes = content?.Length ?? 0
            };
            AddContextItem(item);
        }

        public void AddSelectionContext(string filePath, string selectedText, int startLine, int endLine)
        {
            var item = new ContextItem
            {
                Type = "selection",
                Content = selectedText,
                Source = $"{filePath}:{startLine}-{endLine}",
                SizeBytes = selectedText?.Length ?? 0
            };
            AddContextItem(item);
        }

        public void AddTerminalOutput(string command, string output)
        {
            var item = new ContextItem
            {
                Type = "terminal",
                Content = $"Command: {command}\nOutput: {output}",
                Source = "terminal",
                SizeBytes = (command?.Length ?? 0) + (output?.Length ?? 0)
            };
            AddContextItem(item);
        }

        public void AddGitContext(string repoPath, string gitOutput)
        {
            var item = new ContextItem
            {
                Type = "git",
                Content = gitOutput,
                Source = repoPath,
                SizeBytes = gitOutput?.Length ?? 0
            };
            AddContextItem(item);
        }

        public void AddDiffContext(string filePath, string diff)
        {
            var item = new ContextItem
            {
                Type = "diff",
                Content = diff,
                Source = filePath,
                SizeBytes = diff?.Length ?? 0
            };
            AddContextItem(item);
        }

        public void ClearContext()
        {
            _contextItems.Clear();
        }

        public void RemoveContextItem(string id)
        {
            _contextItems.RemoveAll(x => x.Id == id);
        }

        public string BuildContextPayload()
        {
            var payload = new System.Text.StringBuilder();
            payload.AppendLine("## Context");
            payload.AppendLine();

            var fileContexts = _contextItems.Where(c => c.Type == "file").ToList();
            if (fileContexts.Any())
            {
                payload.AppendLine("### Files");
                foreach (var ctx in fileContexts)
                {
                    payload.AppendLine($"**{ctx.Source}**:");
                    payload.AppendLine("```");
                    var lines = ctx.Content.Split('\n');
                    var displayLines = lines.Length > 50 ? lines.Take(50).Concat(new[] { "..." }) : lines;
                    payload.AppendLine(string.Join("\n", displayLines));
                    payload.AppendLine("```");
                    payload.AppendLine();
                }
            }

            var selectionContexts = _contextItems.Where(c => c.Type == "selection").ToList();
            if (selectionContexts.Any())
            {
                payload.AppendLine("### Selections");
                foreach (var ctx in selectionContexts)
                {
                    payload.AppendLine($"**{ctx.Source}**:");
                    payload.AppendLine("```");
                    payload.AppendLine(ctx.Content);
                    payload.AppendLine("```");
                    payload.AppendLine();
                }
            }

            var terminalContexts = _contextItems.Where(c => c.Type == "terminal").ToList();
            if (terminalContexts.Any())
            {
                payload.AppendLine("### Terminal Output");
                foreach (var ctx in terminalContexts)
                {
                    payload.AppendLine($"```\n{ctx.Content}\n```");
                    payload.AppendLine();
                }
            }

            var gitContexts = _contextItems.Where(c => c.Type == "git").ToList();
            if (gitContexts.Any())
            {
                payload.AppendLine("### Git Context");
                foreach (var ctx in gitContexts)
                {
                    payload.AppendLine($"```\n{ctx.Content}\n```");
                    payload.AppendLine();
                }
            }

            return payload.ToString();
        }

        private void AddContextItem(ContextItem item)
        {
            _contextItems.Add(item);
            TrimContextIfNeeded();
        }

        private void TrimContextIfNeeded()
        {
            var totalSize = _contextItems.Sum(c => c.SizeBytes);
            while (totalSize > MaxContextSizeBytes && _contextItems.Count > 0)
            {
                var oldest = _contextItems[0];
                totalSize -= oldest.SizeBytes;
                _contextItems.RemoveAt(0);
            }
        }
    }
}