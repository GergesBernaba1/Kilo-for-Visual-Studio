using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.App.Services
{
    /// <summary>
    /// AI-powered autocomplete service with inline completion support.
    /// Provides real-time code suggestions as the user types.
    /// </summary>
    public class AutocompleteService
    {
        private readonly string _workspaceRoot;
        private readonly IKiloBackendClient? _backendClient;
        private readonly List<CompletionItem> _cachedCompletions = new List<CompletionItem>();
        private DateTimeOffset _lastIndexTime = DateTimeOffset.MinValue;
        private readonly SemaphoreSlim _completionGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _debounceCts;
        private const int DebounceDelayMs = 300;
        private const int MaxInlineCompletions = 3;

        // Configuration
        private bool _aiEnabled = true;
        private int _maxResults = 10;
        private bool _inlineCompletionEnabled = true;
        private int _contextLinesBefore = 10;
        private int _contextLinesAfter = 5;

        public event EventHandler<IList<CompletionItem>>? CompletionsReady;
        public event EventHandler<InlineCompletionResult>? InlineCompletionReady;

        public bool AiEnabled
        {
            get => _aiEnabled;
            set => _aiEnabled = value;
        }

        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = Math.Max(1, Math.Min(50, value));
        }

        public bool InlineCompletionEnabled
        {
            get => _inlineCompletionEnabled;
            set => _inlineCompletionEnabled = value;
        }

        public AutocompleteService(string workspaceRoot, IKiloBackendClient? backendClient = null)
        {
            _workspaceRoot = workspaceRoot;
            _backendClient = backendClient;
        }

        public async Task<IList<CompletionItem>> GetCompletionsAsync(string filePath, int line, int column, string prefix)
        {
            var completions = new List<CompletionItem>();

            // Add cached/project-based completions
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

            // Add AI-powered completions if backend is available
            if (_aiEnabled && _backendClient != null)
            {
                try
                {
                    var aiCompletions = await GetAICompletionsAsync(filePath, line, column, prefix);
                    completions.AddRange(aiCompletions);
                }
                catch
                {
                    // Fall back to cached completions if AI fails
                }
            }

            return completions.Take(_maxResults).ToList();
        }

        /// <summary>
        /// Gets AI-powered inline completion for the current cursor position.
        /// This provides real-time suggestions shown directly in the editor.
        /// </summary>
        public async Task<InlineCompletionResult?> GetInlineCompletionAsync(
            string filePath, 
            int line, 
            int column, 
            string textBeforeCursor,
            string textAfterCursor,
            CancellationToken cancellationToken = default)
        {
            if (!_aiEnabled || !_inlineCompletionEnabled || _backendClient == null)
                return null;

            // Cancel any pending debounced request
            _debounceCts?.Cancel();
            _debounceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Debounce to avoid too many requests
                await Task.Delay(DebounceDelayMs, _debounceCts.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            await _completionGate.WaitAsync(cancellationToken);
            try
            {
                // Get extended context for better suggestions
                var context = await GetExtendedContextAsync(filePath, line, column);
                if (string.IsNullOrEmpty(context.Content))
                    return null;

                // Build the completion prompt
                var prompt = BuildInlineCompletionPrompt(
                    context.Content,
                    context.CursorLine,
                    textBeforeCursor,
                    textAfterCursor,
                    GetLanguageId(filePath));

                // Request completion from AI
                var request = new AutocompleteRequest
                {
                    FilePath = filePath,
                    LanguageId = GetLanguageId(filePath),
                    Line = line,
                    Column = column,
                    Prefix = textBeforeCursor,
                    Context = prompt
                };

                var response = await _backendClient.SendGenericRequestAsync<AutocompleteRequest, AutocompleteResponse>(
                    "inline-completion", 
                    request);

                if (response?.Completions?.Any() == true)
                {
                    var topCompletion = response.Completions.First();
                    return new InlineCompletionResult
                    {
                        Text = topCompletion.InsertText ?? topCompletion.Label,
                        Range = new CompletionRange
                        {
                            StartLine = line,
                            StartColumn = column,
                            EndLine = line,
                            EndColumn = column + (topCompletion.InsertText?.Length ?? topCompletion.Label.Length)
                        },
                        Confidence = 0.85,
                        Source = "ai"
                    };
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore - request was cancelled
            }
            catch (Exception)
            {
                // Fall back gracefully
            }
            finally
            {
                _completionGate.Release();
            }

            return null;
        }

        /// <summary>
        /// Gets quick inline suggestions without waiting for AI (fallback pattern completion)
        /// </summary>
        public async Task<List<InlineCompletionResult>> GetQuickInlineSuggestionsAsync(
            string filePath,
            int line,
            int column,
            string currentLine)
        {
            var suggestions = new List<InlineCompletionResult>();
            var ext = Path.GetExtension(filePath).ToLower();
            
            // Get pattern-based completions
            var patterns = GetLanguagePatterns(ext);
            foreach (var pattern in patterns)
            {
                if (currentLine.TrimEnd().EndsWith(pattern.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(new InlineCompletionResult
                    {
                        Text = pattern.Completion,
                        Range = new CompletionRange
                        {
                            StartLine = line,
                            StartColumn = column - pattern.Prefix.Length,
                            EndLine = line,
                            EndColumn = column
                        },
                        Confidence = 0.7,
                        Source = "pattern"
                    });

                    if (suggestions.Count >= MaxInlineCompletions)
                        break;
                }
            }

            return await Task.FromResult(suggestions);
        }

        private async Task<ExtendedContext> GetExtendedContextAsync(string filePath, int line, int column)
        {
            var context = new ExtendedContext();

            try
            {
                if (!File.Exists(filePath))
                    return context;

                var allLines = await Task.Run(() => File.ReadAllLines(filePath));
                if (line < 0 || line >= allLines.Length)
                    return context;

                var startLine = Math.Max(0, line - _contextLinesBefore);
                var endLine = Math.Min(allLines.Length - 1, line + _contextLinesAfter);

                var sb = new StringBuilder();
                for (int i = startLine; i <= endLine; i++)
                {
                    var linePrefix = i == line ? ">>> " : "    ";
                    sb.AppendLine($"{linePrefix}{i + 1}: {allLines[i]}");
                }

                context.Content = sb.ToString();
                context.CursorLine = allLines[line];
                context.TotalLines = allLines.Length;
            }
            catch
            {
                // Return empty context on error
            }

            return context;
        }

        private string BuildInlineCompletionPrompt(string context, string cursorLine, string before, string after, string language)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Language: {language}");
            sb.AppendLine($"Current cursor position:");
            sb.AppendLine(cursorLine);
            sb.AppendLine($"Text before cursor: {before}");
            sb.AppendLine($"Text after cursor: {after}");
            sb.AppendLine();
            sb.AppendLine("Provide a completion for the line at >>>. Return only the completion text, no explanations.");
            return sb.ToString();
        }

        private List<PatternCompletion> GetLanguagePatterns(string extension)
        {
            return extension.ToLower() switch
            {
                ".cs" => new List<PatternCompletion>
                {
                    new("pub", "public "),
                    new("priv", "private "),
                    new("prot", "protected "),
                    new("int", "internal "),
                    new("static", "static "),
                    new("async", "async "),
                    new("foreach", "foreach (var item in collection) {\n    \n}"),
                    new("for", "for (int i = 0; i < length; i++) {\n    \n}"),
                    new("if", "if (condition) {\n    \n}"),
                    new("else", "else {\n    \n}"),
                    new("try", "try {\n    \n} catch (Exception ex) {\n    \n}"),
                    new("using", "using (var resource) {\n    \n}"),
                    new("new", "new "),
                    new("var", "var "),
                    new("this", "this."),
                    new("ctor", "public ClassName()\n{\n    \n}"),
                    new("prop", "public Type Property { get; set; }\n"),
                    new("propf", "public Type Property { get; private set; }\n"),
                    new("propg", "public Type Property { get; }\n"),
                },
                ".js" or ".ts" => new List<PatternCompletion>
                {
                    new("const", "const "),
                    new("let", "let "),
                    new("var", "var "),
                    new("function", "function name(params) {\n    \n}"),
                    new("arrow", "const name = (params) => {\n    \n};"),
                    new("async", "async function "),
                    new("await", "await "),
                    new("export", "export const "),
                    new("import", "import {  } from '';"),
                    new("class", "class  {\n    constructor() {\n        \n    }\n}"),
                    new("if", "if (condition) {\n    \n}"),
                    new("for", "for (let i = 0; i < length; i++) {\n    \n}"),
                    new("foreach", ".forEach(item => {\n    \n});"),
                    new("map", ".map(item => {\n    \n});"),
                    new("filter", ".filter(item => {\n    \n});"),
                },
                ".py" => new List<PatternCompletion>
                {
                    new("def", "def ():\n    \n"),
                    new("class", "class :\n    def __init__(self):\n        \n"),
                    new("async", "async def "),
                    new("for", "for  in :\n    \n"),
                    new("if", "if :\n    \n"),
                    new("elif", "elif :\n    \n"),
                    new("else", "else:\n    \n"),
                    new("try", "try:\n    \nexcept Exception as e:\n    \n"),
                    new("with", "with  as :\n    \n"),
                    new("lambda", "lambda x: "),
                    new("print", "print()"),
                },
                _ => new List<PatternCompletion>()
            };
        }

        private async Task<IList<CompletionItem>> GetAICompletionsAsync(string filePath, int line, int column, string prefix)
        {
            if (_backendClient == null)
                return Array.Empty<CompletionItem>();

            // Get context around the current position
            var context = GetContext(filePath, line, column);

            var request = new AutocompleteRequest
            {
                FilePath = filePath,
                LanguageId = GetLanguageId(filePath),
                Line = line,
                Column = column,
                Prefix = prefix,
                Context = context
            };

            try
            {
                var response = await _backendClient.SendGenericRequestAsync<AutocompleteRequest, AutocompleteResponse>(
                    "autocomplete", 
                    request);

                return response?.Completions?.ToList() ?? new List<CompletionItem>();
            }
            catch
            {
                return Array.Empty<CompletionItem>();
            }
        }

        private string GetContext(string filePath, int line, int column)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                if (line < 0 || line >= lines.Length)
                    return string.Empty;

                var startLine = Math.Max(0, line - 5);
                var endLine = Math.Min(lines.Length - 1, line + 5);
                var contextLines = lines.Skip(startLine).Take(endLine - startLine + 1);
                return string.Join(Environment.NewLine, contextLines);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetLanguageId(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            return ext switch
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
                ".html" => "html",
                ".css" => "css",
                _ => "plaintext"
            };
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

            CompletionsReady?.Invoke(this, _cachedCompletions);
        }

        private List<CompletionItem> ExtractSymbols(string content, string extension)
        {
            var symbols = new List<CompletionItem>();

            if (extension == ".cs")
            {
                var methodMatches = System.Text.RegularExpressions.Regex.Matches(
                    content, 
                    @"(?:public|private|protected|internal|static|async|virtual|override)?\s+
                      (?:void|string|int|bool|var|Task|List<[^>]+>|IEnumerable<[^>]+>|async\s+Task|\w+)\s+
                      (\w+)\s*\(");
                
                foreach (System.Text.RegularExpressions.Match match in methodMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Method,
                        Detail = "method"
                    });
                }

                var classMatches = System.Text.RegularExpressions.Regex.Matches(
                    content, 
                    @"(?:public|internal|private|abstract|sealed)\s+(?:class|interface|enum|struct|record)\s+(\w+)");
                
                foreach (System.Text.RegularExpressions.Match match in classMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Class,
                        Detail = "class"
                    });
                }

                var propMatches = System.Text.RegularExpressions.Regex.Matches(
                    content, 
                    @"(?:public|private|protected|internal)\s+(?:string|int|bool|var|\w+)\s+(\w+)\s*\{?");
                
                foreach (System.Text.RegularExpressions.Match match in propMatches)
                {
                    if (!match.Value.Contains("(")) // Not a method
                    {
                        symbols.Add(new CompletionItem
                        {
                            Label = match.Groups[1].Value,
                            Kind = CompletionKind.Property,
                            Detail = "property"
                        });
                    }
                }

                // Also extract fields
                var fieldMatches = System.Text.RegularExpressions.Regex.Matches(
                    content,
                    @"(?:private|public|protected)\s+\w+\s+(\w+)\s*=");
                
                foreach (System.Text.RegularExpressions.Match match in fieldMatches)
                {
                    symbols.Add(new CompletionItem
                    {
                        Label = match.Groups[1].Value,
                        Kind = CompletionKind.Variable,
                        Detail = "field"
                    });
                }
            }

            return symbols;
        }

        private bool ShouldSkipFile(string filePath)
        {
            var skipPaths = new[] { "bin", "obj", "node_modules", ".git", "packages", ".vs", "TestResults" };
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
                new CompletionItem { Label = "HACK", Kind = CompletionKind.Keyword, Detail = "HACK comment" },
                new CompletionItem { Label = "async", Kind = CompletionKind.Keyword, Detail = "async keyword" },
                new CompletionItem { Label = "await", Kind = CompletionKind.Keyword, Detail = "await keyword" },
                new CompletionItem { Label = "public", Kind = CompletionKind.Keyword, Detail = "public modifier" },
                new CompletionItem { Label = "private", Kind = CompletionKind.Keyword, Detail = "private modifier" },
                new CompletionItem { Label = "protected", Kind = CompletionKind.Keyword, Detail = "protected modifier" },
                new CompletionItem { Label = "internal", Kind = CompletionKind.Keyword, Detail = "internal modifier" },
                new CompletionItem { Label = "static", Kind = CompletionKind.Keyword, Detail = "static modifier" },
                new CompletionItem { Label = "readonly", Kind = CompletionKind.Keyword, Detail = "readonly modifier" },
                new CompletionItem { Label = "return", Kind = CompletionKind.Keyword, Detail = "return statement" },
                new CompletionItem { Label = "new", Kind = CompletionKind.Keyword, Detail = "new operator" },
                new CompletionItem { Label = "var", Kind = CompletionKind.Keyword, Detail = "var keyword" },
                new CompletionItem { Label = "null", Kind = CompletionKind.Keyword, Detail = "null literal" },
                new CompletionItem { Label = "true", Kind = CompletionKind.Keyword, Detail = "true literal" },
                new CompletionItem { Label = "false", Kind = CompletionKind.Keyword, Detail = "false literal" },
            };
            return common.Where(c => c.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void Dispose()
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _completionGate.Dispose();
        }
    }

    public class InlineCompletionResult
    {
        public string Text { get; set; } = string.Empty;
        public CompletionRange Range { get; set; } = new();
        public double Confidence { get; set; }
        public string Source { get; set; } = "pattern";
    }

    public class CompletionRange
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }

    public class ExtendedContext
    {
        public string Content { get; set; } = string.Empty;
        public string CursorLine { get; set; } = string.Empty;
        public int TotalLines { get; set; }
    }

    internal class PatternCompletion
    {
        public string Prefix { get; }
        public string Completion { get; }

        public PatternCompletion(string prefix, string completion)
        {
            Prefix = prefix;
            Completion = completion;
        }
    }
}
