using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace Kilo.VisualStudio.App.Services
{
    public class CodeReviewService
    {
        private readonly string _workspaceRoot;

        public CodeReviewService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public Task<CodeReviewSession> CreateReviewSessionAsync(string branch, List<string> modifiedFiles, List<string> stagedFiles, List<string> untrackedFiles, string stagedDiff, string unstagedDiff)
        {
            var session = new CodeReviewSession
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = $"Review - {branch}",
                WorkspacePath = _workspaceRoot,
                Branch = branch,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                StagedChanges = stagedDiff,
                UnstagedChanges = unstagedDiff,
                ModifiedFiles = modifiedFiles,
                StagedFiles = stagedFiles,
                UntrackedFiles = untrackedFiles
            };

            return Task.FromResult(session);
        }

        public Task<string> GenerateReviewSummaryAsync(CodeReviewSession session)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Review the following code changes and provide a summary:");
            prompt.AppendLine();
            prompt.AppendLine("## Branch: " + session.Branch);
            prompt.AppendLine($"## Modified: {session.ModifiedFiles.Count} files");
            prompt.AppendLine($"## Staged: {session.StagedFiles.Count} files");
            prompt.AppendLine();
            
            if (!string.IsNullOrEmpty(session.StagedChanges))
            {
                prompt.AppendLine("### Staged Changes:");
                prompt.AppendLine(session.StagedChanges);
                prompt.AppendLine();
            }

            if (!string.IsNullOrEmpty(session.UnstagedChanges))
            {
                prompt.AppendLine("### Unstaged Changes:");
                prompt.AppendLine(session.UnstagedChanges);
            }

            return Task.FromResult(prompt.ToString());
        }

        public Task<List<CodeReviewComment>> GetInlineCommentsAsync(CodeReviewSession session)
        {
            var comments = new List<CodeReviewComment>();
            var diff = session.StagedChanges + "\n" + session.UnstagedChanges;
            
            var lines = diff.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("+") && !line.StartsWith("+++"))
                {
                    comments.Add(new CodeReviewComment
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        SessionId = session.Id,
                        LineNumber = i + 1,
                        Content = "Added line",
                        Status = ReviewCommentStatus.Pending
                    });
                }
                else if (line.StartsWith("-") && !line.StartsWith("---"))
                {
                    comments.Add(new CodeReviewComment
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        SessionId = session.Id,
                        LineNumber = i + 1,
                        Content = "Removed line",
                        Status = ReviewCommentStatus.Pending
                    });
                }
            }

            return Task.FromResult(comments);
        }

        public void SaveReviewSession(CodeReviewSession session)
        {
            var dir = Path.Combine(_workspaceRoot, ".kilo", "reviews");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"{session.Id}.json");
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(file, json);
        }

        public CodeReviewSession? LoadReviewSession(string sessionId)
        {
            var file = Path.Combine(_workspaceRoot, ".kilo", "reviews", $"{sessionId}.json");
            if (!File.Exists(file))
                return null;

            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<CodeReviewSession>(json);
        }

        public List<CodeReviewSession> GetAllReviews()
        {
            var reviews = new List<CodeReviewSession>();
            var dir = Path.Combine(_workspaceRoot, ".kilo", "reviews");
            
            if (!Directory.Exists(dir))
                return reviews;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<CodeReviewSession>(json);
                if (session != null)
                    reviews.Add(session);
            }

            return reviews;
        }
    }

    public class CodeReviewSession
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string WorkspacePath { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string StagedChanges { get; set; } = string.Empty;
        public string UnstagedChanges { get; set; } = string.Empty;
        public List<string> ModifiedFiles { get; set; } = new List<string>();
        public List<string> StagedFiles { get; set; } = new List<string>();
        public List<string> UntrackedFiles { get; set; } = new List<string>();
    }

    public class CodeReviewComment
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public ReviewCommentStatus Status { get; set; }
    }

    public enum ReviewCommentStatus
    {
        Pending,
        Resolved,
        Dismissed
    }
}