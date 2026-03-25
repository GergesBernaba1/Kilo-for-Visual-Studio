using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;
using Kilo.VisualStudio.Contracts.Services;

namespace Kilo.VisualStudio.Integration
{
    public class KiloBackendClient : IKiloBackendClient
    {
        private readonly HttpClient _httpClient;
        private readonly bool _useMock;

        public string? ApiKey { get; set; }
        public string BackendUrl { get; set; } = "https://api.kilo.example.com/assistant";

        public KiloBackendClient(HttpClient httpClient, bool useMock = false)
        {
            _httpClient = httpClient;
            _useMock = useMock;
        }

        public async Task<AssistantResponse> SendRequestAsync(AssistantRequest request)
        {
            if (_useMock)
            {
                return GenerateMockResponse(request);
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return new AssistantResponse
                {
                    IsSuccess = false,
                    Error = "API key not configured",
                    Message = "Please configure an API key in settings."
                };
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            // Provider/model routing: prefer explicit values from request, then fallback into combined model notation.
            var provider = request.ProviderId?.Trim() ?? string.Empty;
            var model = request.ModelId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(provider) && model.Contains(":"))
            {
                var colonIndex = model.IndexOf(':');
                if (colonIndex >= 0)
                {
                    provider = model.Substring(0, colonIndex).Trim();
                    model = model.Substring(colonIndex + 1).Trim();
                }
            }

            if (string.IsNullOrEmpty(provider)) provider = "openai";
            if (string.IsNullOrEmpty(model)) model = "default";

            var endpoint = provider.ToLowerInvariant() switch
            {
                "openai" => $"{BackendUrl.TrimEnd('/')}/openai/{model}",
                "anthropic" => $"{BackendUrl.TrimEnd('/')}/anthropic/{model}",
                "google" => $"{BackendUrl.TrimEnd('/')}/google/{model}",
                "local" or "ollama" => $"{BackendUrl.TrimEnd('/')}/local/{model}",
                _ => $"{BackendUrl.TrimEnd('/')}/{provider.ToLowerInvariant()}/{model}"
            };

            string jsonBody = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                return new AssistantResponse
                {
                    IsSuccess = false,
                    Error = $"Backend call failed: {response.StatusCode}",
                    Message = await response.Content.ReadAsStringAsync()
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var assistantResponse = JsonSerializer.Deserialize<AssistantResponse>(responseBody);

            return assistantResponse ?? new AssistantResponse
            {
                IsSuccess = false,
                Error = "Failed to deserialize backend response",
                Message = responseBody
            };
        }

        public async Task<TResponse> SendGenericRequestAsync<TRequest, TResponse>(string endpoint, TRequest request)
        {
            if (_useMock)
            {
                // For mock, return empty or default for autocomplete
                if (typeof(TResponse) == typeof(AutocompleteResponse))
                {
                    return (TResponse)(object)new AutocompleteResponse { Completions = new List<CompletionItem>() };
                }
                throw new NotImplementedException("Mock not implemented for this endpoint");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException("API key not configured");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            string jsonBody = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Generic calls don't have typed provider/model info; route by backend defaults.
            var provider = "openai";

            var url = provider.ToLowerInvariant() switch
            {
                "openai" => $"{BackendUrl.TrimEnd('/')}/openai/{endpoint}",
                "anthropic" => $"{BackendUrl.TrimEnd('/')}/anthropic/{endpoint}",
                "google" => $"{BackendUrl.TrimEnd('/')}/google/{endpoint}",
                "local" or "ollama" => $"{BackendUrl.TrimEnd('/')}/local/{endpoint}",
                _ => $"{BackendUrl.TrimEnd('/')}/{provider.ToLowerInvariant()}/{endpoint}"
            };

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Backend call failed: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TResponse>(responseBody);

            return result ?? throw new JsonException("Failed to deserialize response");
        }

        private static AssistantResponse GenerateMockResponse(AssistantRequest request)
        {
            var prompt = request.Prompt?.ToLowerInvariant() ?? string.Empty;
            var selectedText = request.SelectedText ?? string.Empty;

            string message;
            string? suggestedCode = null;

            if (prompt.Contains("explain") || prompt.Contains("what does"))
            {
                message = GenerateExplanation(selectedText);
            }
            else if (prompt.Contains("refactor") || prompt.Contains("improve"))
            {
                suggestedCode = GenerateRefactoredCode(selectedText);
                message = "Here's a refactored version of your code:";
            }
            else if (prompt.Contains("generate") || prompt.Contains("create"))
            {
                suggestedCode = GenerateCodeSnippet(request.LanguageId);
                message = "Here's a generated code snippet:";
            }
            else
            {
                message = $"[MOCK RESPONSE] Received your prompt: {request.Prompt}\n\n" +
                          $"Active file: {request.ActiveFilePath}\n" +
                          $"Language: {request.LanguageId}\n" +
                          $"Selected text length: {selectedText.Length} characters";
            }

            return new AssistantResponse
            {
                IsSuccess = true,
                Message = message,
                SuggestedCode = suggestedCode,
                PatchDiff = suggestedCode != null ? GeneratePatchDiff(selectedText, suggestedCode) : null,
                UsageCostUsd = 0.0015,
                UsageTokens = request.SelectedText?.Length / 3,
                ProviderId = request.ProviderId,
                ModelId = request.ModelId
            };
        }

        private static string GenerateExplanation(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "No code selected. Please select some code to explain.";
            }

            return $"[MOCK EXPLANATION]\n\n" +
                   $"The selected code appears to be a code snippet. " +
                   $"In a real implementation, this would use an LLM to provide a detailed explanation " +
                   $"of what the code does, including:\n\n" +
                   $"- Purpose and functionality\n" +
                   $"- Any potential issues or improvements\n" +
                   $"- Time/space complexity if applicable\n" +
                   $"- Related patterns or best practices\n\n" +
                   $"Selected code:\n```\n{code}\n```";
        }

        private static string GenerateRefactoredCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "// No code to refactor. Please select some code first.";
            }

            return $"// Refactored version\n{code}\n\n// Additional improvements could include:\n// - Adding proper error handling\n// - Improving variable names\n// - Adding documentation comments";
        }

        private static string GenerateCodeSnippet(string languageId)
        {
            var lang = languageId?.ToLowerInvariant() ?? "csharp";
            return lang switch
            {
                "csharp" or "cs" => "public class ExampleService\n{\n    public async Task<string> GetDataAsync()\n    {\n        // TODO: Implement data fetching logic\n        await Task.Delay(100);\n        return \"Sample Data\";\n    }\n}",
                "javascript" or "js" => "async function fetchData() {\n    // TODO: Implement fetch logic\n    const response = await fetch('/api/data');\n    return response.json();\n}",
                "python" or "py" => "def fetch_data():\n    # TODO: Implement data fetching\n    return \"Sample Data\"",
                _ => "// Code snippet generation not supported for this language"
            };
        }

        private static string GeneratePatchDiff(string original, string newCode)
        {
            if (string.IsNullOrWhiteSpace(original))
            {
                return $"--- a/empty\n+++ b/new\n@@ -0,0 +1,3 @@\n+{newCode}";
            }

            return $"--- a/original\n+++ b/refactored\n@@ -1,5 +1,7 @@\n-{original}\n+{newCode}";
        }
    }
}
