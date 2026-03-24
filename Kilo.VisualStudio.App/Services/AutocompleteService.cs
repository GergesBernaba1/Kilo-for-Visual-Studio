using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class AutocompleteService
    {
        private readonly string _workspaceRoot;
        private readonly List<CompletionItem> _cachedCompletions = new List<CompletionItem>();
        private DateTimeOffset _lastIndexTime = DateTimeOffset.MinValue;

        public event EventHandler<IList<CompletionItem>>? CompletionsReady;

        public AutocompleteService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public Task<IList<CompletionItem>> GetCompletionsAsync(string filePath, int line, int column, string prefix)
        {
            var completions = new List<CompletionItem>();

            foreach (var completion in _cachedCompletions)
            {
                if (completion.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add(completion);
                }
            }

            if (completions.Count == 0)
            {
                completions.AddRange(GetCommonCompletions(prefix));
            }

            return Task.FromResult<IList<CompletionItem>>(completions);
        }

        public Task RefreshIndexAsync()
        {
            _cachedCompletions.Clear();
            IndexProjectFiles();
            _lastIndexTime = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        private void IndexProjectFiles()
        {
            var extensions = new[] { "*.cs", "*.js", "*.ts", "*.jsx", "*.tsx", "*.py", "*.java" };

            foreach (var ext in extensions)
            {
                foreach (var file in Directory.GetFiles(_workspaceRoot, ext, SearchOption.AllDirectories))
                {
                    if (ShouldSkipFile(file))
                        continue;

                    try
                    {
                        var content = File.ReadAllText(file);
                        var symbols = ExtractSymbols(content, Path.GetExtension(file));
                        _cachedCompletions.AddRange(symbols);
                    }
                    catch { }
                }
            }
        }

        private List<CompletionItem> ExtractSymbols(string content, string extension)
        {
            var symbols = new List<CompletionItem>();

            if (extension == ".cs")
            {
                var methodMatches = System.Text.RegularExpressions.Regex.Matches(content, @"(?:public|private|protected|internal|static|async)?\s+(?:void|string|int|bool|var|Task|List<[^>]+>|IEnumerable<[^>]+>)\s+(\w+)\s*\(");
                foreach (System.Text.RegularExpressions.Match match in methodMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Method,
                        Detail = "method"
                    });
                }

                var classMatches = System.Text.RegularExpressions.Regex.Matches(content, @"(?:public|internal)\s+(?:class|interface|enum|struct)\s+(\w+)");
                foreach (System.Text.RegularExpressions.Match match in classMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Class,
                        Detail = "class"
                    });
                }

                var propMatches = System.Text.RegularExpressions.Regex.Matches(content, @"(?:public|private|protected)\s+(?:string|int|bool|var)\s+(\w+)\s*\{");
                foreach (System.Text.RegularExpressions.Match match in propMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Property,
                        Detail = "property"
                    });
                }
            }

            return symbols;
        }

        private bool ShouldSkipFile(string filePath)
        {
            var skipPaths = new[] { "bin", "obj", "node_modules", ".git", "packages" };
            foreach (var skip in skipPaths)
            {
                if (filePath.Contains(Path.DirectorySeparatorChar + skip + Path.DirectorySeparatorChar))
                    return true;
            }
            return false;
        }

        private List<CompletionItem> GetCommonCompletions(string prefix)
        {
            var common = new List<CompletionItem>
            {
                new CompletionItem { Label = "TODO", Kind = CompletionKind.Keyword, Detail = "TODO comment" },
                new CompletionItem { Label = "FIXME", Kind = CompletionKind.Keyword, Detail = "FIXME comment" },
                new CompletionItem { Label = "async", Kind = CompletionKind.Keyword, Detail = "async keyword" },
                new CompletionItem { Label = "await", Kind = CompletionKind.Keyword, Detail = "await keyword" },
                new CompletionItem { Label = "public", Kind = CompletionKind.Keyword, Detail = "public modifier" },
                new CompletionItem { Label = "private", Kind = CompletionKind.Keyword, Detail = "private modifier" },
                new CompletionItem { Label = "protected", Kind = CompletionKind.Keyword, Detail = "protected modifier" },
                new CompletionItem { Label = "return", Kind = CompletionKind.Keyword, Detail = "return statement" },
            };
            return common.Where(c => c.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public class CompletionItem
    {
        public string Label { get; set; } = string.Empty;
        public CompletionKind Kind { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string InsertText { get; set; } = string.Empty;
    }

    public enum CompletionKind
    {
        Method,
        Property,
        Class,
        Interface,
        Keyword,
        Variable,
        Snippet
    }
}