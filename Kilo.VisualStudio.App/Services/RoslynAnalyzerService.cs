using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class RoslynAnalyzerService
    {
        private readonly string _workspaceRoot;

        public RoslynAnalyzerService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot ?? throw new ArgumentNullException(nameof(workspaceRoot));
        }

        public async Task<string> AnalyzeFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "File not found.";

            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (extension != ".cs")
                return "Roslyn analyzer currently supports only C# source files (stub mode).";

            var code = File.ReadAllText(filePath);
            var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var issue = new StringBuilder();

            issue.AppendLine($"Roslyn-style analysis report for {Path.GetFileName(filePath)}:");
            issue.AppendLine($"Total lines: {lines.Length}");
            issue.AppendLine($"TODO/FIXME markers: {lines.Count(l => l.Contains("TODO", StringComparison.OrdinalIgnoreCase) || l.Contains("FIXME", StringComparison.OrdinalIgnoreCase))}");
            issue.AppendLine($"Public methods (heuristic): {lines.Count(l => l.TrimStart().StartsWith("public "))}");

            if (code.Contains("async", StringComparison.OrdinalIgnoreCase) && !code.Contains("await", StringComparison.OrdinalIgnoreCase))
                issue.AppendLine("- Async methods without await detected; could be Task-returning patterns.");

            if (code.Contains("throw new Exception", StringComparison.OrdinalIgnoreCase))
                issue.AppendLine("- Generic exception is thrown; prefer specific exception types.");

            issue.AppendLine("\nNote: This is a lightweight, local heuristic analysis as Roslyn packages are not available in this build.");
            return issue.ToString();
        }
    }
}
