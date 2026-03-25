using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;

namespace Kilo.VisualStudio.App.Services
{
    public class SemanticIndexService
    {
        private readonly string _workspaceRoot;
        private readonly string _indexPath;
        private readonly string _embeddingsCachePath;
        private Dictionary<string, IndexedDocument> _index = new Dictionary<string, IndexedDocument>();
        private Dictionary<string, float[]> _embeddingsCache = new Dictionary<string, float[]>();
        private bool _isIndexed = false;
        private bool _useEmbeddings = false;
        private string? _embeddingApiEndpoint;
        private string? _embeddingApiKey;
        private int _embeddingDimension = 1536; // Default for OpenAI text-embedding-ada-002

        public event EventHandler? IndexUpdated;
        public event EventHandler<string>? IndexingError;

        public bool IsIndexed => _isIndexed;
        public bool UsesEmbeddings => _useEmbeddings;

        public SemanticIndexService(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
            _indexPath = Path.Combine(workspaceRoot, ".kilo", "semantic_index.json");
            _embeddingsCachePath = Path.Combine(workspaceRoot, ".kilo", "embeddings_cache.json");
            LoadIndex();
            LoadEmbeddingsCache();
        }

        public void ConfigureEmbeddings(string apiEndpoint, string apiKey, int dimension = 1536)
        {
            _embeddingApiEndpoint = apiEndpoint;
            _embeddingApiKey = apiKey;
            _embeddingDimension = dimension;
            _useEmbeddings = !string.IsNullOrEmpty(apiEndpoint);
        }

        public async Task RebuildIndexAsync(IProgress<string>? progress = null)
        {
            _index.Clear();
            _embeddingsCache.Clear();

            var extensions = new[] { "*.cs", "*.js", "*.ts", "*.jsx", "*.tsx", "*.py", "*.java", "*.json", "*.xml", "*.config", "*.md", "*.txt" };
            var totalFiles = 0;

            // First pass: count files
            foreach (var ext in extensions)
            {
                totalFiles += Directory.GetFiles(_workspaceRoot, ext, SearchOption.AllDirectories)
                    .Count(f => !ShouldSkipFile(f));
            }

            var processedFiles = 0;

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

                        // Create document with enhanced tokenization
                        var tokens = EnhancedTokenize(content);
                        var bigrams = ExtractBigrams(content);
                        var functionSignatures = ExtractFunctionSignatures(content, Path.GetExtension(file));

                        _index[relativePath] = new IndexedDocument
                        {
                            FilePath = relativePath,
                            Content = content,
                            Language = GetLanguageFromExtension(Path.GetExtension(file)),
                            LastModified = File.GetLastWriteTimeUtc(file),
                            Tokens = tokens,
                            Bigrams = bigrams,
                            FunctionSignatures = functionSignatures,
                            // Pre-compute TF-IDF vectors
                            TermFrequencies = ComputeTermFrequencies(tokens)
                        };

                        processedFiles++;
                        progress?.Report($"Indexed {processedFiles}/{totalFiles}: {relativePath}");

                        // Generate embeddings if configured
                        if (_useEmbeddings)
                        {
                            await GenerateEmbeddingAsync(relativePath, content);
                        }
                    }
                    catch (Exception ex)
                    {
                        IndexingError?.Invoke(this, $"Error indexing {file}: {ex.Message}");
                    }
                }
            }

            // Compute IDF scores for all terms
            ComputeIdfScores();

            SaveIndex();
            SaveEmbeddingsCache();
            _isIndexed = true;
            IndexUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 10, bool useSemanticSearch = true)
        {
            var results = new List<SearchResult>();

            if (useSemanticSearch && _useEmbeddings)
            {
                // Use embedding-based semantic search
                results = await SemanticSearchAsync(query, maxResults);
            }
            else
            {
                // Use enhanced keyword search with TF-IDF
                results = EnhancedKeywordSearch(query, maxResults);
            }

            return results;
        }

        private async Task<List<SearchResult>> SemanticSearchAsync(string query, int maxResults)
        {
            var results = new List<SearchResult>();
            
            try
            {
                var queryEmbedding = await GetEmbeddingAsync(query);
                if (queryEmbedding == null)
                {
                    return EnhancedKeywordSearch(query, maxResults);
                }

                foreach (var doc in _index.Values)
                {
                    if (_embeddingsCache.TryGetValue(doc.FilePath, out var docEmbedding))
                    {
                        var similarity = CosineSimilarity(queryEmbedding, docEmbedding);
                        if (similarity > 0.3) // Threshold for relevance
                        {
                            results.Add(new SearchResult
                            {
                                FilePath = doc.FilePath,
                                Score = similarity,
                                Preview = GetPreview(doc.Content, query),
                                Language = doc.Language,
                                MatchType = "semantic"
                            });
                        }
                    }
                }
            }
            catch
            {
                // Fallback to keyword search on error
                return EnhancedKeywordSearch(query, maxResults);
            }

            return results.OrderByDescending(r => r.Score).Take(maxResults).ToList();
        }

        private List<SearchResult> EnhancedKeywordSearch(string query, int maxResults)
        {
            var results = new List<SearchResult>();
            var queryTokens = EnhancedTokenize(query.ToLower());
            var queryBigrams = ExtractBigrams(query.ToLower());

            foreach (var doc in _index.Values)
            {
                var score = CalculateTfIdfScore(doc, queryTokens);
                
                // Bonus for bigram matches
                foreach (var bigram in queryBigrams)
                {
                    if (doc.Bigrams.Contains(bigram))
                        score += 0.3;
                }

                // Bonus for function signature matches
                foreach (var sig in doc.FunctionSignatures)
                {
                    if (sig.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 0.5;
                }

                // Fuzzy matching bonus
                score += CalculateFuzzyScore(doc, query);

                if (score > 0)
                {
                    results.Add(new SearchResult
                    {
                        FilePath = doc.FilePath,
                        Score = score,
                        Preview = GetPreview(doc.Content, query),
                        Language = doc.Language,
                        MatchType = "keyword"
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).Take(maxResults).ToList();
        }

        private Dictionary<string, double> _idfScores = new Dictionary<string, double>();

        private void ComputeIdfScores()
        {
            var totalDocs = _index.Count;
            var allTerms = new HashSet<string>();
            foreach (var doc in _index.Values)
            {
                foreach (var token in doc.Tokens)
                {
                    allTerms.Add(token);
                }
            }

            foreach (var term in allTerms)
            {
                var docsWithTerm = _index.Values.Count(d => d.Tokens.Contains(term));
                _idfScores[term] = Math.Log((double)totalDocs / (1 + docsWithTerm));
            }
        }

        private double CalculateTfIdfScore(IndexedDocument doc, List<string> queryTokens)
        {
            double score = 0;
            foreach (var token in queryTokens)
            {
                if (doc.Tokens.Contains(token))
                {
                    var tf = doc.TermFrequencies.GetValueOrDefault(token, 0);
                    var idf = _idfScores.GetValueOrDefault(token, 0);
                    score += tf * idf;
                }

                // Path match bonus
                if (doc.FilePath.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 0.5;
            }
            return score;
        }

        private Dictionary<string, double> ComputeTermFrequencies(List<string> tokens)
        {
            var tf = new Dictionary<string, double>();
            if (tokens.Count == 0) return tf;

            foreach (var token in tokens)
            {
                tf[token] = tf.GetValueOrDefault(token, 0) + 1;
            }

            // Normalize by total token count
            foreach (var key in tf.Keys.ToList())
            {
                tf[key] = tf[key] / tokens.Count;
            }

            return tf;
        }

        private double CalculateFuzzyScore(IndexedDocument doc, string query)
        {
            double score = 0;
            var queryLower = query.ToLower();

            // Check for fuzzy matches in content (Levenshtein distance)
            foreach (var line in doc.Content.Split('\n').Take(100))
            {
                var lineLower = line.ToLower();
                if (lineLower.Contains(queryLower))
                    continue;

                // Simple fuzzy check: if query is within 2 edits of a word in the line
                var words = Regex.Split(lineLower, @"[^\w]+");
                foreach (var word in words)
                {
                    if (word.Length >= 3 && queryLower.Length >= 3)
                    {
                        var distance = LevenshteinDistance(word, queryLower);
                        if (distance <= 2 && distance > 0)
                        {
                            score += 0.1 * (1.0 - (double)distance / Math.Max(word.Length, queryLower.Length));
                        }
                    }
                }
            }

            return score;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var m = s1.Length;
            var n = s2.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++) dp[i, 0] = i;
            for (int j = 0; j <= n; j++) dp[0, j] = j;

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                        dp[i, j] = dp[i - 1, j - 1];
                    else
                        dp[i, j] = 1 + Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));
                }
            }

            return dp[m, n];
        }

        private async Task GenerateEmbeddingAsync(string filePath, string content)
        {
            try
            {
                // Truncate content if too long (embedding models have token limits)
                var truncated = content.Length > 8000 ? content.Substring(0, 8000) : content;
                var embedding = await GetEmbeddingAsync(truncated);
                if (embedding != null)
                {
                    _embeddingsCache[filePath] = embedding;
                }
            }
            catch { }
        }

        private async Task<float[]?> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrEmpty(_embeddingApiEndpoint))
                return null;

            try
            {
                using var httpClient = new HttpClient();
                if (!string.IsNullOrEmpty(_embeddingApiKey))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _embeddingApiKey);
                }

                var request = new
                {
                    input = text,
                    model = "text-embedding-ada-002"
                };

                var json = JsonSerializer.Serialize(request);
                var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(_embeddingApiEndpoint, httpContent);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);
                    return embeddingResponse?.data?.FirstOrDefault()?.embedding;
                }
            }
            catch { }

            return null;
        }

        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;

            var dotProduct = 0.0;
            var normA = 0.0;
            var normB = 0.0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return (float)(dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        private List<string> EnhancedTokenize(string content)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var words = Regex.Split(content, @"[^\w.#]+");
            
            foreach (var word in words)
            {
                if (word.Length >= 2)
                {
                    // Add the word itself
                    tokens.Add(word.ToLower());
                    
                    // Add stemming (simple suffix removal)
                    if (word.EndsWith("ing"))
                        tokens.Add(word.Substring(0, word.Length - 3).ToLower());
                    if (word.EndsWith("ed"))
                        tokens.Add(word.Substring(0, word.Length - 2).ToLower());
                    if (word.EndsWith("s") && word.Length > 2)
                        tokens.Add(word.Substring(0, word.Length - 1).ToLower());
                }
            }
            
            return tokens.ToList();
        }

        private List<string> ExtractBigrams(string content)
        {
            var bigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var words = Regex.Split(content.ToLower(), @"[^\w]+");
            
            for (int i = 0; i < words.Length - 1; i++)
            {
                if (words[i].Length >= 2 && words[i + 1].Length >= 2)
                {
                    bigrams.Add($"{words[i]} {words[i + 1]}");
                }
            }
            
            return bigrams.ToList();
        }

        private List<string> ExtractFunctionSignatures(string content, string extension)
        {
            var signatures = new List<string>();
            
            var patterns = new Dictionary<string, string[]>
            {
                {".cs", new[] { @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:\w+)\s+(\w+)\s*\(", @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:\w+)\s+(\w+)\s*<" }},
                {".js", new[] { @"(?:function\s+(\w+)|(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\(|(\w+)\s*:\s*(?:async\s*)?\(" }},
                {".ts", new[] { @"(?:function\s+(\w+)|(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\(|(\w+)\s*:\s*(?:async\s*)?\(" }},
                {".py", new[] { @"(?:def\s+(\w+)|class\s+(\w+))" }}
            };

            if (patterns.TryGetValue(extension, out var regexPatterns))
            {
                foreach (var pattern in regexPatterns)
                {
                    var matches = Regex.Matches(content, pattern);
                    foreach (Match match in matches)
                    {
                        for (int i = 1; i < match.Groups.Count; i++)
                        {
                            if (match.Groups[i].Success)
                            {
                                signatures.Add(match.Groups[i].Value);
                            }
                        }
                    }
                }
            }
            
            return signatures;
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

        private bool ShouldSkipFile(string filePath)
        {
            var skipPaths = new[] { "bin", "obj", "node_modules", ".git", "packages", ".vs", ".kilo", "TestResults" };
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
                ".md" => "markdown",
                ".txt" => "text",
                _ => "text"
            };
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

        private void LoadEmbeddingsCache()
        {
            try
            {
                if (File.Exists(_embeddingsCachePath))
                {
                    var json = File.ReadAllText(_embeddingsCachePath);
                    _embeddingsCache = JsonSerializer.Deserialize<Dictionary<string, float[]>>(json) ?? new Dictionary<string, float[]>();
                }
            }
            catch { }
        }

        private void SaveEmbeddingsCache()
        {
            try
            {
                var dir = Path.GetDirectoryName(_embeddingsCachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_embeddingsCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_embeddingsCachePath, json);
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
        public List<string> Bigrams { get; set; } = new List<string>();
        public List<string> FunctionSignatures { get; set; } = new List<string>();
        public Dictionary<string, double> TermFrequencies { get; set; } = new Dictionary<string, double>();
    }

    public class SearchResult
    {
        public string FilePath { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Preview { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string MatchType { get; set; } = "keyword";
    }

    internal class EmbeddingResponse
    {
        public List<EmbeddingData>? data { get; set; }
    }

    internal class EmbeddingData
    {
        public int index { get; set; }
        public float[]? embedding { get; set; }
    }
}