using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;

namespace Kilo.VisualStudio.App.Services
{
    public class SemanticIndexService
    {
        private readonly string _workspaceRoot;
        private readonly string _indexPath;
        private Dictionary<string, IndexedDocument> _index = new Dictionary<string, IndexedDocument>();
        private bool _isIndexed = false;

        public event EventHandler? IndexUpdated;

        public SemanticIndexService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _indexPath = Path.Combine(workspaceRoot, ".kilo", "semantic_index.json");
            LoadIndex();
        }

        public bool IsIndexed => _isIndexed;

        public Task RebuildIndexAsync()
        {
            _index.Clear();

            var extensions = new[] { "*.cs", "*.js", "*.ts", "*.jsx", "*.tsx", "*.py", "*.java", "*.json", "*.xml", "*.config" };

            foreach (var ext in extensions)
            {
                foreach (var file in Directory.GetFiles(_workspaceRoot, ext, SearchOption.AllDirectories))
                {
                    if (ShouldSkipFile(file))
                        continue;

                    try
                    {
                        var content = File.ReadAllText(file);
                        var relativePath = GetRelativePath(file);

                        _index[relativePath] = new IndexedDocument
                        {
                            FilePath = relativePath,
                            Content = content,
                            Language = GetLanguageFromExtension(Path.GetExtension(file)),
                            LastModified = File.GetLastWriteTimeUtc(file),
                            Tokens = Tokenize(content)
                        };
                    }
                    catch { }
                }
            }

            SaveIndex();
            _isIndexed = true;
            IndexUpdated?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        public Task<List<SearchResult>> SearchAsync(string query, int maxResults = 10)
        {
            var results = new List<SearchResult>();
            var queryTokens = Tokenize(query.ToLower());

            foreach (var doc in _index.Values)
            {
                var score = CalculateRelevance(doc, queryTokens);
                if (score > 0)
                {
                    results.Add(new SearchResult
                    {
                        FilePath = doc.FilePath,
                        Score = score,
                        Preview = GetPreview(doc.Content, query),
                        Language = doc.Language
                    });
                }
            }

            var sortedResults = results.OrderByDescending(r => r.Score).Take(maxResults).ToList();
            return Task.FromResult(sortedResults);
        }

        public Task<string> GetSemanticContextAsync(string filePath, int lineNumber, int contextLines = 5)
        {
            if (!_index.TryGetValue(filePath, out var doc))
                return Task.FromResult(string.Empty);

            var lines = doc.Content.Split(new[] { '\n' }, StringSplitOptions.None);
            var start = Math.Max(0, lineNumber - contextLines);
            var end = Math.Min(lines.Length, lineNumber + contextLines);

            var context = new System.Text.StringBuilder();
            context.AppendLine($"// File: {filePath}");
            context.AppendLine($"// Context around line {lineNumber}");
            context.AppendLine();

            for (int i = start; i < end; i++)
            {
                var prefix = i == lineNumber ? ">>> " : "    ";
                context.AppendLine($"{prefix}{i + 1}: {lines[i]}");
            }

            return Task.FromResult(context.ToString());
        }

        private string GetRelativePath(string filePath)
        {
            if (filePath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return filePath.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            return filePath;
        }

        private List<string> Tokenize(string content)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var words = Regex.Split(content, @"[^\w]+");
            foreach (var word in words)
            {
                if (word.Length >= 2)
                    tokens.Add(word.ToLower());
            }
            return tokens.ToList();
        }

        private double CalculateRelevance(IndexedDocument doc, List<string> queryTokens)
        {
            double score = 0;

            foreach (var token in queryTokens)
            {
                if (doc.Tokens.Contains(token))
                    score += 1.0;

                if (doc.FilePath.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 0.5;
            }

            return score;
        }

        private string GetPreview(string content, string query)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 200)
                        trimmed = trimmed.Substring(0, 200) + "...";
                    return trimmed;
                }
            }
            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        private bool ShouldSkipFile(string filePath)
        {
            var skipPaths = new[] { "bin", "obj", "node_modules", ".git", "packages", ".vs" };
            foreach (var skip in skipPaths)
            {
                if (filePath.IndexOf(Path.DirectorySeparatorChar + skip + Path.DirectorySeparatorChar) >= 0)
                    return true;
            }
            return false;
        }

        private string GetLanguageFromExtension(string ext)
        {
            return ext.ToLower() switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".jsx" => "javascript",
                ".tsx" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".json" => "json",
                ".xml" => "xml",
                ".config" => "xml",
                _ => "text"
            };
        }

        private void LoadIndex()
        {
            try
            {
                if (File.Exists(_indexPath))
                {
                    var json = File.ReadAllText(_indexPath);
                    _index = JsonSerializer.Deserialize<Dictionary<string, IndexedDocument>>(json) ?? new Dictionary<string, IndexedDocument>();
                    _isIndexed = _index.Count > 0;
                }
            }
            catch { }
        }

        private void SaveIndex()
        {
            try
            {
                var dir = Path.GetDirectoryName(_indexPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_index, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_indexPath, json);
            }
            catch { }
        }
    }

    public class IndexedDocument
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public List<string> Tokens { get; set; } = new List<string>();
    }

    public class SearchResult
    {
        public string FilePath { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Preview { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }
}