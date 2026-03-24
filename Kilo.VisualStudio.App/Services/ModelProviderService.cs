using System;
using System.Collections.Generic;

namespace Kilo.VisualStudio.App.Services
{
    public class ModelProvider
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string[] Models { get; set; } = Array.Empty<string>();
        public bool RequiresApiKey { get; set; } = true;
        public bool SupportsStreaming { get; set; } = true;
    }

    public class ModelProviderService
    {
        private static readonly List<ModelProvider> BuiltInProviders = new List<ModelProvider>
        {
            new ModelProvider
            {
                Id = "openai",
                Name = "OpenAI",
                DisplayName = "OpenAI",
                Endpoint = "https://api.openai.com/v1",
                Models = new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" },
                RequiresApiKey = true,
                SupportsStreaming = true
            },
            new ModelProvider
            {
                Id = "anthropic",
                Name = "Anthropic",
                DisplayName = "Anthropic",
                Endpoint = "https://api.anthropic.com/v1",
                Models = new[] { "claude-3-5-sonnet-20241022", "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" },
                RequiresApiKey = true,
                SupportsStreaming = true
            },
            new ModelProvider
            {
                Id = "google",
                Name = "Google",
                DisplayName = "Google AI",
                Endpoint = "https://generativelanguage.googleapis.com/v1beta",
                Models = new[] { "gemini-2.0-flash-exp", "gemini-1.5-pro", "gemini-1.5-flash" },
                RequiresApiKey = true,
                SupportsStreaming = true
            },
            new ModelProvider
            {
                Id = "azure",
                Name = "Azure",
                DisplayName = "Azure OpenAI",
                Endpoint = "",
                Models = new[] { "gpt-4o", "gpt-4-turbo", "gpt-35-turbo" },
                RequiresApiKey = false,
                SupportsStreaming = true
            },
            new ModelProvider
            {
                Id = "ollama",
                Name = "Ollama",
                DisplayName = "Ollama (Local)",
                Endpoint = "http://localhost:11434/v1",
                Models = new[] { "llama3.1", "mistral", "codellama", "phi3", "gemma2" },
                RequiresApiKey = false,
                SupportsStreaming = true
            },
            new ModelProvider
            {
                Id = "lmstudio",
                Name = "LM Studio",
                DisplayName = "LM Studio (Local)",
                Endpoint = "http://localhost:1234/v1",
                Models = new[] { "llama3.1", "mistral", "codellama" },
                RequiresApiKey = false,
                SupportsStreaming = true
            }
        };

        private ModelProvider? _selectedProvider;
        private string _selectedModel = "gpt-4o";

        public IReadOnlyList<ModelProvider> Providers => BuiltInProviders;
        public ModelProvider SelectedProvider => _selectedProvider ?? BuiltInProviders[0];
        public string SelectedModel => _selectedModel;

        public void SetProvider(string providerId)
        {
            foreach (var provider in BuiltInProviders)
            {
                if (provider.Id == providerId)
                {
                    _selectedProvider = provider;
                    _selectedModel = provider.Models[0];
                    return;
                }
            }
            _selectedProvider = BuiltInProviders[0];
            _selectedModel = _selectedProvider.Models[0];
        }

        public void SetModel(string model)
        {
            _selectedModel = model;
        }

        public string GetEndpoint()
        {
            return SelectedProvider.Endpoint;
        }

        public string GetModelId()
        {
            return _selectedModel;
        }

        public IReadOnlyList<string> GetModelsForProvider(string providerId)
        {
            foreach (var provider in BuiltInProviders)
            {
                if (provider.Id == providerId)
                {
                    return provider.Models;
                }
            }
            return Array.Empty<string>();
        }

        public string GetDisplayName()
        {
            return $"{SelectedProvider.DisplayName}: {_selectedModel}";
        }

        public bool IsLocalProvider()
        {
            return SelectedProvider.Id == "ollama" || SelectedProvider.Id == "lmstudio";
        }

        public bool RequiresApiKey()
        {
            return SelectedProvider.RequiresApiKey;
        }
    }
}