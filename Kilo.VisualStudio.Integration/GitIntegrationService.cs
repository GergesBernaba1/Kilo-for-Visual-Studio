using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.Integration
{
    public class GitContext
    {
        public string RepositoryPath { get; set; } = string.Empty;
        public string CurrentBranch { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> ModifiedFiles { get; set; } = new List<string>();
        public List<string> StagedFiles { get; set; } = new List<string>();
        public List<string> UntrackedFiles { get; set; } = new List<string>();
        public string LastCommitMessage { get; set; } = string.Empty;
        public string LastCommitHash { get; set; } = string.Empty;
    }

    public class GitIntegrationService
    {
        private readonly string _workspaceRoot;

        public GitIntegrationService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public async Task<GitContext> GetGitContextAsync()
        {
            var context = new GitContext
            {
                RepositoryPath = _workspaceRoot
            };

            try
            {
                var gitDir = Path.Combine(_workspaceRoot, ".git");
                if (!Directory.Exists(gitDir) && !File.Exists(Path.Combine(_workspaceRoot, ".git")))
                {
                    return context;
                }

                context.CurrentBranch = await RunGitCommandAsync("rev-parse --abbrev-ref HEAD");
                context.Status = await RunGitCommandAsync("status --porcelain");
                context.ModifiedFiles = await GetModifiedFilesAsync();
                context.StagedFiles = await GetStagedFilesAsync();
                context.UntrackedFiles = await GetUntrackedFilesAsync();
                context.LastCommitHash = await RunGitCommandAsync("rev-parse HEAD");
                context.LastCommitMessage = await RunGitCommandAsync("log -1 --format=%B");
            }
            catch { }

            return context;
        }

        public async Task<string> GetDiffAsync(string filePath = "")
        {
            var args = string.IsNullOrEmpty(filePath) ? "diff" : $"diff -- \"{filePath}\"";
            return await RunGitCommandAsync(args);
        }

        public async Task<string> GetStagedDiffAsync()
        {
            return await RunGitCommandAsync("diff --cached");
        }

        public async Task<string> GetCommitMessageSuggestionAsync()
        {
            var diff = await GetDiffAsync();
            var staged = await GetStagedDiffAsync();
            var combined = $"{staged}\n\n{diff}";

            return $"Analyze the following changes and suggest a concise commit message:\n\n{combined}";
        }

        public async Task<string> GenerateCommitMessageAsync()
        {
            var diff = await GetDiffAsync();
            var staged = await GetStagedDiffAsync();

            var prompt = new StringBuilder();
            prompt.AppendLine("Generate a concise git commit message for the following changes. Use conventional commit format (type: description).");
            prompt.AppendLine();
            prompt.AppendLine("Changed files:");
            foreach (var file in await GetModifiedFilesAsync())
            {
                prompt.AppendLine($"  - {file}");
            }
            prompt.AppendLine();
            if (!string.IsNullOrEmpty(staged))
            {
                prompt.AppendLine("Staged changes:");
                prompt.AppendLine(staged);
            }

            return prompt.ToString();
        }

        public async Task<List<string>> GetChangedFilesSinceCommitAsync(string commitHash)
        {
            var result = await RunGitCommandAsync($"diff --name-only {commitHash} HEAD");
            var files = new List<string>();
            var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    files.Add(trimmed);
            }
            return files;
        }

        public async Task<string> GetFileLogAsync(string filePath, int maxCount = 10)
        {
            return await RunGitCommandAsync($"log --oneline -n {maxCount} -- \"{filePath}\"");
        }

        public async Task<string> ShowFileAtCommitAsync(string filePath, string commitHash)
        {
            return await RunGitCommandAsync($"show {commitHash}:\"{filePath}\"");
        }

        private async Task<string> RunGitCommandAsync(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = _workspaceRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return string.Empty;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(30000);

                return output.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<List<string>> GetModifiedFilesAsync()
        {
            var result = await RunGitCommandAsync("diff --name-only");
            return ParseFileList(result);
        }

        private async Task<List<string>> GetStagedFilesAsync()
        {
            var result = await RunGitCommandAsync("diff --cached --name-only");
            return ParseFileList(result);
        }

        private async Task<List<string>> GetUntrackedFilesAsync()
        {
            var result = await RunGitCommandAsync("ls-files --others --exclude-standard");
            return ParseFileList(result);
        }

        private List<string> ParseFileList(string output)
        {
            var files = new List<string>();
            var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    files.Add(trimmed);
            }
            return files;
        }
    }
}